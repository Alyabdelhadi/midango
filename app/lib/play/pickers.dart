import 'package:flutter/material.dart';

import '../api/models.dart';
import '../l10n/format.dart';
import '../l10n/strings.dart';
import '../theme/midango_colors.dart';
import '../widgets/sport_icon.dart';

/// The days a customer can book: today and the next 13, by the device's date.
List<String> bookableDays() {
  final now = DateTime.now();
  return [for (var i = 0; i < 14; i++) isoDate(DateTime(now.year, now.month, now.day + i))];
}

/// Sport chips. Selected = lime fill with navy text.
class SportPicker extends StatelessWidget {
  const SportPicker({super.key, required this.categories, required this.selectedId, required this.onSelected});

  final List<Category> categories;
  final String? selectedId;
  final ValueChanged<Category> onSelected;

  @override
  Widget build(BuildContext context) {
    final arabic = Strings.of(context).isArabic;
    return _ChipRow(children: [
      for (final c in categories)
        _PickerChip(
          label: c.name(arabic),
          icon: SportIcon(iconKey: c.iconKey, iconUrl: c.iconUrl, color: MidangoColors.navy),
          selected: c.id == selectedId,
          onTap: () => onSelected(c),
        ),
    ]);
  }
}

/// "Today", "Tomorrow", then "Thu 21 Nov".
class DayPicker extends StatelessWidget {
  const DayPicker({super.key, required this.selected, required this.onSelected});

  final String selected;
  final ValueChanged<String> onSelected;

  @override
  Widget build(BuildContext context) {
    final s = Strings.of(context);
    final days = bookableDays();
    return _ChipRow(children: [
      for (var i = 0; i < days.length; i++)
        _PickerChip(
          label: switch (i) { 0 => s.today, 1 => s.tomorrow, _ => formatDay(days[i], s) },
          selected: days[i] == selected,
          onTap: () => onSelected(days[i]),
        ),
    ]);
  }
}

/// "60 min", "90 min" — the sport's booking lengths.
class LengthPicker extends StatelessWidget {
  const LengthPicker({super.key, required this.durations, required this.selected, required this.onSelected});

  final List<int> durations;
  final int selected;
  final ValueChanged<int> onSelected;

  @override
  Widget build(BuildContext context) {
    final s = Strings.of(context);
    return _ChipRow(children: [
      for (final m in durations)
        _PickerChip(label: s.minutes(m), selected: m == selected, onTap: () => onSelected(m)),
    ]);
  }
}

/// The API's level bands, lowest first.
const matchLevels = ['Beginner', 'Intermediate', 'Advanced'];

/// "Any level" then the three bands. `inline` wraps inside padded content instead of a full-width scroller.
class LevelPicker extends StatelessWidget {
  const LevelPicker({super.key, required this.selected, required this.onSelected, this.inline = false});

  final String? selected;
  final ValueChanged<String?>? onSelected;
  final bool inline;

  @override
  Widget build(BuildContext context) {
    final s = Strings.of(context);
    final chips = [
      for (final level in [null, ...matchLevels])
        _PickerChip(
          label: level == null ? s.levelAny : s.levelName(level),
          selected: level == selected,
          onTap: onSelected == null ? null : () => onSelected!(level),
        ),
    ];
    return inline ? Wrap(spacing: 8, runSpacing: 8, children: chips) : _ChipRow(children: chips);
  }
}

class _ChipRow extends StatelessWidget {
  const _ChipRow({required this.children});
  final List<Widget> children;

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      height: 48,
      child: ListView.separated(
        scrollDirection: Axis.horizontal,
        padding: const EdgeInsets.symmetric(horizontal: 16),
        itemCount: children.length,
        separatorBuilder: (_, _) => const SizedBox(width: 8),
        itemBuilder: (_, i) => Center(child: children[i]),
      ),
    );
  }
}

class _PickerChip extends StatelessWidget {
  const _PickerChip({required this.label, required this.selected, required this.onTap, this.icon});

  final String label;
  final Widget? icon;
  final bool selected;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    return ChoiceChip(
      label: Text(label),
      avatar: icon,
      selected: selected,
      showCheckmark: false,
      onSelected: onTap == null ? null : (_) => onTap!(),
      selectedColor: MidangoColors.lime,
      backgroundColor: MidangoColors.white,
      labelStyle: Theme.of(context).textTheme.labelLarge,
      side: BorderSide(color: selected ? MidangoColors.lime : MidangoColors.line),
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(8)),
    );
  }
}
