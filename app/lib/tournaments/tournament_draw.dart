import 'dart:math';

import 'package:flutter/material.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';

import '../api/models.dart';
import '../l10n/format.dart';
import '../l10n/strings.dart';
import '../theme/midango_colors.dart';

/// "Thu 19 Nov 18:00" for a match's start.
String matchTime(String iso, Strings s) => '${formatDay(dayOf(iso), s)} ${ltr(clock(iso))}';

/// "Thu 19 Nov 18:00 · Court 1", or null for a match with neither (a bye).
String? matchWhen(TournamentMatch m, Strings s) {
  final at = m.startsAt;
  final parts = [if (at != null) matchTime(at, s), ?m.courtName(s.isArabic)];
  return parts.isEmpty ? null : parts.join(' · ');
}

/// The draw tab: the champion once it's over, then the knockout bracket or the group tables.
class DrawTab extends StatelessWidget {
  const DrawTab({super.key, required this.tournament, required this.myTeamId});
  final Tournament tournament;
  final String? myTeamId;

  @override
  Widget build(BuildContext context) {
    final s = Strings.of(context);
    final text = Theme.of(context).textTheme;
    final t = tournament;
    final knockout = t.matches.where((m) => m.knockout).toList();
    return ListView(
      padding: const EdgeInsets.symmetric(vertical: 16),
      children: [
        if (championOf(t) != null)
          Padding(
            padding: const EdgeInsets.fromLTRB(16, 0, 16, 16),
            child: ChampionBanner(name: championOf(t)!),
          ),
        if (knockout.isNotEmpty) _Bracket(matches: knockout, myTeamId: myTeamId),
        for (final g in t.groups)
          Padding(
            padding: const EdgeInsets.fromLTRB(16, 0, 16, 24),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                if (t.groups.length > 1) ...[
                  Text(s.groupName(g.groupNo), style: text.titleMedium),
                  const SizedBox(height: 8),
                ],
                _GroupTable(group: g, myTeamId: myTeamId),
              ],
            ),
          ),
      ],
    );
  }
}

/// The champion's name, only once the tournament is finished.
String? championOf(Tournament t) => t.status == 'Finished' ? t.championTeamName : null;

class ChampionBanner extends StatelessWidget {
  const ChampionBanner({super.key, required this.name});
  final String name;

  @override
  Widget build(BuildContext context) {
    final s = Strings.of(context);
    final text = Theme.of(context).textTheme;
    return Container(
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(color: MidangoColors.navy, borderRadius: BorderRadius.circular(12)),
      child: Row(
        children: [
          const Icon(LucideIcons.trophy, color: MidangoColors.lime, size: 32),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(s.champion, style: text.bodySmall?.copyWith(color: MidangoColors.line)),
                Text(name, style: text.titleMedium?.copyWith(color: MidangoColors.white)),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

/// Rounds as columns, scrolling sideways (from the right in Arabic). Each round's matches sit centred
/// between the two matches that feed them.
class _Bracket extends StatelessWidget {
  const _Bracket({required this.matches, required this.myTeamId});
  final List<TournamentMatch> matches;
  final String? myTeamId;

  static const _slot = 84.0;

  @override
  Widget build(BuildContext context) {
    final s = Strings.of(context);
    final text = Theme.of(context).textTheme;
    final rounds = matches.map((m) => m.round).reduce(max);
    final byRound = [
      for (var r = 1; r <= rounds; r++) matches.where((m) => m.round == r).toList()..sort((a, b) => a.position - b.position),
    ];
    // Round r's match spans 2^(r-1) first-round slots.
    final slots = [for (final (i, round) in byRound.indexed) round.length << i].reduce(max);
    return SingleChildScrollView(
      scrollDirection: Axis.horizontal,
      padding: const EdgeInsets.symmetric(horizontal: 16),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          for (final (i, round) in byRound.indexed)
            Padding(
              padding: EdgeInsetsDirectional.only(end: i == rounds - 1 ? 0 : 16),
              child: SizedBox(
                width: 180,
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(s.knockoutRound(i + 1, rounds), style: text.labelLarge),
                    const SizedBox(height: 8),
                    SizedBox(
                      height: slots * _slot,
                      child: Column(
                        mainAxisAlignment: MainAxisAlignment.spaceAround,
                        children: [for (final m in round) MatchCard(match: m, myTeamId: myTeamId)],
                      ),
                    ),
                  ],
                ),
              ),
            ),
        ],
      ),
    );
  }
}

/// Both teams and their scores, the winner bold and the customer's own team on lime. `header` (time and
/// court) sits above them in the fixtures list.
class MatchCard extends StatelessWidget {
  const MatchCard({super.key, required this.match, required this.myTeamId, this.header});
  final TournamentMatch match;
  final String? myTeamId;
  final String? header;

  @override
  Widget build(BuildContext context) {
    final s = Strings.of(context);
    final text = Theme.of(context).textTheme;
    final m = match;
    Widget side(TournamentTeam? team, int? score) {
      final won = team != null && team.id == m.winnerTeamId;
      final style = text.labelLarge?.copyWith(
        fontWeight: won ? FontWeight.w700 : FontWeight.w400,
        color: team == null ? MidangoColors.muted : MidangoColors.navy,
      );
      return Container(
        color: team != null && team.id == myTeamId ? MidangoColors.lime : null,
        padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
        child: Row(
          children: [
            Expanded(
              child: Text(
                team?.name ?? (m.isBye ? s.bye : s.toBeDecided),
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: style,
              ),
            ),
            if (score != null) ...[const SizedBox(width: 8), Text('$score', style: style)],
          ],
        ),
      );
    }

    return Container(
      clipBehavior: Clip.antiAlias,
      decoration: BoxDecoration(
        color: MidangoColors.white,
        border: Border.all(color: MidangoColors.line),
        borderRadius: BorderRadius.circular(10),
      ),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          if (header != null)
            Padding(padding: const EdgeInsets.fromLTRB(12, 8, 12, 0), child: Text(header!, style: text.bodySmall)),
          side(m.teamA, m.scoreA),
          const Divider(height: 1, color: MidangoColors.line),
          side(m.teamB, m.scoreB),
        ],
      ),
    );
  }
}

/// One group's table: played, won, drawn, lost, difference and points; the customer's team on lime.
class _GroupTable extends StatelessWidget {
  const _GroupTable({required this.group, required this.myTeamId});
  final TournamentGroup group;
  final String? myTeamId;

  @override
  Widget build(BuildContext context) {
    final s = Strings.of(context);
    final text = Theme.of(context).textTheme;
    Widget cell(String value, TextStyle? style, {bool name = false}) => Padding(
          padding: const EdgeInsets.symmetric(horizontal: 4, vertical: 10),
          child: Text(value,
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
              textAlign: name ? TextAlign.start : TextAlign.center,
              style: style),
        );
    final row = text.labelLarge?.copyWith(fontWeight: FontWeight.w400);
    return Table(
      columnWidths: const {0: FlexColumnWidth()},
      defaultColumnWidth: const FixedColumnWidth(38),
      defaultVerticalAlignment: TableCellVerticalAlignment.middle,
      children: [
        TableRow(
          decoration: const BoxDecoration(border: Border(bottom: BorderSide(color: MidangoColors.line))),
          children: [
            cell(s.teamColumn, text.bodySmall, name: true),
            for (final c in s.standingsColumns) cell(c, text.bodySmall),
          ],
        ),
        for (final st in group.standings)
          TableRow(
            decoration: BoxDecoration(
              color: st.teamId == myTeamId ? MidangoColors.lime : null,
              border: const Border(bottom: BorderSide(color: MidangoColors.line)),
            ),
            children: [
              cell(st.name, row, name: true),
              cell('${st.played}', row),
              cell('${st.won}', row),
              cell('${st.drawn}', row),
              cell('${st.lost}', row),
              cell(ltr(_signed(st.scoreFor - st.scoreAgainst)), row),
              cell('${st.points}', row?.copyWith(fontWeight: FontWeight.w700)),
            ],
          ),
      ],
    );
  }

  static String _signed(int n) => n > 0 ? '+$n' : '$n';
}

/// The fixtures tab: the customer's next match first, then every match by round (and group).
class FixturesTab extends StatelessWidget {
  const FixturesTab({super.key, required this.tournament, required this.myTeamId});
  final Tournament tournament;
  final String? myTeamId;

  @override
  Widget build(BuildContext context) {
    final s = Strings.of(context);
    final text = Theme.of(context).textTheme;
    final t = tournament;
    final next = (t.matches.where((m) => m.involves(myTeamId) && !m.played && !m.isBye).toList()
          ..sort((a, b) => (a.startsAt ?? '~').compareTo(b.startsAt ?? '~')))
        .firstOrNull;
    final ordered = [...t.matches]..sort((a, b) => a.round != b.round
        ? a.round - b.round
        : (a.groupNo ?? 0) != (b.groupNo ?? 0)
            ? (a.groupNo ?? 0) - (b.groupNo ?? 0)
            : a.position - b.position);
    final rounds = t.matches.where((m) => m.knockout).map((m) => m.round).fold(0, max);
    String heading(TournamentMatch m) => m.knockout
        ? s.knockoutRound(m.round, rounds)
        : t.groups.length > 1
            ? '${s.groupName(m.groupNo ?? 1)} · ${s.groupRound(m.round)}'
            : s.groupRound(m.round);

    final children = <Widget>[];
    if (next != null) {
      children.addAll([
        Text(s.yourNextMatch, style: text.titleMedium),
        const SizedBox(height: 8),
        MatchCard(match: next, myTeamId: myTeamId, header: matchWhen(next, s)),
        const SizedBox(height: 24),
      ]);
    }
    String? last;
    for (final m in ordered) {
      final h = heading(m);
      if (h != last) {
        if (last != null) children.add(const SizedBox(height: 16));
        children.addAll([Text(h, style: text.titleMedium), const SizedBox(height: 8)]);
        last = h;
      }
      children.add(Padding(
        padding: const EdgeInsets.only(bottom: 8),
        child: MatchCard(match: m, myTeamId: myTeamId, header: matchWhen(m, s)),
      ));
    }
    return ListView(padding: const EdgeInsets.all(16), children: children);
  }
}
