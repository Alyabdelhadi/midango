import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';

import 'styleguide/styleguide_page.dart';
import 'theme/midango_theme.dart';

void main() => runApp(const MidangoApp());

class MidangoApp extends StatefulWidget {
  const MidangoApp({super.key});

  @override
  State<MidangoApp> createState() => _MidangoAppState();
}

class _MidangoAppState extends State<MidangoApp> {
  Locale _locale = const Locale('en');

  @override
  Widget build(BuildContext context) {
    final isArabic = _locale.languageCode == 'ar';
    return MaterialApp(
      title: 'Midango',
      debugShowCheckedModeBanner: false,
      theme: buildMidangoTheme(),
      locale: _locale,
      supportedLocales: const [Locale('en'), Locale('ar')],
      localizationsDelegates: GlobalMaterialLocalizations.delegates,
      home: StyleguidePage(
        isArabic: isArabic,
        onToggleLanguage: () => setState(
          () => _locale = isArabic ? const Locale('en') : const Locale('ar'),
        ),
      ),
    );
  }
}
