import 'package:flutter/material.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';

import '../api/api_client.dart';
import '../api/models.dart';
import '../booking/confirmation_page.dart';
import '../home_shell.dart';
import '../l10n/format.dart';
import '../l10n/strings.dart';
import '../matches/match_page.dart';
import '../matches/match_widgets.dart';
import '../session/session.dart';
import '../theme/midango_colors.dart';
import '../widgets/busy_button.dart';
import 'bookings_tab.dart';

/// One booking: the QR code while it's ahead, its details and status, and cancelling.
/// The organiser of an upcoming booking can open it to other players and manage them; a player can leave.
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

  /// The sport's players-per-match setting; 0 until the venue loads (bookings don't carry the sport).
  int _cap = 0;
  bool _openOn = false;
  late int _players = _booking.match?.maxPlayers ?? 0;
  late String? _level = _booking.match?.level;

  bool get _organiser => !_booking.isPlayer && _booking.cancellable;

  /// The customer's points and redeem rules; null until loaded or when points can't be used here.
  Points? _points;
  late int _redeem = _booking.pointsRedeemed;

  /// Points can go on a live booking that's still ahead, by the organiser or a player still in it.
  /// The booking doesn't say whether it's checked in; the API refuses then.
  bool get _canRedeem =>
      _booking.upcoming && _booking.playerStatus != 'left' && _booking.playerStatus != 'removed';

  @override
  void initState() {
    super.initState();
    if (_organiser) _loadCap();
    if (_booking.match != null) _refresh();
    if (_canRedeem) _loadPoints();
  }

  Future<void> _loadPoints() async {
    try {
      final points = Points.fromJson(await SessionScope.read(context).api.get('/me/points', auth: true) as JsonMap);
      if (mounted) setState(() => _points = points);
    } catch (_) {
      // Offline: the points control stays hidden.
    }
  }

  Future<void> _savePoints(int points) async {
    final s = Strings.of(context);
    await _run((api) => api.post('/bookings/${_booking.id}/points', {'points': points}, true),
        points == 0 ? s.pointsRemoved : s.pointsApplied);
    if (mounted) _loadPoints();
  }

  /// The list's copy can be stale for a match: players join and leave while it's on screen.
  Future<void> _refresh() async {
    try {
      final all = MyBookings.fromJson(
          await SessionScope.read(context).api.get('/me/bookings', auth: true) as JsonMap);
      final fresh = all.upcoming.where((b) => b.id == _booking.id).firstOrNull;
      if (fresh == null || !mounted) return;
      setState(() {
        _booking = fresh;
        _redeem = fresh.pointsRedeemed;
        _players = fresh.match?.maxPlayers ?? _players;
        _level = fresh.match?.level;
      });
    } catch (_) {
      // Offline: keep showing the list's copy.
    }
  }

  Future<void> _loadCap() async {
    try {
      final venue = VenueDetail.fromJson(
          await SessionScope.read(context).api.get('/venues/${_booking.venueId}') as JsonMap);
      final categoryId = venue.court(_booking.courtId)?.categoryId;
      final cap = venue.categories.where((c) => c.id == categoryId).firstOrNull?.playersPerMatch ?? 0;
      if (!mounted) return;
      setState(() {
        _cap = cap;
        if (_booking.match == null) _players = cap;
      });
    } catch (_) {
      // Offline: an open match can still be edited within its current size.
    }
  }

  /// Runs one call that answers with the booking, then says `done`.
  Future<void> _run(Future<dynamic> Function(ApiClient api) call, String done) async {
    final s = Strings.of(context);
    final messenger = ScaffoldMessenger.of(context);
    final api = SessionScope.read(context).api;
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      final booking = Booking.fromJson(await call(api) as JsonMap);
      bookingsChanged.value++;
      if (!mounted) return;
      setState(() {
        _booking = booking;
        _redeem = booking.pointsRedeemed;
        _busy = false;
        _openOn = false;
        _players = booking.match?.maxPlayers ?? _cap;
        _level = booking.match?.level;
      });
      messenger.showSnackBar(SnackBar(content: Text(done)));
    } catch (e) {
      if (mounted) {
        setState(() {
          _busy = false;
          _error = e is ApiException ? e.title : s.somethingWrong;
        });
      }
    }
  }

  Future<bool> _confirm(String title, String? body, String no, String yes) async =>
      await showDialog<bool>(
        context: context,
        builder: (context) => AlertDialog(
          title: Text(title),
          content: body == null ? null : Text(body),
          actions: [
            TextButton(onPressed: () => Navigator.pop(context, false), child: Text(no)),
            FilledButton(
              style:
                  FilledButton.styleFrom(backgroundColor: MidangoColors.error, foregroundColor: MidangoColors.white),
              onPressed: () => Navigator.pop(context, true),
              child: Text(yes),
            ),
          ],
        ),
      ) ==
      true;

  Future<void> _cancel() async {
    final s = Strings.of(context);
    if (!await _confirm(s.cancelTitle, _booking.freeToCancel ? s.cancelFree : s.cancelLate, s.keepBooking,
            s.cancelBooking) ||
        !mounted) {
      return;
    }
    await _run((api) => api.post('/bookings/${_booking.id}/cancel', null, true), s.bookingCancelled);
  }

  void _open() => _run(
      (api) => api.post('/bookings/${_booking.id}/open', {'maxPlayers': _players, 'level': _level}, true),
      Strings.of(context).matchOpened);

  void _close() =>
      _run((api) => api.post('/bookings/${_booking.id}/close', null, true), Strings.of(context).matchClosed);

  Future<void> _remove(MatchPlayer player) async {
    final s = Strings.of(context);
    if (!await _confirm(s.removeTitle(player.firstName), null, s.notNow, s.remove) || !mounted) return;
    await _run((api) => api.delete('/bookings/${_booking.id}/players/${player.customerId}'), s.playerRemoved);
  }

  Future<void> _leave() async {
    final s = Strings.of(context);
    final messenger = ScaffoldMessenger.of(context);
    final nav = Navigator.of(context);
    if (!await confirmLeave(context, _booking) || !mounted) return;
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      await SessionScope.read(context).api.post('/matches/${_booking.id}/leave', null, true);
      bookingsChanged.value++;
      messenger.showSnackBar(SnackBar(content: Text(s.leftMatch)));
      nav.pop(); // no longer one of the customer's bookings
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
            child: Wrap(
              spacing: 6,
              children: [
                if (b.isPlayer || b.match != null) StatusBadge(label: s.matchPill, highlight: true),
                StatusBadge(label: statusLabel(b, s) ?? s.statusConfirmed),
              ],
            ),
          ),
          const SizedBox(height: 12),
          BookingSummary(booking: b),
          ..._pointsSection(b, s, text),
          ..._matchSection(b, s, text),
          if (_error != null) ...[
            const SizedBox(height: 12),
            Text(_error!, style: text.bodyLarge?.copyWith(color: MidangoColors.error)),
          ],
          if (b.cancellable) ...[
            const SizedBox(height: 24),
            OutlinedButton(
              onPressed: _busy ? null : (b.isPlayer ? _leave : _cancel),
              child: _busy
                  ? const SizedBox(width: 20, height: 20, child: CircularProgressIndicator(strokeWidth: 2))
                  : Text(b.isPlayer ? s.leaveMatch : s.cancelBooking),
            ),
          ],
        ],
      ),
    );
  }

  List<Widget> _matchSection(Booking b, Strings s, TextTheme text) {
    final match = b.match;
    final meId = SessionScope.of(context).me?.id;
    final cap = _cap > 0 ? _cap : (match?.maxPlayers ?? 0);
    const divider = Divider(height: 32, color: MidangoColors.line);

    // Not open yet: the organiser can open it while it's ahead.
    if (match == null) {
      if (!_organiser || cap < 2) return const [];
      return [
        divider,
        SwitchListTile(
          contentPadding: EdgeInsets.zero,
          title: Text(s.openToOthers, style: text.labelLarge),
          subtitle: Text(s.openToOthersHelp, style: text.bodySmall),
          value: _openOn,
          onChanged: _busy ? null : (on) => setState(() => _openOn = on),
        ),
        if (_openOn) ...[
          _settings(b, min: 2, max: cap),
          const SizedBox(height: 16),
          BusyButton(label: s.openMatch, busy: _busy, onPressed: _open),
        ],
      ];
    }

    // The API lists the organiser first, then the active joiners.
    final joiners = match.players.length - 1;
    final changed = _players != match.maxPlayers || _level != match.level;
    return [
      divider,
      Row(
        children: [
          Expanded(child: Text(s.playersHeading, style: text.titleMedium)),
          LevelBadge(level: match.level),
          const SizedBox(width: 8),
          Text(s.spotsLeft((match.maxPlayers - 1 - joiners).clamp(0, match.maxPlayers)), style: text.bodySmall),
        ],
      ),
      const SizedBox(height: 8),
      for (final (i, p) in match.players.indexed)
        PlayerRow(
          firstName: p.firstName,
          note: switch ((p.customerId == meId, i == 0)) {
            (true, true) => '${s.you} · ${s.organiser}',
            (true, false) => s.you,
            (false, true) => s.organiser,
            _ => null,
          },
          action: _organiser && i > 0 && p.customerId != null
              ? TextButton(onPressed: _busy ? null : () => _remove(p), child: Text(s.remove))
              : null,
        ),
      if (_organiser) ...[
        const SizedBox(height: 16),
        _settings(b, min: (1 + joiners).clamp(2, cap), max: cap),
        const SizedBox(height: 16),
        BusyButton(label: s.saveChanges, busy: _busy, onPressed: changed ? _open : null),
        const SizedBox(height: 12),
        OutlinedButton(onPressed: _busy || joiners > 0 ? null : _close, child: Text(s.closeMatch)),
        if (joiners > 0) ...[
          const SizedBox(height: 4),
          Text(s.closeBlocked, style: text.bodySmall),
        ],
      ],
    ];
  }

  List<Widget> _pointsSection(Booking b, Strings s, TextTheme text) {
    final p = _points;
    if (p == null || !_canRedeem) return const [];
    // Points already on this booking come back before the new amount is taken.
    final byBalance = (p.balance + b.pointsRedeemed) ~/ p.pointsPerStep;
    final byCap = (b.ownAmount * p.maxPercent / 100 / p.sarPerStep + 1e-9).floor();
    final maxSteps = byBalance < byCap ? byBalance : byCap;
    final steps = (_redeem ~/ p.pointsPerStep).clamp(0, maxSteps);
    final off = steps * p.sarPerStep;
    final change = _busy ? null : (int n) => setState(() => _redeem = n * p.pointsPerStep);
    return [
      const Divider(height: 32, color: MidangoColors.line),
      Text(s.usePoints, style: text.titleMedium),
      const SizedBox(height: 4),
      Text(
        '${s.pointsHave(p.balance)} · ${s.usePointsHelp(p.pointsPerStep, money(p.sarPerStep, b.currency, s), plain(p.maxPercent))}',
        style: text.bodySmall,
      ),
      // Nothing to use, and nothing already used to take back.
      if (maxSteps == 0 && b.pointsRedeemed == 0) ...[
        const SizedBox(height: 8),
        Text(s.notEnoughPoints, style: text.bodyLarge),
      ] else ...[
        const SizedBox(height: 12),
        Row(
          children: [
            IconButton.outlined(
              tooltip: s.fewerPoints,
              onPressed: change == null || steps <= 0 ? null : () => change(steps - 1),
              icon: const Icon(LucideIcons.minus, size: 20),
            ),
            Expanded(
              child: Text(s.pointsCount(steps * p.pointsPerStep),
                  textAlign: TextAlign.center, style: text.titleMedium),
            ),
            IconButton.outlined(
              tooltip: s.morePoints,
              onPressed: change == null || steps >= maxSteps ? null : () => change(steps + 1),
              icon: const Icon(LucideIcons.plus, size: 20),
            ),
          ],
        ),
        const SizedBox(height: 8),
        Text(
          s.pointsOffYouPay(money(off, b.currency, s), money(b.ownAmount - off, b.currency, s)),
          textAlign: TextAlign.center,
          style: text.bodyLarge,
        ),
        const SizedBox(height: 12),
        BusyButton(
          label: s.save,
          busy: _busy,
          onPressed: steps * p.pointsPerStep == b.pointsRedeemed ? null : () => _savePoints(steps * p.pointsPerStep),
        ),
      ],
    ];
  }

  Widget _settings(Booking b, {required int min, required int max}) => MatchSettings(
        min: min,
        max: max,
        players: _players.clamp(min, max),
        level: _level,
        total: b.total,
        currency: b.currency,
        onPlayers: _busy ? null : (n) => setState(() => _players = n),
        onLevel: _busy ? null : (l) => setState(() => _level = l),
      );
}
