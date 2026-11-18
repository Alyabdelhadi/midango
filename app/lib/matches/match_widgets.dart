import 'package:flutter/material.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';

import '../api/models.dart';
import '../bookings/bookings_tab.dart';
import '../l10n/format.dart';
import '../l10n/strings.dart';
import '../play/pickers.dart';
import '../theme/midango_colors.dart';
import '../widgets/media_image.dart';

/// A player's initial in a small navy circle.
class PlayerAvatar extends StatelessWidget {
  const PlayerAvatar({super.key, required this.firstName, this.size = 32});
  final String firstName;
  final double size;

  @override
  Widget build(BuildContext context) {
    final initial = firstName.isEmpty ? '?' : firstName.characters.first.toUpperCase();
    return Container(
      width: size,
      height: size,
      alignment: Alignment.center,
      decoration: const BoxDecoration(color: MidangoColors.navy, shape: BoxShape.circle),
      child: Text(initial,
          style: Theme.of(context).textTheme.labelLarge?.copyWith(color: MidangoColors.white, height: 1)),
    );
  }
}

/// Everyone in the match, then an empty ring for each open spot.
class PlayerAvatars extends StatelessWidget {
  const PlayerAvatars({super.key, required this.names, required this.maxPlayers});
  final List<String> names;
  final int maxPlayers;

  @override
  Widget build(BuildContext context) {
    final s = Strings.of(context);
    final open = (maxPlayers - names.length).clamp(0, maxPlayers);
    return Semantics(
      label: '${names.join(', ')}. ${s.spotsLeft(open)}',
      excludeSemantics: true,
      child: Wrap(
        spacing: 6,
        runSpacing: 6,
        children: [
          for (final name in names) PlayerAvatar(firstName: name),
          for (var i = 0; i < open; i++)
            Container(
              width: 32,
              height: 32,
              decoration: BoxDecoration(
                shape: BoxShape.circle,
                border: Border.all(color: MidangoColors.line, width: 1.5),
              ),
            ),
        ],
      ),
    );
  }
}

/// The match's level on a lime pill ("Any level" when open to all).
class LevelBadge extends StatelessWidget {
  const LevelBadge({super.key, required this.level});
  final String? level;

  @override
  Widget build(BuildContext context) {
    final s = Strings.of(context);
    return StatusBadge(label: level == null ? s.levelAny : s.levelName(level!), highlight: true);
  }
}

/// One open match in the Play list: venue, court, time, level, spots left, the share and who's in.
class MatchCardView extends StatelessWidget {
  const MatchCardView({super.key, required this.match, required this.onOpen});
  final OpenMatch match;
  final VoidCallback onOpen;

  @override
  Widget build(BuildContext context) {
    final s = Strings.of(context);
    final text = Theme.of(context).textTheme;
    final m = match;
    final logo = m.venueLogoUrl;
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
                        Text(m.venueName(s.isArabic), style: text.titleMedium),
                        Text(m.courtName(s.isArabic), style: text.bodySmall),
                      ],
                    ),
                  ),
                  const SizedBox(width: 12),
                  Column(
                    crossAxisAlignment: CrossAxisAlignment.end,
                    children: [
                      Text(money(m.share, m.currency, s), style: text.labelLarge),
                      Text(s.perPlayer, style: text.bodySmall),
                    ],
                  ),
                ],
              ),
              const SizedBox(height: 10),
              Text('${timeRange(m.startsAt, m.endsAt)} · ${s.minutes(m.minutes)}', style: text.bodyLarge),
              const SizedBox(height: 8),
              Row(
                children: [
                  LevelBadge(level: m.level),
                  const SizedBox(width: 8),
                  Text(s.spotsLeft(m.spotsLeft), style: text.bodySmall),
                ],
              ),
              const SizedBox(height: 12),
              PlayerAvatars(names: [for (final p in m.players) p.firstName], maxPlayers: m.maxPlayers),
            ],
          ),
        ),
      ),
    );
  }
}

/// How many players and which level, with the share each pays if every spot fills.
/// Used when opening a booking (confirm sheet) and editing an open one (booking detail).
class MatchSettings extends StatelessWidget {
  const MatchSettings({
    super.key,
    required this.min,
    required this.max,
    required this.players,
    required this.level,
    required this.total,
    required this.currency,
    required this.onPlayers,
    required this.onLevel,
  });

  final int min;
  final int max;
  final int players;
  final String? level;
  final double total;
  final String currency;

  /// Null disables the controls (while saving).
  final ValueChanged<int>? onPlayers;
  final ValueChanged<String?>? onLevel;

  @override
  Widget build(BuildContext context) {
    final s = Strings.of(context);
    final text = Theme.of(context).textTheme;
    final change = onPlayers;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Row(
          children: [
            Expanded(child: Text(s.playersHeading, style: text.labelLarge)),
            IconButton.outlined(
              tooltip: s.fewerPlayers,
              onPressed: change == null || players <= min ? null : () => change(players - 1),
              icon: const Icon(LucideIcons.minus, size: 20),
            ),
            SizedBox(
              width: 40,
              child: Text('$players', textAlign: TextAlign.center, style: text.titleMedium),
            ),
            IconButton.outlined(
              tooltip: s.morePlayers,
              onPressed: change == null || players >= max ? null : () => change(players + 1),
              icon: const Icon(LucideIcons.plus, size: 20),
            ),
          ],
        ),
        const SizedBox(height: 12),
        Text(s.levelLabel, style: text.labelLarge),
        const SizedBox(height: 8),
        LevelPicker(selected: level, onSelected: onLevel, inline: true),
        const SizedBox(height: 12),
        Row(
          children: [
            Expanded(child: Text(s.yourShare, style: text.bodyLarge)),
            Text(money(shareOf(total, players), currency, s), style: text.titleMedium),
          ],
        ),
      ],
    );
  }
}
