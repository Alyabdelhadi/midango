import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';

import 'api/api_client.dart';
import 'home_shell.dart';
import 'l10n/strings.dart';
import 'session/session.dart';
import 'theme/midango_theme.dart';

const _langKey = 'lang';

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();
  const storage = FlutterSecureStorage();
  String? lang;
  try {
    lang = await storage.read(key: _langKey);
  } catch (_) {
    // Storage unavailable: start in English.
  }
  final session = Session(ApiClient(), storage: storage);
  session.restore(); // not awaited: the Play tab needs no account, so don't hold the first frame for the network
  runApp(MidangoApp(session: session, storage: storage, initialLanguage: lang == 'ar' ? 'ar' : 'en'));
}

class MidangoApp extends StatefulWidget {
  const MidangoApp({super.key, required this.session, required this.storage, required this.initialLanguage});

  final Session session;
  final FlutterSecureStorage storage;
  final String initialLanguage;

  @override
  State<MidangoApp> createState() => _MidangoAppState();
}

class _MidangoAppState extends State<MidangoApp> {
  late String _lang = widget.initialLanguage;

  void _setLanguage(String lang) {
    setState(() => _lang = lang);
    widget.storage.write(key: _langKey, value: lang).catchError((_) {});
  }

  @override
  Widget build(BuildContext context) {
    final strings = _lang == 'ar' ? ar : en;
    return SessionScope(
      session: widget.session,
      child: MaterialApp(
        title: 'Midango',
        debugShowCheckedModeBanner: false,
        theme: buildMidangoTheme(),
        locale: Locale(_lang),
        supportedLocales: const [Locale('en'), Locale('ar')],
        localizationsDelegates: GlobalMaterialLocalizations.delegates,
        builder: (context, child) => StringsScope(strings: strings, child: child!),
        home: HomeShell(onLanguage: _setLanguage),
      ),
    );
  }
}
