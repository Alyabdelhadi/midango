import 'package:flutter/material.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';

import '../api/api_client.dart';
import '../api/models.dart';
import '../home_shell.dart';
import '../l10n/format.dart';
import '../l10n/strings.dart';
import '../push/push.dart';
import '../session/session.dart';
import '../theme/midango_colors.dart';
import '../widgets/busy_button.dart';
import 'confirmation_page.dart';
import 'sign_in_flow.dart';

/// Opens the confirm sheet for one start time. Pass `venue` when the caller has it; otherwise it's fetched.
Future<void> showConfirmSheet(BuildContext context,
    {required String venueId, VenueDetail? venue, required Slot slot, required int minutes}) async {
  final s = Strings.of(context);
  var detail = venue;
  if (detail == null) {
    try {
      detail = VenueDetail.fromJson(await SessionScope.read(context).api.get('/venues/$venueId') as JsonMap);
    } catch (_) {
      if (context.mounted) ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(s.somethingWrong)));
      return;
    }
  }
  if (!context.mounted) return;
  await showModalBottomSheet<void>(
    context: context,
    isScrollControlled: true,
    useSafeArea: true,
    showDragHandle: true,
    backgroundColor: MidangoColors.white,
    builder: (_) => ConfirmSheet(venue: detail!, slot: slot, minutes: minutes),
  );
}

class ConfirmSheet extends StatefulWidget {
  const ConfirmSheet({super.key, required this.venue, required this.slot, required this.minutes});

  final VenueDetail venue;
  final Slot slot;
  final int minutes;

  @override
  State<ConfirmSheet> createState() => _ConfirmSheetState();
}

class _ConfirmSheetState extends State<ConfirmSheet> {
  /// Free courts at this time, cheapest first; the first is preselected.
  late final List<SlotCourt> _courts = [...widget.slot.courts]..sort((a, b) => a.total.compareTo(b.total));
  late SlotCourt _court = _courts.first;
  bool _busy = false;
  String? _error;

  Future<void> _book() async {
    final s = Strings.of(context);
    final session = SessionScope.read(context);
    final nav = Navigator.of(context);
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      if (!await ensureSignedIn(context)) {
        if (mounted) setState(() => _busy = false);
        return;
      }
      final json = await session.api.post(
          '/bookings', {'courtId': _court.courtId, 'start': widget.slot.start, 'minutes': widget.minutes}, true);
      final booking = Booking.fromJson(json as JsonMap);
      bookingsChanged.value++;
      nav.pop();
      nav.push(MaterialPageRoute<void>(builder: (_) => ConfirmationPage(booking: booking)));
      push.askPermissionOnce(); // over the confirmation page, after the first booking
    } on ApiException catch (e) {
      if (e.status == 409) bookingsChanged.value++; // someone took the time: the caller shows fresh times
      if (mounted) {
        setState(() {
          _busy = false;
          _error = e.title;
        });
      }
    } catch (_) {
      if (mounted) {
        setState(() {
          _busy = false;
          _error = s.somethingWrong;
        });
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final s = Strings.of(context);
    final text = Theme.of(context).textTheme;
    final venue = widget.venue;
    final pricesDiffer = _courts.any((c) => c.total != _courts.first.total);
    final total = money(_court.total, venue.currency, s);

    return SingleChildScrollView(
      padding: const EdgeInsets.fromLTRB(24, 0, 24, 24),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(s.confirmTitle, style: text.bodySmall),
          const SizedBox(height: 4),
          Text(venue.name(s.isArabic), style: text.titleLarge),
          const SizedBox(height: 8),
          Text(
            '${formatDay(dayOf(widget.slot.start), s)} · ${timeRangeFor(widget.slot.start, widget.minutes)}',
            style: text.titleMedium,
          ),
          Text(s.minutes(widget.minutes), style: text.bodySmall),
          if (_courts.length > 1) ...[
            const SizedBox(height: 20),
            Text(s.courtLabel, style: text.labelLarge),
            const SizedBox(height: 8),
            Wrap(
              spacing: 8,
              runSpacing: 8,
              children: [
                for (final c in _courts)
                  ChoiceChip(
                    label: Text([
                      venue.court(c.courtId)?.name(s.isArabic) ?? '',
                      if (pricesDiffer) money(c.total, venue.currency, s),
                    ].join(' · ')),
                    selected: c == _court,
                    showCheckmark: false,
                    selectedColor: MidangoColors.lime,
                    backgroundColor: MidangoColors.white,
                    labelStyle: text.labelLarge,
                    side: BorderSide(color: c == _court ? MidangoColors.lime : MidangoColors.line),
                    onSelected: _busy ? null : (_) => setState(() => _court = c),
                  ),
              ],
            ),
          ] else ...[
            const SizedBox(height: 12),
            Text('${s.courtLabel}: ${venue.court(_court.courtId)?.name(s.isArabic) ?? ''}', style: text.bodyLarge),
          ],
          const Divider(height: 32, color: MidangoColors.line),
          Row(
            crossAxisAlignment: CrossAxisAlignment.end,
            children: [
              Expanded(child: Text(s.totalLabel, style: text.bodyLarge)),
              Text(total, style: text.titleLarge),
            ],
          ),
          Align(alignment: AlignmentDirectional.centerEnd, child: Text(s.includesVat, style: text.bodySmall)),
          const SizedBox(height: 16),
          InfoLine(icon: LucideIcons.banknote, text: s.payAtVenue),
          InfoLine(icon: LucideIcons.calendarX, text: s.freeCancellationHours(venue.freeCancellationHours)),
          if (_error != null) ...[
            const SizedBox(height: 12),
            Text(_error!, style: text.bodyLarge?.copyWith(color: MidangoColors.error)),
          ],
          const SizedBox(height: 20),
          SizedBox(
            width: double.infinity,
            child: BusyButton(label: s.bookFor(total), busy: _busy, onPressed: _book),
          ),
        ],
      ),
    );
  }
}

/// An icon and one line of text, used for payment, cancellation and booking details.
class InfoLine extends StatelessWidget {
  const InfoLine({super.key, required this.icon, required this.text, this.style});
  final IconData icon;
  final String text;
  final TextStyle? style;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 6),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Icon(icon, size: 20, color: MidangoColors.slate),
          const SizedBox(width: 12),
          Expanded(child: Text(text, style: style ?? Theme.of(context).textTheme.bodyLarge)),
        ],
      ),
    );
  }
}
