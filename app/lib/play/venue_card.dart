import 'package:flutter/material.dart';

import '../api/models.dart';
import '../l10n/format.dart';
import '../l10n/strings.dart';
import '../widgets/slot_chip.dart';

/// One venue in the Play list: name, address, "from" price and its first open times.
class VenueCardView extends StatelessWidget {
  const VenueCardView({super.key, required this.venue, required this.onOpen, required this.onTime});

  final VenueCard venue;
  final VoidCallback onOpen;
  final ValueChanged<Slot> onTime;

  @override
  Widget build(BuildContext context) {
    final s = Strings.of(context);
    final text = Theme.of(context).textTheme;
    final address = venue.address(s.isArabic);
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
              Text(venue.name(s.isArabic), style: text.titleMedium),
              if (address != null && address.isNotEmpty) ...[
                const SizedBox(height: 2),
                Text(address, style: text.bodySmall),
              ],
              if (venue.fromTotal != null) ...[
                const SizedBox(height: 6),
                Text(s.fromPrice(money(venue.fromTotal!, venue.currency, s)), style: text.labelLarge),
              ],
              const SizedBox(height: 12),
              if (venue.times.isEmpty)
                Text(venue.open ? s.noOpenTimes : s.closedThisDay, style: text.bodySmall)
              else
                Wrap(
                  spacing: 8,
                  runSpacing: 8,
                  children: [
                    for (final slot in venue.times)
                      SlotChip(
                        time: slot.localTime,
                        available: true,
                        semanticLabel: s.slotOpen(slot.localTime),
                        onTap: () => onTime(slot),
                      ),
                  ],
                ),
            ],
          ),
        ),
      ),
    );
  }
}
