import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:lifelink_mobile/models/domain.dart';
import 'package:lifelink_mobile/models/session.dart';
import 'package:lifelink_mobile/screens/home_screen.dart';
import 'package:lifelink_mobile/screens/hospital/request_detail_screen.dart';
import 'package:lifelink_mobile/services/api_client.dart';
import 'package:lifelink_mobile/services/auth_controller.dart';

BloodRequest _request(String status, {String urgency = 'Routine'}) =>
    BloodRequest(
        id: 'request-1',
        bloodType: 'APositive',
        quantityUnits: 2,
        urgency: urgency,
        status: status,
        requiredByUtc: '2026-10-06T08:00:00Z',
        notes: 'Theatre list');

Map<String, dynamic> _json(BloodRequest r) => {
      'id': r.id,
      'bloodType': r.bloodType,
      'quantityUnits': r.quantityUnits,
      'urgency': r.urgency,
      'status': r.status,
      'requiredByUtc': r.requiredByUtc,
      'notes': r.notes
    };

class _FakeApi extends ApiClient {
  _FakeApi({this.hospital});
  final Map<String, dynamic>? hospital;
  final puts = <String, Object>{};

  @override
  Future<dynamic> get(String path) async {
    if (path == '/api/hospitals/me') {
      if (hospital == null) {
        throw const ApiException(
            'This account is not linked to a hospital yet.', 404);
      }
      return hospital;
    }
    if (path.endsWith('/history')) return <dynamic>[];
    return {'items': <dynamic>[]};
  }

  @override
  Future<dynamic> put(String path, Object body) async {
    puts[path] = body;
    final input = body as Map<String, dynamic>;
    return {
      ..._json(_request('PendingApproval')),
      'quantityUnits': input['quantityUnits'],
      'requiredByUtc': input['requiredByUtc']
    };
  }
}

class _HospitalAuth extends AuthController {
  @override
  Future<Session?> build() async => const Session(
      accessToken: 'test',
      refreshToken: 'test',
      email: 'staff@example.test',
      role: 'HospitalRequester');
}

Future<void> _pump(WidgetTester tester, _FakeApi api, Widget page) async {
  tester.view.physicalSize = const Size(600, 1400);
  tester.view.devicePixelRatio = 1;
  addTearDown(tester.view.resetPhysicalSize);
  addTearDown(tester.view.resetDevicePixelRatio);
  await tester.pumpWidget(ProviderScope(
      overrides: [
        apiProvider.overrideWithValue(api),
        authProvider.overrideWith(_HospitalAuth.new)
      ],
      child: MaterialApp(
          home: Consumer(
              builder: (context, ref, _) => ref.watch(authProvider).when(
                  data: (_) => page,
                  loading: () => const SizedBox(),
                  error: (_, __) => const SizedBox())))));
  await tester.pumpAndSettle();
}

void main() {
  setUp(() => FlutterSecureStorage.setMockInitialValues({}));

  testWidgets('Unregistered hospital staff are asked to register first',
      (tester) async {
    await _pump(tester, _FakeApi(), const HomeScreen());
    expect(find.text('Register hospital'), findsOneWidget);
    expect(find.text('Blood requests'), findsNothing);
  });

  testWidgets('Suspended hospital sees its status and why requests are paused',
      (tester) async {
    await _pump(
        tester,
        _FakeApi(hospital: {
          'id': 'h1',
          'name': 'City Hospital',
          'registrationNumber': 'REG-001',
          'verificationStatus': 'Suspended'
        }),
        const HomeScreen());
    expect(find.text('City Hospital'), findsOneWidget);
    expect(find.textContaining('Suspended by the blood bank'), findsOneWidget);
    expect(find.text('Register hospital'), findsNothing);
    expect(find.text('Blood requests'), findsOneWidget);
  });

  for (final status in [
    'Approved',
    'Dispatched',
    'Fulfilled',
    'ClosedUnfulfilled'
  ]) {
    testWidgets('$status request offers no edit, escalate or withdraw',
        (tester) async {
      await _pump(
          tester, _FakeApi(), RequestDetailScreen(request: _request(status)));
      expect(find.text('Edit details'), findsNothing);
      expect(find.text('Escalate'), findsNothing);
      expect(find.text('Withdraw'), findsNothing);
    });
  }

  testWidgets(
      'Critical escalated request can be withdrawn but not re-escalated',
      (tester) async {
    await _pump(
        tester,
        _FakeApi(),
        RequestDetailScreen(
            request: _request('Escalated', urgency: 'Critical')));
    expect(find.text('Escalate'), findsNothing);
    expect(find.text('Withdraw'), findsOneWidget);
  });

  testWidgets('Request awaiting approval can be edited keeping its deadline',
      (tester) async {
    final api = _FakeApi();
    await _pump(
        tester, api, RequestDetailScreen(request: _request('PendingApproval')));
    expect(find.text('Escalate'), findsOneWidget);
    await tester.tap(find.text('Edit details'));
    await tester.pumpAndSettle();
    await tester.enterText(find.widgetWithText(TextFormField, 'Units'), '4');
    await tester.tap(find.text('Save changes'));
    await tester.pumpAndSettle();
    final body = api.puts['/api/requests/request-1'] as Map<String, dynamic>;
    expect(body['quantityUnits'], 4);
    expect(body['requiredByUtc'], '2026-10-06T08:00:00Z');
    expect(find.text('4 unit(s)'), findsOneWidget);
  });
}
