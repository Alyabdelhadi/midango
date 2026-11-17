import 'package:flutter/material.dart';
import 'package:google_fonts/google_fonts.dart';

import '../theme/midango_colors.dart';

/// A start time the customer can tap. Lime fill = open; grey + struck through = full.
/// Full times stay visible so the customer knows why a time is missing.
class SlotChip extends StatelessWidget {
  const SlotChip({
    super.key,
    required this.time,
    required this.available,
    required this.semanticLabel,
    this.onTap,
  });

  final String time;
  final bool available;

  /// Read by screen readers, e.g. "20:00, available, 180 riyals".
  final String semanticLabel;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    return Semantics(
      button: true,
      enabled: available,
      label: semanticLabel,
      excludeSemantics: true,
      child: Material(
        color: available ? MidangoColors.lime : MidangoColors.disabledBg,
        borderRadius: BorderRadius.circular(8),
        child: InkWell(
          borderRadius: BorderRadius.circular(8),
          onTap: available ? onTap : null,
          child: ConstrainedBox(
            constraints: const BoxConstraints(minWidth: 64, minHeight: 44),
            // widthFactor/heightFactor 1: size to the label, don't stretch to the parent's width.
            child: Center(
              widthFactor: 1,
              heightFactor: 1,
              child: Padding(
                padding: const EdgeInsets.symmetric(horizontal: 12),
                child: Text(
                  time,
                  style: GoogleFonts.spaceGrotesk(
                    fontSize: 16,
                    fontWeight: FontWeight.w500,
                    color: available ? MidangoColors.navy : MidangoColors.muted,
                    decoration: available ? null : TextDecoration.lineThrough,
                    fontFeatures: const [FontFeature.tabularFigures()],
                  ),
                ),
              ),
            ),
          ),
        ),
      ),
    );
  }
}
