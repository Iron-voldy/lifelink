import 'dart:async';
import 'dart:convert';
import 'dart:io';
import 'package:http/http.dart' as http;
import '../models/session.dart';

class ApiException implements Exception {
  const ApiException(this.message, this.statusCode);
  final String message;
  final int statusCode;
  @override
  String toString() => message;
}

class ApiClient {
  ApiClient({String? baseUrl})
      : baseUrl = baseUrl ??
            const String.fromEnvironment('LIFELINK_API_BASE_URL',
                defaultValue: 'https://lifelink-api.72-62-255-113.sslip.io');
  final String baseUrl;
  String? accessToken;
  String? refreshToken;
  Future<void> Function(Session session)? onSessionRefreshed;

  Future<Session> login(String email, String password) async =>
      Session.fromJson(await post(
          '/api/auth/login',
          {
            'email': email,
            'password': password,
            'deviceName': 'LifeLink mobile'
          },
          authenticated: false) as Map<String, dynamic>);
  Future<Session> register(String email, String password, String role) async =>
      Session.fromJson(await post(
          '/api/auth/register',
          {
            'email': email,
            'password': password,
            'role': role,
            'deviceName': 'LifeLink mobile'
          },
          authenticated: false) as Map<String, dynamic>);
  Future<Session> refresh(String refreshToken) async =>
      Session.fromJson(await post('/api/auth/refresh',
          {'refreshToken': refreshToken, 'deviceName': 'LifeLink mobile'},
          authenticated: false) as Map<String, dynamic>);

  /// Best-effort server-side sign-out; a failure must never keep the person signed in locally.
  Future<void> revoke(String refreshToken) async {
    try {
      await post('/api/auth/revoke', {'refreshToken': refreshToken},
          authenticated: false);
    } catch (_) {}
  }

  Future<dynamic> get(String path) => _send('GET', path);
  Future<dynamic> put(String path, Object body) =>
      _send('PUT', path, body: body);
  Future<dynamic> delete(String path, {Object? body}) =>
      _send('DELETE', path, body: body);

  /// Push token registered for the signed-in account (set by PushNotificationService).
  String? deviceToken;

  /// Best-effort removal of this device's push token so the previous account
  /// stops receiving pushes and the next account can register the same token.
  Future<void> unregisterDevice() async {
    final token = deviceToken;
    deviceToken = null;
    if (token == null || accessToken == null) return;
    try {
      await delete('/api/notifications/devices', body: {'token': token})
          .timeout(const Duration(seconds: 5));
    } catch (_) {}
  }
  Future<dynamic> post(String path, Object body, {bool authenticated = true}) =>
      _send('POST', path, body: body, authenticated: authenticated);
  Future<dynamic> _send(String method, String path,
      {Object? body, bool authenticated = true, bool retry = true}) async {
    final response = http.Request(method, Uri.parse('$baseUrl$path'));
    response.headers.addAll({
      'Accept': 'application/json',
      if (body != null) 'Content-Type': 'application/json',
      if (authenticated && accessToken != null)
        'Authorization': 'Bearer $accessToken'
    });
    response.body = body == null ? '' : jsonEncode(body);
    final http.Response value;
    try {
      final streamed =
          await response.send().timeout(const Duration(seconds: 15));
      value = await http.Response.fromStream(streamed);
    } on TimeoutException {
      throw const ApiException(
          'The server took too long to respond. Please try again.', 0);
    } on SocketException {
      throw const ApiException(
          'Cannot reach LifeLink. Check your internet connection and try again.',
          0);
    } on http.ClientException {
      throw const ApiException(
          'Cannot reach LifeLink. Check your internet connection and try again.',
          0);
    }
    if (value.statusCode == 401 &&
        authenticated &&
        retry &&
        refreshToken != null) {
      final session = await refresh(refreshToken!);
      accessToken = session.accessToken;
      refreshToken = session.refreshToken;
      await onSessionRefreshed?.call(session);
      return _send(method, path,
          body: body, authenticated: authenticated, retry: false);
    }
    dynamic decoded;
    if (value.body.isNotEmpty) {
      try {
        decoded = jsonDecode(value.body);
      } on FormatException {
        // A proxy or gateway error page is not JSON; fall through to a status-based message.
        decoded = null;
      }
    }
    if (value.statusCode < 200 || value.statusCode >= 300) {
      throw ApiException(
          _messageFor(value.statusCode, decoded), value.statusCode);
    }
    return decoded;
  }

  /// Turns a server problem into a sentence the person can act on.
  static String _messageFor(int status, dynamic problem) {
    final map = problem is Map ? problem : const {};
    final detail = map['detail']?.toString();
    final errors = map['errors'];
    final fieldErrors = errors is Map
        ? errors.entries
            .map((e) =>
                '${e.key}: ${e.value is List && (e.value as List).isNotEmpty ? (e.value as List).first : e.value}')
            .join('; ')
        : '';
    if (status == 401) {
      return detail ?? 'Your session has expired. Please sign in again.';
    }
    if (status == 403) return 'You do not have permission to do this.';
    if (status == 429) {
      return detail ?? 'Too many attempts. Please wait a minute and try again.';
    }
    if (status >= 500) {
      final ref = map['correlationId'];
      return '${detail ?? 'The server had a problem. Please try again shortly.'}${ref != null ? ' (reference $ref)' : ''}';
    }
    if (detail != null && detail.isNotEmpty) return detail;
    if (fieldErrors.isNotEmpty) return fieldErrors;
    return map['title']?.toString() ?? 'Request failed ($status)';
  }
}
