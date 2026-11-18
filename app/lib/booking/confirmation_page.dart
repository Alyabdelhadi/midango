import 'package:flutter/material.dart';
import 'package:google_fonts/google_fonts.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';
import 'package:qr_flutter/qr_flutter.dart';

import '../api/models.dart';
import '../home_shell.dart';
import '../l10n/format.dart';
import '../l10n/strings.dart';
import '../theme/midango_colors.dart';
import 'confirm_sheet.dart';

/// "You're booked": the reference and its QR code for the desk, and what was booked.
class ConfirmationPage extends StatelessWidget {
  const ConfirmationPage({super.key, required this.booking});
  final Booking booking;

  void _leave(BuildContext context, int tab) {
    Navigator.of(context).popUntil((route) => route.isFirst);
    shellTab.value = tab;
  }

  @override
  Widget build(BuildContext context) {
    final s = Strings.of(context);
    final text = Theme.of(context).textTheme;
    return Scaffold(
      backgroundColor: MidangoColors.white,
      body: SafeArea(
        child: ListView(
          padding: const EdgeInsets.all(24),
          children: [
            const Icon(LucideIcons.circleCheck, size: 40, color: MidangoColors.navy),
            const SizedBox(height: 12),
            Text(s.booked, style: text.headlineLarge),
            const SizedBox(height: 24),
            BookingCode(reference: booking.reference),
            const SizedBox(height: 24),
            BookingSummary(booking: booking),
            const SizedBox(height: 24),
            FilledButton(onPressed: () => _leave(context, 1), child: Text(s.viewMyBookings)),
            const SizedBox(height: 12),
            OutlinedButton(onPressed: () => _leave(context, 0), child: Text(s.done)),
          ],
        ),
      ),
    );
  }
}

/// The reference in large letters and as a QR code (the bare reference — the desk's scanner types it in).
class BookingCode extends StatelessWidget {
  const BookingCode({super.key, required this.reference});
  final String reference;

  @override
  Widget build(BuildContext context) {
    final s = Strings.of(context);
    final text = Theme.of(context).textTheme;
    return Column(
      children: [
        Text(s.referenceLabel, style: text.bodySmall),
        const SizedBox(height: 4),
        Text(
          reference,
          textDirection: TextDirection.ltr,
          style: GoogleFonts.spaceGrotesk(
              fontSize: 36, fontWeight: FontWeight.w700, letterSpacing: 6, color: MidangoColors.navy),
        ),
        const SizedBox(height: 12),
        Container(
          padding: const EdgeInsets.all(12),
          decoration: BoxDecoration(
            color: MidangoColors.white,
            border: Border.all(color: MidangoColors.line),
            borderRadius: BorderRadius.circular(12),
          ),
          child: QrImageView(
            data: reference,
            size: 200,
            backgroundColor: MidangoColors.white,
            semanticsLabel: reference,
          ),
        ),
        const SizedBox(height: 8),
        Text(s.showAtDesk, style: text.bodyLarge),
      ],
    );
  }
}

/// Venue, day and time, court, total, cash at the venue and the free-cancellation line.
class BookingSummary extends StatelessWidget {
  const BookingSummary({super.key, required this.booking});
  final Booking booking;

  @override
  Widget build(BuildContext context) {
    final s = Strings.of(context);
    final b = booking;
    final until = b.freeCancellationUntil;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        InfoLine(icon: LucideIcons.mapPin, text: b.venueName(s.isArabic)),
        InfoLine(
          icon: LucideIcons.calendar,
          text: '${formatDay(dayOf(b.startsAt), s)} · ${timeRange(b.startsAt, b.endsAt)} · ${s.minutes(b.minutes)}',
        ),
        InfoLine(icon: LucideIcons.layoutGrid, text: b.courtName(s.isArabic)),
        InfoLine(icon: LucideIcons.banknote, text: s.payAmountAtVenue(money(b.total, b.currency, s))),
        if (b.confirmed)
          InfoLine(
            icon: LucideIcons.calendarX,
            text: s.freeCancellationUntil('${formatDay(dayOf(until), s)} ${ltr(clock(until))}'),
          ),
      ],
    );
  }
}
