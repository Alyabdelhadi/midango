import 'package:flutter/material.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';

import '../api/models.dart';
import '../home_shell.dart';
import '../l10n/format.dart';
import '../l10n/strings.dart';
import '../session/session.dart';
import '../theme/midango_colors.dart';

/// Balance, tier, progress to the next tier and the earn rate; opens the history.
class PointsCard extends StatefulWidget {
  const PointsCard({super.key});

  @override
  State<PointsCard> createState() => _PointsCardState();
}

class _PointsCardState extends State<PointsCard> {
  Points? _points;

  @override
  void initState() {
    super.initState();
    shellTab.addListener(_onTab);
    bookingsChanged.addListener(_load);
    _load();
  }

  @override
  void dispose() {
    shellTab.removeListener(_onTab);
    bookingsChanged.removeListener(_load);
    super.dispose();
  }

  // Points move at the desk, so the card reloads whenever Profile is opened.
  void _onTab() {
    if (shellTab.value == 2) _load();
  }

  Future<void> _load() async {
    try {
      final points = Points.fromJson(await SessionScope.read(context).api.get('/me/points', auth: true) as JsonMap);
      if (mounted) setState(() => _points = points);
    } catch (_) {
      // Offline: the card stays hidden (or stale) until the tab is opened again.
    }
  }

  @override
  Widget build(BuildContext context) {
    final p = _points;
    if (p == null) return const SizedBox.shrink();
    final s = Strings.of(context);
    final text = Theme.of(context).textTheme;
    final white = text.bodySmall?.copyWith(color: MidangoColors.white);
    final needed = p.nextTierGames;
    return Padding(
      padding: const EdgeInsets.only(top: 16),
      child: Card(
        margin: EdgeInsets.zero,
        color: MidangoColors.navy,
        clipBehavior: Clip.antiAlias,
        child: InkWell(
          onTap: () => Navigator.of(context, rootNavigator: true)
              .push(MaterialPageRoute<void>(builder: (_) => PointsHistoryPage(points: p))),
          child: Padding(
            padding: const EdgeInsets.all(16),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Row(
                  children: [
                    Expanded(child: Text(s.pointsTitle, style: white)),
                    TierBadge(tier: p.tier),
                  ],
                ),
                const SizedBox(height: 4),
                Text(s.pointsCount(p.balance),
                    style: text.headlineLarge?.copyWith(color: MidangoColors.white)),
                const SizedBox(height: 12),
                if (needed != null && p.nextTierName != null) ...[
                  ClipRRect(
                    borderRadius: BorderRadius.circular(4),
                    child: LinearProgressIndicator(
                      value: p.gamesInWindow / (p.gamesInWindow + needed),
                      minHeight: 6,
                      color: MidangoColors.lime,
                      backgroundColor: MidangoColors.slate,
                    ),
                  ),
                  const SizedBox(height: 8),
                  Text(s.gamesToNext(needed, s.tierName(p.nextTierName!)), style: white),
                ] else
                  Text(s.topTier, style: white),
                const SizedBox(height: 4),
                Row(
                  children: [
                    Expanded(child: Text(s.earnMultiplier(plain(p.multiplier)), style: white)),
                    Text(s.pointsHistory, style: white),
                    Icon(s.isArabic ? LucideIcons.chevronLeft : LucideIcons.chevronRight,
                        size: 16, color: MidangoColors.white),
                  ],
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}

/// The tier on a brand pill: Gold on lime, Silver on cyan, Bronze on grey.
class TierBadge extends StatelessWidget {
  const TierBadge({super.key, required this.tier});
  final String tier;

  @override
  Widget build(BuildContext context) {
    final (bg, fg) = switch (tier) {
      'Gold' => (MidangoColors.lime, MidangoColors.navy),
      'Silver' => (MidangoColors.cyan, MidangoColors.navy),
      _ => (MidangoColors.disabledBg, MidangoColors.slate),
    };
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
      decoration: BoxDecoration(color: bg, borderRadius: BorderRadius.circular(6)),
      child: Text(Strings.of(context).tierName(tier),
          style: Theme.of(context).textTheme.bodySmall?.copyWith(color: fg, fontWeight: FontWeight.w600)),
    );
  }
}

/// The latest points entries: what was earned, used, reversed or returned.
class PointsHistoryPage extends StatelessWidget {
  const PointsHistoryPage({super.key, required this.points});
  final Points points;

  @override
  Widget build(BuildContext context) {
    final s = Strings.of(context);
    final text = Theme.of(context).textTheme;
    return Scaffold(
      appBar: AppBar(title: Text(s.pointsHistory)),
      body: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          Row(
            children: [
              Expanded(child: Text(s.pointsCount(points.balance), style: text.titleLarge)),
              TierBadge(tier: points.tier),
            ],
          ),
          const SizedBox(height: 16),
          if (points.history.isEmpty)
            Text(s.noPointsYet, style: text.bodyLarge)
          else
            Card(
              margin: EdgeInsets.zero,
              child: Column(
                children: [
                  for (final (i, e) in points.history.indexed) ...[
                    if (i > 0) const Divider(height: 1, color: MidangoColors.line),
                    ListTile(
                      title: Text(s.pointsKind(e.kind), style: text.bodyLarge),
                      subtitle: Text(
                        [?e.venueName(s.isArabic), formatDay(dayOf(e.at), s)].join(' · '),
                        style: text.bodySmall,
                      ),
                      trailing: Text(
                        ltr(e.points > 0 ? '+${e.points}' : '${e.points}'),
                        style: text.titleMedium
                            ?.copyWith(color: e.points < 0 ? MidangoColors.muted : MidangoColors.navy),
                      ),
                    ),
                  ],
                ],
              ),
            ),
        ],
      ),
    );
  }
}
