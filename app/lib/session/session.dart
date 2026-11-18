import 'dart:async';

import 'package:flutter/widgets.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';

import '../api/api_client.dart';
import '../api/models.dart';
import '../push/push.dart';

/// The signed-in customer. Refresh token in secure storage; access token in memory only.
class Session extends ChangeNotifier implements TokenSource {
  Session(this.api, {FlutterSecureStorage? storage}) : _storage = storage ?? const FlutterSecureStorage() {
    api.tokens = this;
  }

  static const _refreshKey = 'refresh_token';
  final ApiClient api;
  final FlutterSecureStorage _storage;
  String? _access;
  String? _refresh;
  Future<bool>? _refreshing;
  Me? me;

  /// Sport id → "Beginner" | "Intermediate" | "Advanced"; null until loaded.
  Map<String, String>? levels;

  bool get signedIn => _refresh != null;
  bool get hasName => me?.name != null;
  @override
  String? get accessToken => _access;

  /// On launch: trade the stored refresh token for a session and load the profile. Offline keeps it for later.
  Future<void> restore() async {
    try {
      _refresh = await _storage.read(key: _refreshKey);
      if (_refresh != null && await refresh()) await loadMe();
    } catch (_) {
      // Offline or storage unavailable: stay as we are; the next authorised call retries.
    }
    notifyListeners();
  }

  Future<void> requestCode(String phone) => api.post('/auth/otp/request', {'phone': phone});

  /// Returns true when the account already has a name.
  Future<bool> verify(String phone, String code) async {
    final json = await api.post('/auth/otp/verify', {'phone': phone, 'code': code}) as Map<String, dynamic>;
    await _store(json['accessToken'] as String, json['refreshToken'] as String);
    await loadMe();
    notifyListeners();
    return json['profileComplete'] as bool;
  }

  Future<void> loadMe() async {
    me = Me.fromJson(await api.get('/me', auth: true) as Map<String, dynamic>);
  }

  Future<void> saveName(String name) async {
    me = Me.fromJson(await api.put('/me', {'name': name}) as Map<String, dynamic>);
    notifyListeners();
  }

  /// The customer's levels, loaded once per sign-in. Empty when signed out or offline.
  Future<Map<String, String>> ensureLevels() async {
    if (!signedIn) return const {};
    final cached = levels;
    if (cached != null) return cached;
    try {
      final json = await api.get('/me/levels', auth: true) as List;
      levels = {for (final l in json.cast<Map<String, dynamic>>()) l['categoryId'] as String: l['level'] as String};
      notifyListeners();
      return levels!;
    } catch (_) {
      return const {};
    }
  }

  /// Null clears the level for that sport.
  Future<void> setLevel(String categoryId, String? level) async {
    await api.put('/me/levels/$categoryId', {'level': level});
    final next = {...?levels};
    level == null ? next.remove(categoryId) : next[categoryId] = level;
    levels = next;
    notifyListeners();
  }

  @override
  Future<bool> refresh() => _refreshing ??= _doRefresh().whenComplete(() => _refreshing = null);

  Future<bool> _doRefresh() async {
    final token = _refresh;
    if (token == null) return false;
    try {
      final json = await api.post('/auth/refresh', {'refreshToken': token}) as Map<String, dynamic>;
      await _store(json['accessToken'] as String, json['refreshToken'] as String);
      return true;
    } on ApiException catch (e) {
      if (e.status == 401) {
        await _clear(); // revoked or expired: signed out
        notifyListeners();
      }
      return false;
    } catch (_) {
      return false; // offline: keep the token and try again later
    }
  }

  Future<void> signOut() async {
    await push.unregister(); // first: it needs the access token
    final token = _refresh;
    await _clear();
    notifyListeners();
    if (token != null) unawaited(api.post('/auth/logout', {'refreshToken': token}).catchError((_) => null));
  }

  Future<void> _store(String access, String refresh) async {
    _access = access;
    _refresh = refresh;
    await _storage.write(key: _refreshKey, value: refresh);
  }

  Future<void> _clear() async {
    _access = null;
    _refresh = null;
    me = null;
    levels = null;
    await _storage.delete(key: _refreshKey);
  }
}

/// Puts the one Session above the app. `of` rebuilds on sign-in/out; `read` is for callbacks.
class SessionScope extends InheritedNotifier<Session> {
  const SessionScope({super.key, required Session session, required super.child}) : super(notifier: session);

  static Session of(BuildContext context) =>
      context.dependOnInheritedWidgetOfExactType<SessionScope>()!.notifier!;

  static Session read(BuildContext context) => context.getInheritedWidgetOfExactType<SessionScope>()!.notifier!;
}
