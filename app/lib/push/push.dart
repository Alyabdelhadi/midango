// firebase_messaging 17 deprecates getToken/onTokenRefresh/deleteToken in favour of register()/onRegistered,
// which hand out a Firebase Installation ID instead of the FCM registration token the server sends to.
// ponytail: stays on the token API until the server's FcmPushSender targets installation IDs.
// ignore_for_file: deprecated_member_use
import 'dart:async';

import 'package:firebase_core/firebase_core.dart';
import 'package:firebase_messaging/firebase_messaging.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';

import '../session/session.dart';

/// The app's one Push, set up by main.
final push = Push();

/// Booking reminders over FCM, Android only for now: iOS needs an APNs key and web a VAPID key and service
/// worker, so there every method is a no-op. Failures are silent; the next start retries.
class Push {
  static const _askedKey = 'push_asked';
  bool _on = false;
  late Session _session;
  late FlutterSecureStorage _storage;
  late void Function(String bookingId) _open;
  late void Function(String tournamentId) _openTournament;
  bool _wasSignedIn = false;
  RemoteMessage? _launchTap;

  /// 'en' or 'ar': the language reminders are written in. Set it, then [register] again.
  String language = 'en';

  /// Call before `session.restore()` so the first sign-in state is seen.
  Future<void> init(
    Session session,
    FlutterSecureStorage storage, {
    required String language,
    required void Function(String bookingId) open,
    required void Function(String tournamentId) openTournament,

    /// `view` opens what the message is about (a booking or a tournament), null when it's about neither.
    required void Function(String title, String body, VoidCallback? view) foreground,
  }) async {
    if (kIsWeb || defaultTargetPlatform != TargetPlatform.android) return;
    try {
      await Firebase.initializeApp();
      _session = session;
      _storage = storage;
      _open = open;
      _openTournament = openTournament;
      this.language = language;
      session.addListener(_onSession);
      final messaging = FirebaseMessaging.instance;
      messaging.onTokenRefresh.listen((_) => register());
      FirebaseMessaging.onMessageOpenedApp.listen(_tap);
      // Android shows nothing while the app is open, so the app says it itself.
      FirebaseMessaging.onMessage.listen(
          (m) => foreground(m.notification?.title ?? '', m.notification?.body ?? '', _viewFor(m)));
      _launchTap = await messaging.getInitialMessage(); // opened once the session is restored
      _on = true;
    } catch (_) {
      // No Play services or Firebase misconfigured: the app works without reminders.
    }
  }

  void _tap(RemoteMessage m) => _viewFor(m)?.call();

  /// Tournament pushes carry `tournamentId`, booking pushes `bookingId`.
  VoidCallback? _viewFor(RemoteMessage m) {
    final tournament = m.data['tournamentId'], booking = m.data['bookingId'];
    if (tournament is String) return () => _openTournament(tournament);
    if (booking is String) return () => _open(booking);
    return null;
  }

  void _onSession() {
    final signedIn = _session.signedIn;
    if (signedIn && !_wasSignedIn) register(); // app start signed in, or sign-in
    _wasSignedIn = signedIn;
    final launch = _launchTap;
    _launchTap = null;
    if (launch != null) _tap(launch);
  }

  /// After the first booking: the system prompt (Android 13+), asked once ever, then [register].
  Future<void> askPermissionOnce() async {
    if (!_on) return;
    try {
      if (await _storage.read(key: _askedKey) == null) {
        await _storage.write(key: _askedKey, value: '1');
        await FirebaseMessaging.instance.requestPermission();
      }
    } catch (_) {}
    await register();
  }

  /// Tells the server this device's token, when signed in and notifications are allowed.
  Future<void> register() async {
    if (!_on || !_session.signedIn) return;
    try {
      final messaging = FirebaseMessaging.instance;
      final settings = await messaging.getNotificationSettings();
      if (settings.authorizationStatus != AuthorizationStatus.authorized) return;
      final token = await messaging.getToken();
      if (token == null) return;
      await _session.api.put('/me/devices', {'token': token, 'platform': 'android', 'language': language});
    } catch (_) {}
  }

  /// Before sign-out clears the session (the call needs the access token). Never blocks sign-out for long.
  Future<void> unregister() async {
    if (!_on) return;
    try {
      await () async {
        final token = await FirebaseMessaging.instance.getToken();
        if (token != null) await _session.api.delete('/me/devices', query: {'token': token});
        await FirebaseMessaging.instance.deleteToken();
      }()
          .timeout(const Duration(seconds: 5));
    } catch (_) {}
  }
}
