import 'package:flutter/material.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';

import '../api/api_client.dart';
import '../api/models.dart';
import '../booking/confirm_sheet.dart';
import '../booking/sign_in_flow.dart';
import '../bookings/booking_detail_page.dart';
import '../home_shell.dart';
import '../l10n/format.dart';
import '../l10n/strings.dart';
import '../push/push.dart';
import '../session/session.dart';
import '../theme/midango_colors.dart';
import '../widgets/busy_button.dart';
import '../widgets/error_retry.dart';
import '../widgets/media_image.dart';
import 'match_widgets.dart';

/// One open match: where and when, who's in, the share, and Join or Leave.
class MatchPage extends StatefulWidget {
  const MatchPage({super.key, required this.matchId, this.initial});

  final String matchId;

  /// The list's copy, shown while the details load.
  final OpenMatch? initial;

  @override
  State<MatchPage> createState() => _MatchPageState();
}

class _MatchPageState extends State<MatchPage> {
  late OpenMatch? _match = widget.initial;
  bool _failed = false;

  /// This match among the customer's own bookings: organiser or joined player. Null = not in it.
  Booking? _mine;
  bool _busy = false;
  String? _error;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    final session = SessionScope.read(context);
    setState(() => _failed = false);
    try {
      final json = await session.api.get('/matches/${widget.matchId}') as JsonMap;
      if (!mounted) return;
      setState(() => _match = OpenMatch.fromJson(json));
    } catch (_) {
      if (mounted && _match == null) setState(() => _failed = true);
    }
    await _loadMine();
    if (mounted) await session.ensureLevels();
  }

  Future<void> _loadMine() async {
    final session = SessionScope.read(context);
    if (!session.signedIn) return;
    try {
      final data = MyBookings.fromJson(await session.api.get('/me/bookings', auth: true) as JsonMap);
      if (mounted) setState(() => _mine = data.upcoming.where((b) => b.id == widget.matchId).firstOrNull);
    } catch (_) {
      // Offline: Join still works; the server refuses a second join.
    }
  }

  /// The customer's level for this sport differs from the match's.
  bool get _mismatch {
    final m = _match;
    final level = m?.level;
    final mine = SessionScope.read(context).levels?[m?.categoryId];
    return level != null && mine != null && mine != level;
  }

  Future<void> _join() async {
    final s = Strings.of(context);
    final session = SessionScope.read(context);
    final messenger = ScaffoldMessenger.of(context);
    final warned = _mismatch; // the warning was on screen, so this tap is "Join anyway"
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      if (!await ensureSignedIn(context)) {
        if (mounted) setState(() => _busy = false);
        return;
      }
      await session.ensureLevels();
      await _loadMine();
      if (!mounted) return;
      if (_mine != null || (_mismatch && !warned)) {
        setState(() => _busy = false); // already in it, or now show the level line first
        return;
      }
      final json = await session.api.post('/matches/${widget.matchId}/join', null, true);
      bookingsChanged.value++;
      if (!mounted) return;
      setState(() {
        _match = OpenMatch.fromJson(json as JsonMap);
        _busy = false;
      });
      messenger.showSnackBar(SnackBar(content: Text(s.joined)));
      await _loadMine();
      push.askPermissionOnce(); // reminders and match news
    } catch (e) {
      if (mounted) {
        setState(() {
          _busy = false;
          _error = e is ApiException ? e.title : s.somethingWrong;
        });
      }
    }
  }

  Future<void> _leave() async {
    final s = Strings.of(context);
    final messenger = ScaffoldMessenger.of(context);
    final mine = _mine;
    if (mine == null || !await confirmLeave(context, mine) || !mounted) return;
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      final json = await SessionScope.read(context).api.post('/matches/${widget.matchId}/leave', null, true);
      bookingsChanged.value++;
      if (!mounted) return;
      setState(() {
        _match = OpenMatch.fromJson(json as JsonMap);
        _mine = null;
        _busy = false;
      });
      messenger.showSnackBar(SnackBar(content: Text(s.leftMatch)));
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
    SessionScope.of(context); // rebuild when levels or sign-in change
    final m = _match;
    return Scaffold(
      appBar: AppBar(title: Text(s.matchTitle)),
      body: _failed
          ? Center(child: ErrorRetry(onRetry: _load))
          : m == null
              ? const Center(child: CircularProgressIndicator())
              : RefreshIndicator(onRefresh: _load, child: _body(m, s)),
    );
  }

  Widget _body(OpenMatch m, Strings s) {
    final text = Theme.of(context).textTheme;
    final logo = m.venueLogoUrl;
    final address = m.address(s.isArabic);
    final until = m.freeCancellationUntil;
    final mine = _mine;
    return ListView(
      padding: const EdgeInsets.all(24),
      children: [
        Row(
          children: [
            if (logo != null) ...[
              ClipRRect(borderRadius: BorderRadius.circular(8), child: MediaImage(logo, width: 48, height: 48)),
              const SizedBox(width: 12),
            ],
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(m.venueName(s.isArabic), style: text.titleLarge),
                  if (address != null && address.isNotEmpty) Text(address, style: text.bodySmall),
                ],
              ),
            ),
          ],
        ),
        const SizedBox(height: 12),
        Row(
          children: [
            LevelBadge(level: m.level),
            const SizedBox(width: 8),
            Text(s.spotsLeft(m.spotsLeft), style: text.bodySmall),
          ],
        ),
        const SizedBox(height: 8),
        InfoLine(
          icon: LucideIcons.calendar,
          text: '${formatDay(dayOf(m.startsAt), s)} · ${timeRange(m.startsAt, m.endsAt)} · ${s.minutes(m.minutes)}',
        ),
        InfoLine(icon: LucideIcons.layoutGrid, text: m.courtName(s.isArabic)),
        InfoLine(icon: LucideIcons.banknote, text: s.sharePayAtDesk(money(m.share, m.currency, s))),
        if (until != null)
          InfoLine(
            icon: LucideIcons.calendarX,
            text: s.freeLeaveUntil('${formatDay(dayOf(until), s)} ${ltr(clock(until))}'),
          ),
        const Divider(height: 32, color: MidangoColors.line),
        Text(s.playersHeading, style: text.titleMedium),
        const SizedBox(height: 8),
        for (final (i, p) in m.players.indexed)
          PlayerRow(firstName: p.firstName, note: i == 0 ? s.organiser : null), // the API lists the organiser first
        const SizedBox(height: 24),
        if (_error != null) ...[
          Text(_error!, style: text.bodyLarge?.copyWith(color: MidangoColors.error)),
          const SizedBox(height: 12),
        ],
        if (mine != null && !mine.isPlayer) ...[
          Text(s.yourMatch, style: text.bodyLarge),
          const SizedBox(height: 12),
          OutlinedButton(
            onPressed: () => Navigator.of(context)
                .push(MaterialPageRoute<void>(builder: (_) => BookingDetailPage(booking: mine))),
            child: Text(s.viewBooking),
          ),
        ] else if (mine != null)
          OutlinedButton(
            onPressed: _busy ? null : _leave,
            child: _busy
                ? const SizedBox(width: 20, height: 20, child: CircularProgressIndicator(strokeWidth: 2))
                : Text(s.leaveMatch),
          )
        else if (m.spotsLeft <= 0)
          FilledButton(onPressed: null, child: Text(s.matchFull))
        else ...[
          if (_mismatch) ...[
            InfoLine(
              icon: LucideIcons.triangleAlert,
              text: s.levelMismatch(s.levelName(m.level!)),
              style: text.bodyLarge?.copyWith(color: MidangoColors.warning),
            ),
            const SizedBox(height: 8),
          ],
          BusyButton(label: _mismatch ? s.joinAnyway : s.joinMatch, busy: _busy, onPressed: _join),
        ],
      ],
    );
  }
}

/// Asks before leaving a joined match; warns when it's past the free window.
Future<bool> confirmLeave(BuildContext context, Booking booking) async {
  final s = Strings.of(context);
  final yes = await showDialog<bool>(
    context: context,
    builder: (context) => AlertDialog(
      title: Text(s.leaveTitle),
      content: Text(booking.freeToCancel ? s.leaveFree : s.leaveLate),
      actions: [
        TextButton(onPressed: () => Navigator.pop(context, false), child: Text(s.stayIn)),
        FilledButton(
          style: FilledButton.styleFrom(backgroundColor: MidangoColors.error, foregroundColor: MidangoColors.white),
          onPressed: () => Navigator.pop(context, true),
          child: Text(s.leaveMatch),
        ),
      ],
    ),
  );
  return yes == true;
}

/// An avatar, a first name and an optional note ("Organiser", "You"), with an optional action at the end.
class PlayerRow extends StatelessWidget {
  const PlayerRow({super.key, required this.firstName, this.note, this.action});
  final String firstName;
  final String? note;
  final Widget? action;

  @override
  Widget build(BuildContext context) {
    final text = Theme.of(context).textTheme;
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 4),
      child: Row(
        children: [
          PlayerAvatar(firstName: firstName),
          const SizedBox(width: 12),
          Expanded(
            child: Text.rich(TextSpan(children: [
              TextSpan(text: firstName, style: text.bodyLarge),
              if (note != null) TextSpan(text: ' · $note', style: text.bodySmall),
            ])),
          ),
          ?action,
        ],
      ),
    );
  }
}
