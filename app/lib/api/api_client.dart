import 'dart:convert';

import 'package:http/http.dart' as http;

import '../config.dart';

/// A failed call. `title` is the API's sentence for people (ProblemDetails.title).
class ApiException implements Exception {
  ApiException(this.status, this.title);
  final int status;
  final String title;
  @override
  String toString() => title;
}

/// Supplies and renews the access token; implemented by Session.
abstract interface class TokenSource {
  String? get accessToken;

  /// Spends the refresh token once. Concurrent callers must share one call (refresh tokens are single-use).
  Future<bool> refresh();
}

class ApiClient {
  ApiClient({http.Client? client}) : _http = client ?? http.Client();
  final http.Client _http;
  TokenSource? tokens;

  Future<dynamic> get(String path, {Map<String, String>? query, bool auth = false}) =>
      _send('GET', path, query: query, auth: auth);
  Future<dynamic> post(String path, [Object? body, bool auth = false]) => _send('POST', path, body: body, auth: auth);
  Future<dynamic> put(String path, Object? body, {bool auth = true}) => _send('PUT', path, body: body, auth: auth);

  Future<dynamic> _send(String method, String path,
      {Map<String, String>? query, Object? body, required bool auth}) async {
    Future<http.Response> send() {
      final request = http.Request(method, Uri.parse('$apiBase$path').replace(queryParameters: query))
        ..headers['Content-Type'] = 'application/json';
      final token = tokens?.accessToken;
      if (auth && token != null) request.headers['Authorization'] = 'Bearer $token';
      if (body != null) request.body = jsonEncode(body);
      return _http.send(request).then(http.Response.fromStream);
    }

    var res = await send();
    if (res.statusCode == 401 && auth && tokens != null && await tokens!.refresh()) res = await send();
    final text = utf8.decode(res.bodyBytes);
    final json = text.isEmpty ? null : jsonDecode(text);
    if (res.statusCode >= 400) {
      final title = json is Map && json['title'] is String ? json['title'] as String : 'Request failed (${res.statusCode})';
      throw ApiException(res.statusCode, title);
    }
    return json;
  }
}
