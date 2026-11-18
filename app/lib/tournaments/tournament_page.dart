import 'package:flutter/material.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';

import '../api/api_client.dart';
import '../api/models.dart';
import '../booking/confirm_sheet.dart';
import '../booking/sign_in_flow.dart';
import '../home_shell.dart';
import '../l10n/strings.dart';
import '../matches/match_page.dart';
import '../matches/match_widgets.dart';
import '../push/push.dart';
import '../session/session.dart';
import '../theme/midango_colors.dart';
import '../widgets/busy_button.dart';
import '../widgets/error_retry.dart';
import '../widgets/media_image.dart';
import 'tournament_widgets.dart';

/// One tournament: what, where and when, the teams in it, and Register a team or the captain's own team.
class TournamentPage extends StatefulWidget {
  const TournamentPage({super.key, required this.tournamentId, this.initial});

  final String tournamentId;

  /// The list's copy, shown while the details load.
  final Tournament? initial;

  @override
  State<TournamentPage> createState() => _TournamentPageState();
}

class _TournamentPageState extends State<TournamentPage> {
  late Tournament? _tournament = widget.initial;
  bool _failed = false;

  /// The customer's team in this tournament, unless withdrawn. Null = none.
  TournamentTeam? _mine;
  bool _busy = false;
  String? _error;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    setState(() => _failed = false);
    try {
      final json = await SessionScope.read(context).api.get('/tournaments/${widget.tournamentId}') as JsonMap;
      if (!mounted) return;
      setState(() => _tournament = Tournament.fromJson(json));
    } catch (_) {
      if (mounted && _tournament == null) setState(() => _failed = true);
    }
    await _loadMine();
  }

  Future<void> _loadMine() async {
    final session = SessionScope.read(context);
    if (!session.signedIn) return;
    try {
      final json = await session.api.get('/me/tournaments', auth: true) as List;
      final mine = json
          .map((j) => MyTournament.fromJson(j as JsonMap))
          .where((m) => m.tournament.id == widget.tournamentId && !m.team.withdrawn)
          .firstOrNull;
      if (mounted) setState(() => _mine = mine?.team);
    } catch (_) {
      // Offline: Register still works; the server refuses a second team.
    }
  }

  /// Applies what register and withdraw answer: the team and the tournament as it is now.
  void _apply(MyTournament result) {
    bookingsChanged.value++;
    setState(() {
      _tournament = result.tournament;
      _mine = result.team.withdrawn ? null : result.team;
      _busy = false;
    });
  }

  Future<void> _register() async {
    final s = Strings.of(context);
    final messenger = ScaffoldMessenger.of(context);
    final nav = Navigator.of(context);
    setState(() {
      _busy = true;
      _error = null;
    });
    if (!await ensureSignedIn(context)) {
      if (mounted) setState(() => _busy = false);
      return;
    }
    await _loadMine();
    final t = _tournament;
    if (!mounted || t == null || _mine != null) {
      if (mounted) setState(() => _busy = false); // already has a team: its card shows now
      return;
    }
    final result = await nav.push<MyTournament>(
      MaterialPageRoute(builder: (_) => RegisterTeamPage(tournament: t)),
    );
    if (!mounted) return;
    if (result == null) {
      setState(() => _busy = false);
      return;
    }
    _apply(result);
    messenger.showSnackBar(SnackBar(content: Text(result.team.waitlisted ? s.teamWaitlisted : s.teamRegistered)));
    push.askPermissionOnce(); // waitlist promotions and tournament news
  }

  Future<void> _withdraw() async {
    final s = Strings.of(context);
    final messenger = ScaffoldMessenger.of(context);
    final team = _mine;
    if (team == null || !await _confirmWithdraw() || !mounted) return;
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      final json = await SessionScope.read(context)
          .api
          .post('/tournaments/${widget.tournamentId}/teams/${team.id}/withdraw', null, true);
      if (!mounted) return;
      _apply(MyTournament.fromJson(json as JsonMap));
      messenger.showSnackBar(SnackBar(content: Text(s.teamWithdrawn)));
    } catch (e) {
      if (mounted) {
        setState(() {
          _busy = false;
          _error = e is ApiException ? e.title : s.somethingWrong;
        });
      }
    }
  }

  Future<bool> _confirmWithdraw() async {
    final s = Strings.of(context);
    final yes = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: Text(s.withdrawTitle),
        content: Text(s.withdrawBody),
        actions: [
          TextButton(onPressed: () => Navigator.pop(context, false), child: Text(s.keepTeam)),
          FilledButton(
            style: FilledButton.styleFrom(backgroundColor: MidangoColors.error, foregroundColor: MidangoColors.white),
            onPressed: () => Navigator.pop(context, true),
            child: Text(s.withdrawTeam),
          ),
        ],
      ),
    );
    return yes == true;
  }

  @override
  Widget build(BuildContext context) {
    final s = Strings.of(context);
    SessionScope.of(context); // rebuild on sign-in
    final t = _tournament;
    return Scaffold(
      appBar: AppBar(title: Text(s.tournamentTitle)),
      body: _failed
          ? Center(child: ErrorRetry(onRetry: _load))
          : t == null
              ? const Center(child: CircularProgressIndicator())
              : RefreshIndicator(onRefresh: _load, child: _body(t, s)),
    );
  }

  Widget _body(Tournament t, Strings s) {
    final text = Theme.of(context).textTheme;
    final logo = t.venueLogoUrl;
    final address = t.address(s.isArabic);
    final description = t.description(s.isArabic);
    final mine = _mine;
    return ListView(
      padding: const EdgeInsets.all(24),
      children: [
        Text(t.name(s.isArabic), style: text.titleLarge),
        const SizedBox(height: 12),
        Row(
          children: [
            if (logo != null) ...[
              ClipRRect(borderRadius: BorderRadius.circular(8), child: MediaImage(logo, width: 40, height: 40)),
              const SizedBox(width: 12),
            ],
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(t.venueName(s.isArabic), style: text.titleMedium),
                  if (address != null && address.isNotEmpty) Text(address, style: text.bodySmall),
                ],
              ),
            ),
          ],
        ),
        const SizedBox(height: 12),
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
        if (description != null && description.isNotEmpty) ...[
          Text(description, style: text.bodyLarge),
          const SizedBox(height: 8),
        ],
        InfoLine(icon: LucideIcons.calendar, text: tournamentWhen(t, s)),
        if (t.courts.isNotEmpty)
          InfoLine(icon: LucideIcons.layoutGrid, text: t.courts.map((c) => c.name(s.isArabic)).join('، ')),
        InfoLine(icon: LucideIcons.trophy, text: '${s.formatName(t.format)}. ${s.formatHelp(t.format)}'),
        InfoLine(icon: LucideIcons.users, text: s.teamSize(t.teamSize)),
        InfoLine(
          icon: LucideIcons.banknote,
          text: t.entryFee == 0 ? s.freeEntry : s.feePerTeam(teamFee(t, s)),
        ),
        InfoLine(icon: LucideIcons.calendarClock, text: registrationLine(t, s)),
        const SizedBox(height: 16),
        if (mine != null) ...[
          _MyTeamCard(team: mine),
          const SizedBox(height: 16),
        ],
        if (_error != null) ...[
          Text(_error!, style: text.bodyLarge?.copyWith(color: MidangoColors.error)),
          const SizedBox(height: 12),
        ],
        if (mine != null && !t.started && mine.checkedInAt == null)
          OutlinedButton(
            onPressed: _busy ? null : _withdraw,
            child: _busy
                ? const SizedBox(width: 20, height: 20, child: CircularProgressIndicator(strokeWidth: 2))
                : Text(s.withdrawTeam),
          )
        else if (mine == null && t.registrationOpen)
          BusyButton(label: s.registerTeam, busy: _busy, onPressed: _register)
        else if (mine == null)
          FilledButton(onPressed: null, child: Text(s.registrationClosed)),
        const Divider(height: 40, color: MidangoColors.line),
        Text(s.teamsHeading, style: text.titleMedium),
        const SizedBox(height: 8),
        if (t.teams.isEmpty) Text(s.noTeamsYet, style: text.bodyLarge),
        for (final team in t.teams)
          Padding(
            padding: const EdgeInsets.symmetric(vertical: 6),
            child: Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                PlayerAvatar(firstName: team.name),
                const SizedBox(width: 12),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(team.name, style: text.bodyLarge),
                      if (team.members.isNotEmpty) Text(team.members.join('، '), style: text.bodySmall),
                    ],
                  ),
                ),
              ],
            ),
          ),
      ],
    );
  }
}

/// The captain's team: its status, players and, on the waitlist, what happens next.
class _MyTeamCard extends StatelessWidget {
  const _MyTeamCard({required this.team});
  final TournamentTeam team;

  @override
  Widget build(BuildContext context) {
    final s = Strings.of(context);
    final text = Theme.of(context).textTheme;
    return Card(
      margin: EdgeInsets.zero,
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                Expanded(child: Text(s.yourTeam(team.name), style: text.titleMedium)),
                const SizedBox(width: 8),
                teamStatusBadge(team, s),
              ],
            ),
            if (team.waitlisted) ...[
              const SizedBox(height: 8),
              Text(s.teamWaitlisted, style: text.bodySmall),
            ],
            const SizedBox(height: 8),
            for (final name in team.members) PlayerRow(firstName: name),
          ],
        ),
      ),
    );
  }
}

/// The captain names the team and their teammates; the captain is the signed-in customer.
/// Pops with what the API answered.
class RegisterTeamPage extends StatefulWidget {
  const RegisterTeamPage({super.key, required this.tournament});
  final Tournament tournament;

  @override
  State<RegisterTeamPage> createState() => _RegisterTeamPageState();
}

class _RegisterTeamPageState extends State<RegisterTeamPage> {
  final _form = GlobalKey<FormState>();
  final _name = TextEditingController();
  late final _members = [for (var i = 1; i < widget.tournament.teamSize; i++) TextEditingController()];
  bool _busy = false;
  String? _error;

  @override
  void dispose() {
    _name.dispose();
    for (final c in _members) {
      c.dispose();
    }
    super.dispose();
  }

  Future<void> _submit() async {
    final s = Strings.of(context);
    if (!_form.currentState!.validate()) return;
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      final json = await SessionScope.read(context).api.post(
        '/tournaments/${widget.tournament.id}/teams',
        {
          'name': _name.text.trim(),
          'members': [for (final c in _members) c.text.trim()],
        },
        true,
      );
      if (mounted) Navigator.of(context).pop(MyTournament.fromJson(json as JsonMap));
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
    final me = SessionScope.of(context).me?.name;
    String? required(String? v) => (v ?? '').trim().isEmpty ? s.requiredField : null;
    return Scaffold(
      appBar: AppBar(title: Text(s.registerTeam)),
      body: Form(
        key: _form,
        child: ListView(
          padding: const EdgeInsets.all(24),
          children: [
            Text(widget.tournament.name(s.isArabic), style: text.titleMedium),
            const SizedBox(height: 16),
            TextFormField(
              controller: _name,
              maxLength: 40,
              textInputAction: TextInputAction.next,
              textCapitalization: TextCapitalization.words,
              decoration: InputDecoration(labelText: s.teamNameLabel),
              validator: required,
            ),
            const SizedBox(height: 8),
            Text(s.playersHeading, style: text.titleMedium),
            const SizedBox(height: 4),
            PlayerRow(firstName: me ?? s.you, note: me == null ? s.captain : '${s.you} · ${s.captain}'),
            if (_members.isNotEmpty) ...[
              const SizedBox(height: 8),
              Text(s.teammatesHelp, style: text.bodySmall),
              const SizedBox(height: 12),
              for (final (i, c) in _members.indexed)
                TextFormField(
                  controller: c,
                  maxLength: 60,
                  textInputAction: i == _members.length - 1 ? TextInputAction.done : TextInputAction.next,
                  textCapitalization: TextCapitalization.words,
                  decoration: InputDecoration(labelText: s.teammateLabel(i + 1)),
                  validator: required,
                ),
            ],
            const SizedBox(height: 8),
            InfoLine(
              icon: LucideIcons.banknote,
              text: widget.tournament.entryFee == 0
                  ? s.freeEntry
                  : s.feePerTeam(teamFee(widget.tournament, s)),
            ),
            const SizedBox(height: 16),
            if (_error != null) ...[
              Text(_error!, style: text.bodyLarge?.copyWith(color: MidangoColors.error)),
              const SizedBox(height: 12),
            ],
            BusyButton(label: s.register, busy: _busy, onPressed: _submit),
          ],
        ),
      ),
    );
  }
}
