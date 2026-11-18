import 'package:flutter/material.dart';

import '../api/api_client.dart';
import '../api/models.dart';
import '../booking/confirmation_page.dart';
import '../home_shell.dart';
import '../l10n/strings.dart';
import '../session/session.dart';
import '../theme/midango_colors.dart';
import 'bookings_tab.dart';

/// One booking: the QR code while it's ahead, its details and status, and cancelling.
class BookingDetailPage extends StatefulWidget {
  const BookingDetailPage({super.key, required this.booking});
  final Booking booking;

  @override
  State<BookingDetailPage> createState() => _BookingDetailPageState();
}

class _BookingDetailPageState extends State<BookingDetailPage> {
  late Booking _booking = widget.booking;
  bool _busy = false;
  String? _error;

  Future<void> _cancel() async {
    final s = Strings.of(context);
    final messenger = ScaffoldMessenger.of(context);
    final api = SessionScope.read(context).api;
    final yes = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: Text(s.cancelTitle),
        content: Text(_booking.freeToCancel ? s.cancelFree : s.cancelLate),
        actions: [
          TextButton(onPressed: () => Navigator.pop(context, false), child: Text(s.keepBooking)),
          FilledButton(
            style: FilledButton.styleFrom(backgroundColor: MidangoColors.error, foregroundColor: MidangoColors.white),
            onPressed: () => Navigator.pop(context, true),
            child: Text(s.cancelBooking),
          ),
        ],
      ),
    );
    if (yes != true || !mounted) return;

    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      final json = await api.post('/bookings/${_booking.id}/cancel', null, true);
      bookingsChanged.value++;
      if (!mounted) return;
      setState(() {
        _booking = Booking.fromJson(json as JsonMap);
        _busy = false;
      });
      messenger.showSnackBar(SnackBar(content: Text(s.bookingCancelled)));
    } catch (e) {
      if (mounted) {
        setState(() {
          _busy = false;
          _error = e is ApiException ? e.title : s.somethingWrong;
        });
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final s = Strings.of(context);
    final text = Theme.of(context).textTheme;
    final b = _booking;
    return Scaffold(
      appBar: AppBar(title: Text(s.bookingTitle)),
      body: ListView(
        padding: const EdgeInsets.all(24),
        children: [
          if (b.upcoming) ...[
            BookingCode(reference: b.reference),
            const SizedBox(height: 24),
          ] else ...[
            Text(b.reference, style: text.titleMedium, textDirection: TextDirection.ltr),
            const SizedBox(height: 8),
          ],
          Align(
            alignment: AlignmentDirectional.centerStart,
            child: StatusBadge(label: statusLabel(b, s) ?? s.statusConfirmed),
          ),
          const SizedBox(height: 12),
          BookingSummary(booking: b),
          if (_error != null) ...[
            const SizedBox(height: 12),
            Text(_error!, style: text.bodyLarge?.copyWith(color: MidangoColors.error)),
          ],
          if (b.cancellable) ...[
            const SizedBox(height: 24),
            OutlinedButton(
              onPressed: _busy ? null : _cancel,
              child: _busy
                  ? const SizedBox(width: 20, height: 20, child: CircularProgressIndicator(strokeWidth: 2))
                  : Text(s.cancelBooking),
            ),
          ],
        ],
      ),
    );
  }
}
