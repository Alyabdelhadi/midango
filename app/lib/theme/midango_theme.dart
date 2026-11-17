import 'package:flutter/material.dart';
import 'package:google_fonts/google_fonts.dart';

import 'midango_colors.dart';

/// App type scale (D5), logical px. Arabic glyphs fall back to IBM Plex Sans Arabic.
TextTheme _textTheme() {
  final arabic = GoogleFonts.ibmPlexSansArabic().fontFamily!;
  TextStyle display(double size, double height, FontWeight weight) =>
      GoogleFonts.spaceGrotesk(fontSize: size, height: height / size, fontWeight: weight, color: MidangoColors.navy)
          .copyWith(fontFamilyFallback: [arabic]);
  TextStyle ui(double size, double height, FontWeight weight, Color color) =>
      GoogleFonts.inter(fontSize: size, height: height / size, fontWeight: weight, color: color)
          .copyWith(fontFamilyFallback: [arabic]);

  return TextTheme(
    headlineLarge: display(32, 38, FontWeight.w700), // display: "You're booked"
    titleLarge: display(24, 30, FontWeight.w700), // screen titles, venue name
    titleMedium: display(18, 24, FontWeight.w500), // section heads, price
    bodyLarge: ui(16, 24, FontWeight.w400, MidangoColors.slate), // body
    labelLarge: ui(14, 20, FontWeight.w600, MidangoColors.navy), // buttons, chips
    bodySmall: ui(12, 16, FontWeight.w500, MidangoColors.muted), // caption, metadata
  );
}

ThemeData buildMidangoTheme() {
  const scheme = ColorScheme.light(
    primary: MidangoColors.lime,
    onPrimary: MidangoColors.navy,
    secondary: MidangoColors.cyanInk,
    onSecondary: MidangoColors.white,
    surface: MidangoColors.white,
    onSurface: MidangoColors.navy,
    error: MidangoColors.error,
    onError: MidangoColors.white,
    outline: MidangoColors.line,
  );
  final text = _textTheme();
  final shape = RoundedRectangleBorder(borderRadius: BorderRadius.circular(12));
  const minSize = Size(64, 48);

  return ThemeData(
    useMaterial3: true,
    colorScheme: scheme,
    scaffoldBackgroundColor: MidangoColors.cloud,
    textTheme: text,
    appBarTheme: AppBarTheme(
      backgroundColor: MidangoColors.white,
      foregroundColor: MidangoColors.navy,
      elevation: 0,
      titleTextStyle: text.titleMedium,
    ),
    filledButtonTheme: FilledButtonThemeData(
      style: FilledButton.styleFrom(
        backgroundColor: MidangoColors.lime,
        foregroundColor: MidangoColors.navy,
        disabledBackgroundColor: MidangoColors.disabledBg,
        disabledForegroundColor: MidangoColors.muted,
        minimumSize: minSize,
        shape: shape,
        textStyle: text.labelLarge,
      ),
    ),
    outlinedButtonTheme: OutlinedButtonThemeData(
      style: OutlinedButton.styleFrom(
        foregroundColor: MidangoColors.navy,
        side: const BorderSide(color: MidangoColors.navy, width: 1.5),
        minimumSize: minSize,
        shape: shape,
        textStyle: text.labelLarge,
      ),
    ),
    textButtonTheme: TextButtonThemeData(
      style: TextButton.styleFrom(
        foregroundColor: MidangoColors.cyanInk,
        textStyle: text.labelLarge?.copyWith(decoration: TextDecoration.underline),
      ),
    ),
    cardTheme: CardThemeData(
      color: MidangoColors.white,
      elevation: 0,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(12),
        side: const BorderSide(color: MidangoColors.line),
      ),
    ),
  );
}
