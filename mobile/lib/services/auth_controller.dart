import 'dart:async';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import '../models/session.dart';
import 'api_client.dart';

final apiProvider = Provider((_) => ApiClient());
final authProvider =
    AsyncNotifierProvider<AuthController, Session?>(AuthController.new);

class AuthController extends AsyncNotifier<Session?> {
  static const _storage = FlutterSecureStorage();
  @override
  Future<Session?> build() async {
    ref.read(apiProvider).onSessionRefreshed = _persist;
    final token = await _storage.read(key: 'refresh_token');
    if (token == null) return null;
    try {
      final session = await ref.read(apiProvider).refresh(token);
      await _persist(session);
      return session;
    } catch (_) {
      await _storage.delete(key: 'refresh_token');
      return null;
    }
  }

  Future<void> login(String email, String password) async {
    state = const AsyncLoading();
    state = await AsyncValue.guard(() async {
      final session = await ref.read(apiProvider).login(email, password);
      await _persist(session);
      return session;
    });
  }

  Future<void> register(String email, String password, String role) async {
    state = const AsyncLoading();
    state = await AsyncValue.guard(() async {
      final session =
          await ref.read(apiProvider).register(email, password, role);
      await _persist(session);
      return session;
    });
  }

  Future<void> logout() async {
    final api = ref.read(apiProvider);
    // Must run while still authenticated: the endpoint removes the token for the current user.
    await api.unregisterDevice();
    final token = api.refreshToken;
    api.accessToken = null;
    api.refreshToken = null;
    state = const AsyncData(null);
    await _storage.delete(key: 'refresh_token');
    if (token != null) unawaited(api.revoke(token));
  }

  Future<void> _persist(Session session) async {
    final api = ref.read(apiProvider);
    api.accessToken = session.accessToken;
    api.refreshToken = session.refreshToken;
    await _storage.write(key: 'refresh_token', value: session.refreshToken);
  }
}
