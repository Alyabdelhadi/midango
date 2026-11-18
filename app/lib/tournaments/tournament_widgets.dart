import 'package:flutter/material.dart';

import '../api/models.dart';
import '../bookings/bookings_tab.dart';
import '../l10n/format.dart';
import '../l10n/strings.dart';
import '../matches/match_widgets.dart';
import '../widgets/media_image.dart';

/// "Thu 19 Nov · 18:00–22:00", or "Thu 19 Nov 18:00 – Fri 20 Nov 14:00" across days.
String tournamentWhen(Tournament t, Strings s) {
  final start = dayOf(t.startsAt), end = dayOf(t.endsAt);
  return start == end
      ? '${formatDay(start, s)} · ${timeRange(t.startsAt, t.endsAt)}'
      : '${formatDay(start, s)} ${ltr(clock(t.startsAt))} – ${formatDay(end, s)} ${ltr(clock(t.endsAt))}';
}

/// "Registration closes Thu 19 Nov 18:00" or "Registration closed".
String registrationLine(Tournament t, Strings s) => t.registrationOpen
    ? s.registrationCloses('${formatDay(dayOf(t.registrationClosesAt), s)} ${ltr(clock(t.registrationClosesAt))}')
    : s.registrationClosed;

/// The fee for one team, or "Free entry".
String teamFee(Tournament t, Strings s) => t.entryFee == 0 ? s.freeEntry : money(t.entryFee, t.currency, s);

/// The team's state in words: Checked in, Registered, Waitlist or Withdrawn.
StatusBadge teamStatusBadge(TournamentTeam team, Strings s) => switch (team.status) {
      _ when team.checkedInAt != null => StatusBadge(label: s.statusCheckedIn, highlight: true),
      'Registered' => StatusBadge(label: s.statusRegistered, highlight: true),
      'Waitlisted' => StatusBadge(label: s.statusWaitlist),
      _ => StatusBadge(label: s.statusWithdrawn),
    };

/// One tournament in the Play list: venue, name, dates, format, team size, fee and how full it is.
class TournamentCardView extends StatelessWidget {
  const TournamentCardView({super.key, required this.tournament, required this.onOpen});
  final Tournament tournament;
  final VoidCallback onOpen;

  @override
  Widget build(BuildContext context) {
    final s = Strings.of(context);
    final text = Theme.of(context).textTheme;
    final t = tournament;
    final logo = t.venueLogoUrl;
    return Card(
      margin: EdgeInsets.zero,
      clipBehavior: Clip.antiAlias,
      child: InkWell(
        onTap: onOpen,
        child: Padding(
          padding: const EdgeInsets.all(16),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  if (logo != null) ...[
                    ClipRRect(borderRadius: BorderRadius.circular(8), child: MediaImage(logo, width: 40, height: 40)),
                    const SizedBox(width: 12),
                  ],
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(t.name(s.isArabic), style: text.titleMedium),
                        Text(t.venueName(s.isArabic), style: text.bodySmall),
                      ],
                    ),
                  ),
                  const SizedBox(width: 12),
                  Column(
                    crossAxisAlignment: CrossAxisAlignment.end,
                    children: [
                      Text(teamFee(t, s), style: text.labelLarge),
                      if (t.entryFee > 0) Text(s.perTeam, style: text.bodySmall),
                    ],
                  ),
                ],
              ),
              const SizedBox(height: 10),
              Text(tournamentWhen(t, s), style: text.bodyLarge),
              Text('${s.formatName(t.format)} · ${s.teamSize(t.teamSize)}', style: text.bodySmall),
              const SizedBox(height: 8),
              Wrap(
                spacing: 8,
                runSpacing: 6,
                crossAxisAlignment: WrapCrossAlignment.center,
                children: [
                  LevelBadge(level: t.level),
                  Text(s.teamsCount(t.registered, t.maxTeams), style: text.bodySmall),
                  if (t.waitlisted > 0) Text(s.onWaitlist(t.waitlisted), style: text.bodySmall),
                ],
              ),
              const SizedBox(height: 8),
              Text(registrationLine(t, s), style: text.bodySmall),
            ],
          ),
        ),
      ),
    );
  }
}
