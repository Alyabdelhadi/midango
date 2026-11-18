import 'dart:async';

import 'package:flutter/material.dart';

import '../api/models.dart';
import '../booking/sign_in_flow.dart';
import '../home_shell.dart';
import '../l10n/format.dart';
import '../l10n/strings.dart';
import '../session/session.dart';
import '../theme/midango_colors.dart';
import '../widgets/error_retry.dart';
import 'booking_detail_page.dart';

/// The status in words, or null for a plain confirmed booking.
String? statusLabel(Booking b, Strings s) => switch (b.status) {
      _ when b.playerStatus == 'removed' => s.statusRemoved,
      _ when b.playerStatus == 'left' => s.statusLeft,
      'Cancelled' when b.cancelledByVenue => s.statusCancelledByVenue,
      'Cancelled' when b.lateCancellation => s.statusLateCancellation,
      'Cancelled' => s.statusCancelled,
      'NoShow' => s.statusNoShow,
      _ => null,
    };

/// Upcoming and past bookings; a sign-in prompt when signed out.
class BookingsTab extends StatefulWidget {
  const BookingsTab({super.key});

  @override
  State<BookingsTab> createState() => _BookingsTabState();
}

class _BookingsTabState extends State<BookingsTab> {
  MyBookings? _data;
  bool _failed = false;
  bool? _signedIn;
  int _request = 0;

  @override
  void initState() {
    super.initState();
    shellTab.addListener(_onTab);
    bookingsChanged.addListener(_load);
  }

  @override
  void dispose() {
    shellTab.removeListener(_onTab);
    bookingsChanged.removeListener(_load);
    super.dispose();
  }

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    final signedIn = SessionScope.of(context).signedIn;
    if (signedIn != _signedIn) {
      _signedIn = signedIn;
      _data = null;
      if (signedIn) scheduleMicrotask(_load); // not during build
    }
  }

  void _onTab() {
    if (shellTab.value == 1) _load();
  }

  Future<void> _load() async {
    if (!mounted) return;
    final session = SessionScope.read(context);
    if (!session.signedIn) return;
    final request = ++_request;
    setState(() => _failed = false);
    try {
      final data = MyBookings.fromJson(await session.api.get('/me/bookings', auth: true) as JsonMap);
      if (mounted && request == _request) setState(() => _data = data);
    } catch (_) {
      if (mounted && request == _request) setState(() => _failed = true);
    }
  }

  @override
  Widget build(BuildContext context) {
    final s = Strings.of(context);
    final text = Theme.of(context).textTheme;
    final signedIn = SessionScope.of(context).signedIn;
    final data = _data;

    return SafeArea(
      child: RefreshIndicator(
        onRefresh: _load,
        child: ListView(
          padding: const EdgeInsets.fromLTRB(16, 16, 16, 24),
          children: [
            Text(s.bookingsTitle, style: text.titleLarge),
            const SizedBox(height: 16),
            if (!signedIn) ...[
              Text(s.signInForBookings, style: text.bodyLarge),
              const SizedBox(height: 16),
              FilledButton(onPressed: () => ensureSignedIn(context), child: Text(s.signIn)),
            ] else if (data == null && _failed)
              ErrorRetry(onRetry: _load)
            else if (data == null)
              const Padding(padding: EdgeInsets.all(32), child: Center(child: CircularProgressIndicator()))
            else ...[
              Text(s.upcoming, style: text.titleMedium),
              const SizedBox(height: 8),
              if (data.upcoming.isEmpty) ...[
                Text(s.noUpcoming, style: text.bodyLarge),
                const SizedBox(height: 12),
                Align(
                  alignment: AlignmentDirectional.centerStart,
                  child: FilledButton(onPressed: () => shellTab.value = 0, child: Text(s.findCourt)),
                ),
              ],
              for (final b in data.upcoming) _BookingRow(booking: b),
              if (data.history.isNotEmpty) ...[
                const SizedBox(height: 24),
                Text(s.past, style: text.titleMedium),
                const SizedBox(height: 8),
                for (final b in data.history) _BookingRow(booking: b),
              ],
            ],
          ],
        ),
      ),
    );
  }
}

class _BookingRow extends StatelessWidget {
  const _BookingRow({required this.booking});
  final Booking booking;

  @override
  Widget build(BuildContext context) {
    final s = Strings.of(context);
    final text = Theme.of(context).textTheme;
    final b = booking;
    final status = statusLabel(b, s);
    return Card(
      margin: const EdgeInsets.only(bottom: 8),
      clipBehavior: Clip.antiAlias,
      child: InkWell(
        onTap: () => Navigator.of(context)
            .push(MaterialPageRoute<void>(builder: (_) => BookingDetailPage(booking: b))),
        child: Padding(
          padding: const EdgeInsets.all(16),
          child: Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(b.venueName(s.isArabic), style: text.titleMedium),
                    const SizedBox(height: 2),
                    Text('${formatDay(dayOf(b.startsAt), s)} · ${timeRange(b.startsAt, b.endsAt)}', style: text.bodyLarge),
                    Text(b.courtName(s.isArabic), style: text.bodySmall),
                    if (status != null || b.isPlayer || b.match != null) ...[
                      const SizedBox(height: 8),
                      Wrap(
                        spacing: 6,
                        runSpacing: 6,
                        children: [
                          if (b.isPlayer || b.match != null) StatusBadge(label: s.matchPill, highlight: true),
                          if (status != null) StatusBadge(label: status),
                        ],
                      ),
                    ],
                  ],
                ),
              ),
              const SizedBox(width: 12),
              Column(
                crossAxisAlignment: CrossAxisAlignment.end,
                children: [
                  Text(money(b.toPay, b.currency, s), style: text.labelLarge),
                  if (b.pointsDiscount > 0)
                    Text(s.withPoints(money(b.pointsDiscount, b.currency, s)), style: text.bodySmall),
                ],
              ),
            ],
          ),
        ),
      ),
    );
  }
}

/// A status in words on a quiet grey pill; `highlight` puts it on lime (the "Match" pill).
class StatusBadge extends StatelessWidget {
  const StatusBadge({super.key, required this.label, this.highlight = false});
  final String label;
  final bool highlight;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
      decoration: BoxDecoration(
          color: highlight ? MidangoColors.lime : MidangoColors.disabledBg, borderRadius: BorderRadius.circular(6)),
      child: Text(label,
          style: Theme.of(context)
              .textTheme
              .bodySmall
              ?.copyWith(color: highlight ? MidangoColors.navy : MidangoColors.slate)),
    );
  }
}
