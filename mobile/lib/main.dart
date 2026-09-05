import 'core/app_theme.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:firebase_messaging/firebase_messaging.dart';
import 'screens/home_screen.dart';
import 'screens/login_screen.dart';
import 'services/auth_controller.dart';
import 'services/push_notification_service.dart';

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();
  if (PushNotificationService.enabled) {
    FirebaseMessaging.onBackgroundMessage(lifelinkFirebaseBackgroundHandler);
  }
  await restoreTheme();
  runApp(const ProviderScope(child: LifeLinkApp()));
}

class LifeLinkApp extends ConsumerWidget {
  const LifeLinkApp({super.key});
  @override
  Widget build(BuildContext context, WidgetRef ref) =>
      ValueListenableBuilder<ThemeMode>(
          valueListenable: appThemeMode,
          builder: (context, mode, _) => MaterialApp(
              debugShowCheckedModeBanner: false,
              title: 'LifeLink',
              theme: lifeLinkTheme(Brightness.light),
              darkTheme: lifeLinkTheme(Brightness.dark),
              themeMode: mode,
              themeAnimationDuration: const Duration(milliseconds: 240),
              home: ref.watch(authProvider).when(
                  data: (session) => session == null
                      ? const LoginScreen()
                      : const HomeScreen(),
                  loading: () => const Scaffold(
                      body: Center(child: CircularProgressIndicator())),
                  error: (_, __) => const LoginScreen())));
}
