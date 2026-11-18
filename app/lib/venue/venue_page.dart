import 'package:flutter/material.dart';

import '../api/models.dart';
import '../booking/confirm_sheet.dart';
import '../home_shell.dart';
import '../l10n/strings.dart';
import '../play/pickers.dart';
import '../session/session.dart';
import '../theme/midango_colors.dart';
import '../widgets/error_retry.dart';
import '../widgets/media_image.dart';
import '../widgets/slot_chip.dart';

/// One venue: its sports, every start time of the chosen day (full ones struck through), courts and hours.
class VenuePage extends StatefulWidget {
  const VenuePage({super.key, required this.venueId, required this.categoryId, required this.date, required this.minutes});

  final String venueId;
  final String categoryId;
  final String date;
  final int minutes;

  @override
  State<VenuePage> createState() => _VenuePageState();
}

class _VenuePageState extends State<VenuePage> {
  VenueDetail? _venue;
  bool _venueFailed = false;
  Category? _sport;
  late String _day = widget.date;
  late int _minutes = widget.minutes;

  Availability? _times;
  bool _timesFailed = false;
  int _request = 0;

  @override
  void initState() {
    super.initState();
    bookingsChanged.addListener(_loadTimes);
    _loadVenue();
  }

  @override
  void dispose() {
    bookingsChanged.removeListener(_loadTimes);
    super.dispose();
  }

  Future<void> _loadVenue() async {
    if (_venueFailed) setState(() => _venueFailed = false); // not from initState: no setState there
    try {
      final venue = VenueDetail.fromJson(
          await SessionScope.read(context).api.get('/venues/${widget.venueId}') as JsonMap);
      if (!mounted) return;
      setState(() => _venue = venue);
      if (venue.categories.isNotEmpty) {
        _selectSport(venue.categories.firstWhere((c) => c.id == widget.categoryId, orElse: () => venue.categories.first));
      }
    } catch (_) {
      if (mounted) setState(() => _venueFailed = true);
    }
  }

  void _selectSport(Category sport) {
    setState(() {
      _sport = sport;
      if (!sport.durationsMinutes.contains(_minutes)) _minutes = sport.durationsMinutes.first;
    });
    _loadTimes();
  }

  Future<void> _loadTimes() async {
    final sport = _sport;
    if (sport == null) return;
    final request = ++_request;
    setState(() {
      _times = null;
      _timesFailed = false;
    });
    try {
      final json = await SessionScope.read(context).api.get('/venues/${widget.venueId}/availability',
          query: {'date': _day, 'categoryId': sport.id, 'minutes': '$_minutes'}) as JsonMap;
      if (!mounted || request != _request) return;
      setState(() => _times = Availability.fromJson(json));
    } catch (_) {
      if (mounted && request == _request) setState(() => _timesFailed = true);
    }
  }

  void _book(Slot slot) => showConfirmSheet(context, venueId: widget.venueId, venue: _venue, slot: slot, minutes: _minutes);

  @override
  Widget build(BuildContext context) {
    final s = Strings.of(context);
    final venue = _venue;
    return Scaffold(
      appBar: AppBar(title: Text(venue?.name(s.isArabic) ?? '')),
      body: _venueFailed
          ? Center(child: ErrorRetry(onRetry: _loadVenue))
          : venue == null
              ? const Center(child: CircularProgressIndicator())
              : RefreshIndicator(onRefresh: _loadTimes, child: _body(venue, s)),
    );
  }

  Widget _body(VenueDetail venue, Strings s) {
    final text = Theme.of(context).textTheme;
    final sport = _sport;
    final address = venue.address(s.isArabic);
    final logo = venue.logoUrl;
    final heading = Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(venue.name(s.isArabic), style: text.titleLarge),
        if (address != null && address.isNotEmpty) ...[
          const SizedBox(height: 4),
          Text(address, style: text.bodySmall),
        ],
      ],
    );
    Widget section(String title) =>
        Padding(padding: const EdgeInsets.fromLTRB(16, 24, 16, 8), child: Text(title, style: text.titleMedium));

    return ListView(
      padding: const EdgeInsets.only(bottom: 32),
      children: [
        if (venue.photos.isNotEmpty) _Gallery(photos: venue.photos),
        Padding(
          padding: const EdgeInsets.fromLTRB(16, 16, 16, 12),
          child: logo == null
              ? heading
              : Row(
                  children: [
                    ClipRRect(borderRadius: BorderRadius.circular(12), child: MediaImage(logo, width: 56, height: 56)),
                    const SizedBox(width: 12),
                    Expanded(child: heading),
                  ],
                ),
        ),
        SportPicker(categories: venue.categories, selectedId: sport?.id, onSelected: _selectSport),
        const SizedBox(height: 4),
        DayPicker(
          selected: _day,
          onSelected: (day) {
            setState(() => _day = day);
            _loadTimes();
          },
        ),
        if (sport != null) ...[
          const SizedBox(height: 4),
          LengthPicker(
            durations: sport.durationsMinutes,
            selected: _minutes,
            onSelected: (m) {
              setState(() => _minutes = m);
              _loadTimes();
            },
          ),
        ],
        section(s.timesHeading),
        Padding(padding: const EdgeInsets.symmetric(horizontal: 16), child: _timesView(s, text)),
        section(s.courtsHeading),
        for (final court in venue.courts.where((c) => c.categoryId == sport?.id)) _CourtRow(court: court),
        section(s.hoursHeading),
        _HoursTable(hours: venue.hours),
      ],
    );
  }

  Widget _timesView(Strings s, TextTheme text) {
    final times = _times;
    if (_timesFailed) return ErrorRetry(onRetry: _loadTimes);
    if (times == null) return const Center(child: CircularProgressIndicator());
    if (!times.open) return Text(s.closedThisDay, style: text.bodyLarge);
    if (times.slots.isEmpty) return Text(s.noOpenTimes, style: text.bodyLarge);
    return Wrap(
      spacing: 8,
      runSpacing: 8,
      children: [
        for (final slot in times.slots)
          SlotChip(
            time: slot.localTime,
            available: slot.free,
            semanticLabel: slot.free ? s.slotOpen(slot.localTime) : s.slotFull(slot.localTime),
            onTap: () => _book(slot),
          ),
      ],
    );
  }
}

class _CourtRow extends StatelessWidget {
  const _CourtRow({required this.court});
  final Court court;

  @override
  Widget build(BuildContext context) {
    final s = Strings.of(context);
    final text = Theme.of(context).textTheme;
    final details = <InlineSpan>[TextSpan(text: court.indoor ? s.indoor : s.outdoor)];
    for (final f in court.features) {
      final icon = f.iconUrl;
      details.add(const TextSpan(text: ' · '));
      if (icon != null) {
        details.add(WidgetSpan(
          alignment: PlaceholderAlignment.middle,
          child: Padding(
            padding: const EdgeInsetsDirectional.only(end: 4),
            child: MediaImage(icon, width: 16, height: 16),
          ),
        ));
      }
      details.add(TextSpan(text: f.name(s.isArabic)));
    }
    return Padding(
      padding: const EdgeInsets.fromLTRB(16, 0, 16, 12),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(court.name(s.isArabic), style: text.labelLarge),
          Text.rich(TextSpan(children: details), style: text.bodySmall),
        ],
      ),
    );
  }
}

/// The venue's photos, swiped sideways (mirrored in Arabic), with a dot per photo.
class _Gallery extends StatefulWidget {
  const _Gallery({required this.photos});
  final List<String> photos;

  @override
  State<_Gallery> createState() => _GalleryState();
}

class _GalleryState extends State<_Gallery> {
  int _page = 0;

  @override
  Widget build(BuildContext context) {
    final photos = widget.photos;
    return AspectRatio(
      aspectRatio: 16 / 10,
      child: Stack(
        children: [
          PageView.builder(
            itemCount: photos.length,
            onPageChanged: (i) => setState(() => _page = i),
            itemBuilder: (_, i) => MediaImage(photos[i]),
          ),
          if (photos.length > 1)
            Positioned(
              left: 0,
              right: 0,
              bottom: 12,
              child: ExcludeSemantics(
                child: Row(
                  mainAxisAlignment: MainAxisAlignment.center,
                  children: [
                    for (var i = 0; i < photos.length; i++)
                      AnimatedContainer(
                        duration: const Duration(milliseconds: 200),
                        margin: const EdgeInsets.symmetric(horizontal: 3),
                        width: i == _page ? 16 : 6,
                        height: 6,
                        decoration: BoxDecoration(
                          color: i == _page ? MidangoColors.lime : MidangoColors.white.withValues(alpha: 0.7),
                          borderRadius: BorderRadius.circular(3),
                          boxShadow: const [BoxShadow(color: Color(0x40071329), blurRadius: 2)],
                        ),
                      ),
                  ],
                ),
              ),
            ),
        ],
      ),
    );
  }
}

/// Seven rows, Monday first; a day without hours says "Closed".
class _HoursTable extends StatelessWidget {
  const _HoursTable({required this.hours});
  final List<Hours> hours;

  static const _days = ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday', 'Sunday'];

  @override
  Widget build(BuildContext context) {
    final s = Strings.of(context);
    final text = Theme.of(context).textTheme;
    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 16),
      child: Column(
        children: [
          for (var i = 0; i < 7; i++)
            Padding(
              padding: const EdgeInsets.symmetric(vertical: 4),
              child: Row(
                children: [
                  Expanded(child: Text(s.weekdays[i], style: text.bodyLarge)),
                  Text(
                    _rangeOf(_days[i]) ?? s.closed,
                    style: text.bodyLarge?.copyWith(fontFeatures: const [FontFeature.tabularFigures()]),
                  ),
                ],
              ),
            ),
        ],
      ),
    );
  }

  String? _rangeOf(String day) {
    final ranges = hours.where((h) => h.day == day).map((h) => ltr('${h.opens}–${h.closes}')).toList();
    return ranges.isEmpty ? null : ranges.join(', ');
  }
}
