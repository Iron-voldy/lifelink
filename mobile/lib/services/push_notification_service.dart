import 'dart:async';
import 'package:firebase_core/firebase_core.dart';
import 'package:firebase_messaging/firebase_messaging.dart';
import 'api_client.dart';

@pragma('vm:entry-point')
Future<void> lifelinkFirebaseBackgroundHandler(RemoteMessage message) async {
  await Firebase.initializeApp();
}

class PushNotificationService {
  PushNotificationService(this.api);
  final ApiClient api;
  static const enabled =
      bool.fromEnvironment('LIFELINK_FIREBASE_ENABLED', defaultValue: false);
  StreamSubscription<String>? _tokenSubscription;
  StreamSubscription<RemoteMessage>? _foregroundSubscription;
  final _messages = StreamController<RemoteMessage>.broadcast();
  Stream<RemoteMessage> get messages => _messages.stream;

  Future<void> start() async {
    if (!enabled) return;
    await Firebase.initializeApp();
    final messaging = FirebaseMessaging.instance;
    final settings = await messaging.requestPermission(
        alert: true, badge: true, sound: true);
    if (settings.authorizationStatus == AuthorizationStatus.denied) return;
    final token = await messaging.getToken();
    if (token != null) await _register(token);
    _tokenSubscription =
        messaging.onTokenRefresh.listen((token) => _register(token));
    _foregroundSubscription = FirebaseMessaging.onMessage.listen(_messages.add);
    FirebaseMessaging.onMessageOpenedApp.listen(_messages.add);
  }

  Future<void> _register(String token) async {
    await api.post(
        '/api/notifications/devices', {'token': token, 'platform': 'android'});
    api.deviceToken = token;
  }
  Future<void> dispose() async {
    await _tokenSubscription?.cancel();
    await _foregroundSubscription?.cancel();
    await _messages.close();
  }
}
