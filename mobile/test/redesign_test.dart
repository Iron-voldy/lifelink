import 'dart:io';
import 'dart:ui' as ui;
import 'package:flutter/material.dart';
import 'package:flutter/rendering.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:lifelink_mobile/core/app_theme.dart';
import 'package:lifelink_mobile/models/domain.dart';
import 'package:lifelink_mobile/models/session.dart';
import 'package:lifelink_mobile/screens/login_screen.dart';
import 'package:lifelink_mobile/screens/register_screen.dart';
import 'package:lifelink_mobile/screens/home_screen.dart';
import 'package:lifelink_mobile/screens/donor/profile_screen.dart';
import 'package:lifelink_mobile/screens/camps/camps_screen.dart';
import 'package:lifelink_mobile/screens/camps/camp_detail_screen.dart';
import 'package:lifelink_mobile/screens/hospital/requests_screen.dart';
import 'package:lifelink_mobile/screens/hospital/request_detail_screen.dart';
import 'package:lifelink_mobile/screens/hospital/hospital_registration_screen.dart';
import 'package:lifelink_mobile/screens/notifications_screen.dart';
import 'package:lifelink_mobile/services/api_client.dart';
import 'package:lifelink_mobile/services/auth_controller.dart';

const _camp = Camp(
    id: 'camp-1',
    name: 'Colombo community donation day',
    location: 'Colombo Town Hall',
    startsAtUtc: '2026-10-04T08:00:00Z',
    availableSlots: 24);
const _request = BloodRequest(
    id: 'request-1',
    bloodType: 'OPositive',
    quantityUnits: 4,
    urgency: 'Critical',
    status: 'Submitted',
    requiredByUtc: '2026-10-04T08:00:00Z',
    notes: 'Blood required for patient care.');

class _ReviewApi extends ApiClient {
  @override
  Future<dynamic> get(String path) async {
    if (path == '/api/donors/me') {
      return {
        'id': 'donor-1',
        'bloodType': 'OPositive',
        'dateOfBirth': '1995-04-12',
        'address': 'Colombo, Western Province',
        'eligibilityStatus': 'TemporarilyIneligible',
        'medicalFlags': []
      };
    }
    if (path.contains('/slots')) {
      return [
        {
          'id': 'slot-1',
          'slotTimeUtc': '2026-10-04T08:00:00Z',
          'status': 'Available'
        }
      ];
    }
    if (path.contains('/camps?')) {
      return {
        'items': [
          {
            'id': _camp.id,
            'name': _camp.name,
            'location': _camp.location,
            'startsAtUtc': _camp.startsAtUtc,
            'availableSlots': 24
          }
        ]
      };
    }
    if (path == '/api/requests') {
      return {
        'items': [
          {
            'id': _request.id,
            'bloodType': _request.bloodType,
            'quantityUnits': 4,
            'urgency': 'Critical',
            'status': 'Submitted',
            'requiredByUtc': _request.requiredByUtc
          }
        ]
      };
    }
    if (path == '/api/notifications/history') {
      return [
        {
          'type': 'DonationReminder',
          'status': 'Sent',
          'createdAtUtc': '2026-09-29T08:00:00Z'
        }
      ];
    }
    return [];
  }
}

class _ReviewAuth extends AuthController {
  @override
  Future<Session?> build() async => const Session(
      accessToken: 'test',
      refreshToken: 'test',
      email: 'donor@example.test',
      role: 'Donor');
}

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();
  setUpAll(() async {
    final loader = FontLoader('DMSans')
      ..addFont(rootBundle.load('assets/fonts/dm-sans-400.ttf'));
    await loader.load();
    final icons = FontLoader('MaterialIcons')
      ..addFont(rootBundle.load('fonts/MaterialIcons-Regular.otf'));
    await icons.load();
  });
  setUp(() {
    FlutterSecureStorage.setMockInitialValues({});
    TestDefaultBinaryMessengerBinding.instance.defaultBinaryMessenger
        .setMockMethodCallHandler(
            const MethodChannel('flutter.baseflow.com/geolocator'),
            (call) async => 0);
    appThemeMode.value = ThemeMode.system;
  });
  final pages = <String, Widget>{
    'login': const LoginScreen(),
    'register': const RegisterScreen(),
    'home': const HomeScreen(),
    'profile': const DonorProfileScreen(),
    'camps': const CampsScreen(),
    'camp-detail': const CampDetailScreen(camp: _camp),
    'requests': const RequestsScreen(),
    'request-detail': const RequestDetailScreen(request: _request),
    'hospital-registration': const HospitalRegistrationScreen(),
    'notifications': const NotificationsScreen()
  };
  for (final width in [320.0, 390.0, 820.0]) {
    for (final brightness in Brightness.values) {
      testWidgets(
          'All mobile screens fit $width in ${brightness.name} with enlarged text',
          (tester) async {
        tester.view.physicalSize = Size(width, 1000);
        tester.view.devicePixelRatio = 1;
        addTearDown(tester.view.resetPhysicalSize);
        addTearDown(tester.view.resetDevicePixelRatio);
        for (final page in pages.entries) {
          final key = GlobalKey();
          await tester.pumpWidget(ProviderScope(
              overrides: [
                apiProvider.overrideWithValue(_ReviewApi()),
                authProvider.overrideWith(_ReviewAuth.new)
              ],
              child: MaterialApp(
                  theme: lifeLinkTheme(brightness),
                  builder: (context, child) => MediaQuery(
                      data: MediaQuery.of(context).copyWith(
                          textScaler: const TextScaler.linear(1.3),
                          disableAnimations: true),
                      child: child!),
                  home: Consumer(
                      builder: (context, ref, _) => ref
                          .watch(authProvider)
                          .when(
                              data: (_) =>
                                  RepaintBoundary(key: key, child: page.value),
                              loading: () => const SizedBox(),
                              error: (_, __) => const SizedBox())))));
          await tester.pumpAndSettle();
          expect(tester.takeException(), isNull, reason: page.key);
          expect(
              find.byTooltip(brightness == Brightness.dark
                  ? 'Switch to light mode'
                  : 'Switch to dark mode'),
              findsOneWidget,
              reason: page.key);
          if (const bool.fromEnvironment('CAPTURE_UI')) {
            await tester.runAsync(() async {
              final boundary = key.currentContext!.findRenderObject()!
                  as RenderRepaintBoundary;
              final image = await boundary.toImage(pixelRatio: 1);
              final bytes =
                  await image.toByteData(format: ui.ImageByteFormat.png);
              image.dispose();
              final file = File(
                  'build/ui-review/${width.toInt()}-${brightness.name}-${page.key}.png');
              await file.parent.create(recursive: true);
              await file.writeAsBytes(bytes!.buffer.asUint8List());
            });
          }
          final scroll = find.byType(Scrollable);
          if (scroll.evaluate().isNotEmpty) {
            await tester.drag(scroll.first, const Offset(0, -650));
            await tester.pumpAndSettle();
            expect(tester.takeException(), isNull,
                reason: '${page.key} after scroll');
          }
          await tester.pumpWidget(const SizedBox());
          await tester.pumpAndSettle();
        }
      });
    }
  }
  testWidgets('Theme switch persists through restore and logout',
      (tester) async {
    await tester.pumpWidget(ProviderScope(
        child: ValueListenableBuilder<ThemeMode>(
            valueListenable: appThemeMode,
            builder: (context, mode, _) => MaterialApp(
                theme: lifeLinkTheme(Brightness.light),
                darkTheme: lifeLinkTheme(Brightness.dark),
                themeMode: mode,
                home: const LoginScreen()))));
    await tester.pumpAndSettle();
    await tester.tap(find.byTooltip('Switch to dark mode'));
    await tester.pumpAndSettle();
    expect(appThemeMode.value, ThemeMode.dark);
    appThemeMode.value = ThemeMode.light;
    await restoreTheme();
    expect(appThemeMode.value, ThemeMode.dark);
    final context = tester.element(find.byType(LoginScreen));
    await ProviderScope.containerOf(context)
        .read(authProvider.notifier)
        .logout();
    appThemeMode.value = ThemeMode.light;
    await restoreTheme();
    expect(appThemeMode.value, ThemeMode.dark);
  });
}
