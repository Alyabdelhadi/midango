import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:google_fonts/google_fonts.dart';

import 'api/api_client.dart';
import 'api/models.dart';
import 'app_config.dart';
import 'bookings/booking_detail_page.dart';
import 'home_shell.dart';
import 'l10n/strings.dart';
import 'push/push.dart';
import 'session/session.dart';
import 'theme/midango_theme.dart';
import 'tournaments/tournament_page.dart';

const _langKey = 'lang';

/// google_fonts downloads each font on first use. Text laid out before it arrives keeps the fallback
/// font's width (chip labels got clipped) and Arabic shows empty boxes, so wait for every weight we use.
/// ponytail: still a network fetch; bundle the font files as assets before release.
Future<void> _loadFonts() async {
  try {
    await GoogleFonts.pendingFonts([
      for (final w in [FontWeight.w400, FontWeight.w500, FontWeight.w600]) GoogleFonts.inter(fontWeight: w),
      for (final w in [FontWeight.w500, FontWeight.w700]) GoogleFonts.spaceGrotesk(fontWeight: w),
      for (final w in [FontWeight.w400, FontWeight.w500, FontWeight.w600, FontWeight.w700])
        GoogleFonts.ibmPlexSansArabic(fontWeight: w),
    ]).timeout(const Duration(seconds: 8));
  } catch (_) {
    // Offline or slow: start with the fallback font rather than a blank screen.
  }
}

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();
  const storage = FlutterSecureStorage();
  String? lang;
  try {
    lang = await storage.read(key: _langKey);
  } catch (_) {
    // Storage unavailable: start in English.
  }
  await _loadFonts();
  final session = Session(ApiClient(), storage: storage);
  final language = lang == 'ar' ? 'ar' : 'en';
  await push.init(session, storage,
      language: language,
      open: (id) => _openBooking(session, id),
      openTournament: _openTournament,
      foreground: _showReminder);
  session.restore(); // not awaited: the Play tab needs no account, so don't hold the first frame for the network
  runApp(MidangoApp(session: session, storage: storage, initialLanguage: language));
}

final _navigator = GlobalKey<NavigatorState>();
final _messenger = GlobalKey<ScaffoldMessengerState>();

/// A tapped reminder: the Bookings tab, then that booking if it's still listed.
Future<void> _openBooking(Session session, String id) async {
  final nav = _navigator.currentState;
  if (nav == null) {
    // Launched by the tap: wait for the first frame.
    WidgetsBinding.instance.addPostFrameCallback((_) => _openBooking(session, id));
    return;
  }
  nav.popUntil((route) => route.isFirst);
  shellTab.value = 1;
  if (!session.signedIn) return;
  try {
    final data = MyBookings.fromJson(await session.api.get('/me/bookings', auth: true) as JsonMap);
    final booking = [...data.upcoming, ...data.history].where((b) => b.id == id).firstOrNull;
    if (booking != null) nav.push(MaterialPageRoute<void>(builder: (_) => BookingDetailPage(booking: booking)));
  } catch (_) {
    // Offline: the Bookings tab shows its own retry.
  }
}

/// A tapped tournament push (cancelled, off the waitlist): the Play tab, then that tournament's page.
void _openTournament(String id) {
  final nav = _navigator.currentState;
  if (nav == null) {
    WidgetsBinding.instance.addPostFrameCallback((_) => _openTournament(id));
    return;
  }
  nav.popUntil((route) => route.isFirst);
  shellTab.value = 0;
  nav.push(MaterialPageRoute<void>(builder: (_) => TournamentPage(tournamentId: id)));
}

/// A reminder arriving while the app is open.
void _showReminder(String title, String body, VoidCallback? view) {
  final context = _navigator.currentContext;
  if (context == null) return;
  _messenger.currentState?.showSnackBar(SnackBar(
    content: Text(body.isEmpty ? title : '$title\n$body'),
    action: view == null ? null : SnackBarAction(label: Strings.of(context).view, onPressed: view),
  ));
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
    push.language = lang;
    push.register(); // reminders follow the app language
  }

  @override
  Widget build(BuildContext context) {
    final strings = _lang == 'ar' ? ar : en;
    return SessionScope(
      session: widget.session,
      child: MaterialApp(
        title: 'Midango',
        navigatorKey: _navigator,
        scaffoldMessengerKey: _messenger,
        debugShowCheckedModeBanner: false,
        theme: buildMidangoTheme(),
        locale: Locale(_lang),
        supportedLocales: const [Locale('en'), Locale('ar')],
        localizationsDelegates: GlobalMaterialLocalizations.delegates,
        builder: (context, child) => StringsScope(strings: strings, child: child!),
        home: AppGate(child: HomeShell(onLanguage: _setLanguage)),
      ),
    );
  }
}
