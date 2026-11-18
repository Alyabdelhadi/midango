import 'package:flutter/material.dart';

import '../api/models.dart';
import '../booking/confirm_sheet.dart';
import '../home_shell.dart';
import '../l10n/strings.dart';
import '../session/session.dart';
import '../theme/midango_colors.dart';
import '../venue/venue_page.dart';
import '../widgets/error_retry.dart';
import 'pickers.dart';
import 'venue_card.dart';

/// Pick a sport, a day and a length; see venues with their open times. No account needed.
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

  @override
  void initState() {
    super.initState();
    bookingsChanged.addListener(_loadVenues);
    _loadCategories();
  }

  @override
  void dispose() {
    bookingsChanged.removeListener(_loadVenues);
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
    });
    _loadVenues();
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
        onRefresh: categories == null ? _loadCategories : _loadVenues,
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
              SportPicker(categories: categories, selectedId: _sport?.id, onSelected: _selectSport),
              const SizedBox(height: 4),
              DayPicker(
                selected: _day,
                onSelected: (day) {
                  setState(() => _day = day);
                  _loadVenues();
                },
              ),
              const SizedBox(height: 4),
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
        ),
      ),
    );
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
