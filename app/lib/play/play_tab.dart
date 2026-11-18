import 'package:flutter/material.dart';

import '../api/models.dart';
import '../booking/confirm_sheet.dart';
import '../home_shell.dart';
import '../l10n/strings.dart';
import '../matches/match_page.dart';
import '../matches/match_widgets.dart';
import '../session/session.dart';
import '../theme/midango_colors.dart';
import '../tournaments/tournament_page.dart';
import '../tournaments/tournament_widgets.dart';
import '../venue/venue_page.dart';
import '../widgets/error_retry.dart';
import 'pickers.dart';
import 'venue_card.dart';

/// Pick a sport, a day and a length; see venues with their open times. Or switch to open matches others
/// started, filtered by level; or tournaments for the sport. No account needed.
class PlayTab extends StatefulWidget {
  const PlayTab({super.key});

  @override
  State<PlayTab> createState() => _PlayTabState();
}

class _PlayTabState extends State<PlayTab> {
  List<Category>? _categories;
  bool _categoriesFailed = false;
  Category? _sport;
  String _day = bookableDays().first;
  int _minutes = 0;

  List<VenueCard>? _venues;
  bool _venuesFailed = false;
  int _request = 0; // only the newest answer is shown when the customer taps quickly

  _Mode _mode = _Mode.courts;
  String? _level; // filter; starts at the customer's own level for the sport
  List<OpenMatch>? _matches;
  bool _matchesFailed = false;

  List<Tournament>? _tournaments;
  bool _tournamentsFailed = false;

  @override
  void initState() {
    super.initState();
    bookingsChanged.addListener(_reload);
    _loadCategories();
  }

  @override
  void dispose() {
    bookingsChanged.removeListener(_reload);
    super.dispose();
  }

  Future<void> _loadCategories() async {
    if (_categoriesFailed) setState(() => _categoriesFailed = false); // not from initState: no setState there
    try {
      final json = await SessionScope.read(context).api.get('/categories') as List;
      final categories = json.map((j) => Category.fromJson(j as JsonMap)).toList();
      if (!mounted) return;
      setState(() => _categories = categories);
      if (categories.isNotEmpty) _selectSport(categories.first);
    } catch (_) {
      if (mounted) setState(() => _categoriesFailed = true);
    }
  }

  void _selectSport(Category sport) {
    setState(() {
      _sport = sport;
      if (!sport.durationsMinutes.contains(_minutes)) _minutes = sport.durationsMinutes.first;
      _level = SessionScope.read(context).levels?[sport.id];
    });
    _reload();
  }

  Future<void> _reload() => switch (_mode) {
        _Mode.courts => _loadVenues(),
        _Mode.matches => _loadMatches(),
        _Mode.tournaments => _loadTournaments(),
      };

  Future<void> _setMode(_Mode mode) async {
    setState(() => _mode = mode);
    if (mode == _Mode.matches) {
      final levels = await SessionScope.read(context).ensureLevels();
      if (!mounted) return;
      setState(() => _level = levels[_sport?.id]);
    }
    _reload();
  }

  Future<void> _loadMatches() async {
    final sport = _sport;
    if (sport == null) return;
    final request = ++_request;
    setState(() {
      _matches = null;
      _matchesFailed = false;
    });
    try {
      final json = await SessionScope.read(context).api.get('/matches',
          query: {'categoryId': sport.id, 'date': _day, 'level': ?_level}) as List;
      if (!mounted || request != _request) return;
      setState(() => _matches = json.map((j) => OpenMatch.fromJson(j as JsonMap)).toList());
    } catch (_) {
      if (mounted && request == _request) setState(() => _matchesFailed = true);
    }
  }

  void _openMatch(OpenMatch match) {
    Navigator.of(context).push(MaterialPageRoute<void>(
      builder: (_) => MatchPage(matchId: match.id, initial: match),
    ));
  }

  Future<void> _loadTournaments() async {
    final sport = _sport;
    if (sport == null) return;
    final request = ++_request;
    setState(() {
      _tournaments = null;
      _tournamentsFailed = false;
    });
    try {
      final json = await SessionScope.read(context).api.get('/tournaments', query: {'categoryId': sport.id}) as List;
      if (!mounted || request != _request) return;
      setState(() => _tournaments = json.map((j) => Tournament.fromJson(j as JsonMap)).toList());
    } catch (_) {
      if (mounted && request == _request) setState(() => _tournamentsFailed = true);
    }
  }

  void _openTournament(Tournament t) {
    Navigator.of(context).push(MaterialPageRoute<void>(
      builder: (_) => TournamentPage(tournamentId: t.id, initial: t),
    ));
  }

  Future<void> _loadVenues() async {
    final sport = _sport;
    if (sport == null) return;
    final request = ++_request;
    setState(() {
      _venues = null;
      _venuesFailed = false;
    });
    try {
      final json = await SessionScope.read(context).api.get('/venues',
          query: {'categoryId': sport.id, 'date': _day, 'minutes': '$_minutes'}) as List;
      if (!mounted || request != _request) return;
      setState(() => _venues = json.map((j) => VenueCard.fromJson(j as JsonMap)).toList());
    } catch (_) {
      if (mounted && request == _request) setState(() => _venuesFailed = true);
    }
  }

  void _openVenue(VenueCard venue) {
    Navigator.of(context).push(MaterialPageRoute<void>(
      builder: (_) => VenuePage(venueId: venue.id, categoryId: _sport!.id, date: _day, minutes: _minutes),
    ));
  }

  @override
  Widget build(BuildContext context) {
    final s = Strings.of(context);
    final text = Theme.of(context).textTheme;
    final categories = _categories;

    return SafeArea(
      child: RefreshIndicator(
        onRefresh: categories == null ? _loadCategories : _reload,
        child: ListView(
          padding: const EdgeInsets.only(bottom: 24),
          children: [
            Padding(
              padding: const EdgeInsets.fromLTRB(16, 16, 16, 8),
              child: Text(s.playTitle, style: text.titleLarge),
            ),
            if (_categoriesFailed)
              ErrorRetry(onRetry: _loadCategories)
            else if (categories == null)
              const Padding(padding: EdgeInsets.all(32), child: Center(child: CircularProgressIndicator()))
            else if (categories.isEmpty)
              Padding(padding: const EdgeInsets.all(16), child: Text(s.noSports, style: text.bodyLarge))
            else ...[
              Padding(
                padding: const EdgeInsets.fromLTRB(16, 0, 16, 8),
                child: SegmentedButton<_Mode>(
                  segments: [
                    ButtonSegment(value: _Mode.courts, label: Text(s.courtsHeading)),
                    ButtonSegment(value: _Mode.matches, label: Text(s.openMatches)),
                    ButtonSegment(value: _Mode.tournaments, label: Text(s.tournaments)),
                  ],
                  selected: {_mode},
                  showSelectedIcon: false,
                  onSelectionChanged: (choice) => _setMode(choice.first),
                  style: SegmentedButton.styleFrom(
                    selectedBackgroundColor: MidangoColors.lime,
                    selectedForegroundColor: MidangoColors.navy,
                    foregroundColor: MidangoColors.navy,
                    side: const BorderSide(color: MidangoColors.line),
                  ),
                ),
              ),
              SportPicker(categories: categories, selectedId: _sport?.id, onSelected: _selectSport),
              const SizedBox(height: 4),
              if (_mode == _Mode.tournaments) ...[
                const SizedBox(height: 8),
                ..._tournamentList(s, text),
              ] else ...[
              DayPicker(
                selected: _day,
                onSelected: (day) {
                  setState(() => _day = day);
                  _reload();
                },
              ),
              const SizedBox(height: 4),
              if (_mode == _Mode.matches) ...[
                LevelPicker(
                  selected: _level,
                  onSelected: (level) {
                    setState(() => _level = level);
                    _loadMatches();
                  },
                ),
                const SizedBox(height: 12),
                ..._matchList(s, text),
              ] else ...[
              LengthPicker(
                durations: _sport!.durationsMinutes,
                selected: _minutes,
                onSelected: (m) {
                  setState(() => _minutes = m);
                  _loadVenues();
                },
              ),
              const SizedBox(height: 12),
              ..._venueList(s, text),
              ],
              ],
            ],
          ],
        ),
      ),
    );
  }

  List<Widget> _matchList(Strings s, TextTheme text) {
    Widget padded(Widget child) => Padding(padding: const EdgeInsets.fromLTRB(16, 0, 16, 12), child: child);
    final matches = _matches;
    if (_matchesFailed) return [ErrorRetry(onRetry: _loadMatches)];
    if (matches == null) return [for (var i = 0; i < 3; i++) padded(const _PlaceholderCard())];
    if (matches.isEmpty) return [padded(Text(s.noMatches, style: text.bodyLarge))];
    return [for (final m in matches) padded(MatchCardView(match: m, onOpen: () => _openMatch(m)))];
  }

  List<Widget> _tournamentList(Strings s, TextTheme text) {
    Widget padded(Widget child) => Padding(padding: const EdgeInsets.fromLTRB(16, 0, 16, 12), child: child);
    final tournaments = _tournaments;
    if (_tournamentsFailed) return [ErrorRetry(onRetry: _loadTournaments)];
    if (tournaments == null) return [for (var i = 0; i < 3; i++) padded(const _PlaceholderCard())];
    if (tournaments.isEmpty) return [padded(Text(s.noTournaments, style: text.bodyLarge))];
    return [for (final t in tournaments) padded(TournamentCardView(tournament: t, onOpen: () => _openTournament(t)))];
  }

  List<Widget> _venueList(Strings s, TextTheme text) {
    Widget padded(Widget child) => Padding(padding: const EdgeInsets.fromLTRB(16, 0, 16, 12), child: child);
    final venues = _venues;
    if (_venuesFailed) return [ErrorRetry(onRetry: _loadVenues)];
    if (venues == null) return [for (var i = 0; i < 3; i++) padded(const _PlaceholderCard())];
    if (venues.isEmpty) return [padded(Text(s.noVenues, style: text.bodyLarge))];
    return [
      for (final venue in venues)
        padded(VenueCardView(
          venue: venue,
          onOpen: () => _openVenue(venue),
          onTime: (slot) => showConfirmSheet(context, venueId: venue.id, slot: slot, minutes: _minutes),
        )),
    ];
  }
}

enum _Mode { courts, matches, tournaments }

/// A grey card shape while venues load.
class _PlaceholderCard extends StatelessWidget {
  const _PlaceholderCard();

  @override
  Widget build(BuildContext context) {
    Widget bar(double width, double height) => Container(
          width: width,
          height: height,
          decoration: BoxDecoration(color: MidangoColors.disabledBg, borderRadius: BorderRadius.circular(6)),
        );
    return Card(
      margin: EdgeInsets.zero,
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            bar(180, 20),
            const SizedBox(height: 8),
            bar(120, 12),
            const SizedBox(height: 16),
            Row(children: [bar(64, 44), const SizedBox(width: 8), bar(64, 44), const SizedBox(width: 8), bar(64, 44)]),
          ],
        ),
      ),
    );
  }
}
