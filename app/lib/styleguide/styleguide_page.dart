import 'package:flutter/material.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';

import '../theme/midango_colors.dart';
import '../widgets/slot_chip.dart';

class StyleguidePage extends StatefulWidget {
  const StyleguidePage({super.key, required this.isArabic, required this.onToggleLanguage});

  final bool isArabic;
  final VoidCallback onToggleLanguage;

  @override
  State<StyleguidePage> createState() => _StyleguidePageState();
}

class _StyleguidePageState extends State<StyleguidePage> {
  String? _picked;

  static const _swatches = <(String, Color)>[
    ('Lime', MidangoColors.lime),
    ('Navy', MidangoColors.navy),
    ('Slate', MidangoColors.slate),
    ('Muted', MidangoColors.muted),
    ('Cyan', MidangoColors.cyan),
    ('Cyan Ink', MidangoColors.cyanInk),
    ('Cloud', MidangoColors.cloud),
    ('Error', MidangoColors.error),
    ('Warning', MidangoColors.warning),
  ];

  static const _slots = <(String, bool)>[
    ('18:00', true),
    ('19:00', true),
    ('20:00', true),
    ('20:30', false),
    ('21:30', true),
  ];

  @override
  Widget build(BuildContext context) {
    final text = Theme.of(context).textTheme;
    final sample = widget.isArabic ? 'احجز ملعبك القادم' : 'Book your next move';

    return Scaffold(
      appBar: AppBar(
        leading: Padding(
          padding: const EdgeInsetsDirectional.only(start: 16),
          child: Image.asset('assets/brand/midango-icon.png', semanticLabel: 'Midango'),
        ),
        title: const Text('Styleguide'),
        actions: [
          TextButton.icon(
            onPressed: widget.onToggleLanguage,
            icon: const Icon(LucideIcons.languages, size: 18),
            label: Text(widget.isArabic ? 'English' : 'العربية'),
          ),
        ],
      ),
      body: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          Text('Colors', style: text.titleMedium),
          const SizedBox(height: 8),
          Wrap(
            spacing: 8,
            runSpacing: 8,
            children: [
              for (final (name, color) in _swatches)
                Column(
                  children: [
                    Container(
                      width: 56,
                      height: 40,
                      decoration: BoxDecoration(
                        color: color,
                        borderRadius: BorderRadius.circular(8),
                        border: Border.all(color: MidangoColors.line),
                      ),
                    ),
                    Text(name, style: text.bodySmall),
                  ],
                ),
            ],
          ),
          const SizedBox(height: 24),
          Text('Type', style: text.titleMedium),
          Text(sample, style: text.headlineLarge),
          Text(sample, style: text.titleLarge),
          Text(sample, style: text.titleMedium),
          Text(sample, style: text.bodyLarge),
          Text(sample, style: text.labelLarge),
          Text(sample, style: text.bodySmall),
          const SizedBox(height: 24),
          Text('Slots', style: text.titleMedium),
          const SizedBox(height: 8),
          Wrap(
            spacing: 8,
            runSpacing: 8,
            children: [
              for (final (time, open) in _slots)
                SlotChip(
                  time: time,
                  available: open,
                  semanticLabel: open ? '$time, available' : '$time, full',
                  onTap: () => setState(() => _picked = time),
                ),
            ],
          ),
          const SizedBox(height: 8),
          Text(_picked == null ? 'Tap an open time' : 'Picked $_picked', style: text.bodySmall),
          const SizedBox(height: 24),
          Text('Buttons', style: text.titleMedium),
          const SizedBox(height: 8),
          FilledButton(onPressed: () {}, child: const Text('Confirm booking')),
          const SizedBox(height: 8),
          OutlinedButton(onPressed: () {}, child: const Text('Change time')),
          const SizedBox(height: 8),
          const FilledButton(onPressed: null, child: Text('Confirm booking')),
          Text('Disabled example: no open time selected', style: text.bodySmall),
        ],
      ),
    );
  }
}
