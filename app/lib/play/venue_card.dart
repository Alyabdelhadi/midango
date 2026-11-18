import 'package:flutter/material.dart';

import '../api/models.dart';
import '../l10n/format.dart';
import '../l10n/strings.dart';
import '../widgets/media_image.dart';
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
    final photo = venue.photoUrl;
    final logo = venue.logoUrl;
    final heading = Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(venue.name(s.isArabic), style: text.titleMedium),
        if (address != null && address.isNotEmpty) ...[
          const SizedBox(height: 2),
          Text(address, style: text.bodySmall),
        ],
      ],
    );
    return Card(
      margin: EdgeInsets.zero,
      clipBehavior: Clip.antiAlias,
      child: InkWell(
        onTap: onOpen,
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            if (photo != null) AspectRatio(aspectRatio: 16 / 9, child: MediaImage(photo)),
            Padding(
              padding: const EdgeInsets.all(16),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  if (logo == null)
                    heading
                  else
                    Row(
                      children: [
                        ClipRRect(borderRadius: BorderRadius.circular(8), child: MediaImage(logo, width: 40, height: 40)),
                        const SizedBox(width: 12),
                        Expanded(child: heading),
                      ],
                    ),
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
          ],
        ),
      ),
    );
  }
}
