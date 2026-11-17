import 'package:flutter/painting.dart';

/// Midango brand colors. Source: docs/research/midango-design-research.html (D1–D4).
/// Lime is a fill only: never use it as text or icon color on light surfaces.
abstract final class MidangoColors {
  static const lime = Color(0xFFA8FF00);
  static const navy = Color(0xFF071329);
  static const slate = Color(0xFF202B43);
  static const muted = Color(0xFF5B6478);
  static const cyan = Color(0xFF00CDEB); // gradients and links on navy only
  static const cyanInk = Color(0xFF00788A); // links and focus on light surfaces
  static const cloud = Color(0xFFF5F7FA);
  static const white = Color(0xFFFFFFFF);
  static const line = Color(0xFFE3E7EE);
  static const disabledBg = Color(0xFFE7EAF2);
  static const error = Color(0xFFD92D20);
  static const errorBg = Color(0xFFFEF0EF);
  static const warning = Color(0xFFB54708);
  static const warningBg = Color(0xFFFFF4E5);
}
