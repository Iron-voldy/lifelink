import 'dart:convert';

import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:lifelink_mobile/core/validators.dart';
import 'package:lifelink_mobile/services/api_client.dart';

void main() {
  group('donor date of birth matches the API rules', () {
    final now = DateTime(2026, 10, 4);
    test('requires donors to be at least 18', () {
      expect(Validators.dateOfBirth('2008-10-04', now: now), isNull);
      expect(Validators.dateOfBirth('2008-10-05', now: now),
          'Donors must be at least 18 years old.');
      expect(Validators.dateOfBirth('2016-01-01', now: now),
          'Donors must be at least 18 years old.');
    });
    test('rejects dates more than 120 years ago', () {
      expect(Validators.dateOfBirth('1906-10-04', now: now), isNull);
      expect(Validators.dateOfBirth('1906-10-03', now: now),
          'Date of birth cannot be more than 120 years ago.');
      expect(Validators.dateOfBirth('0001-01-01', now: now),
          'Date of birth cannot be more than 120 years ago.');
    });
  });

  group('push token on sign-out', () {
    test('unregisterDevice deletes the token for the signed-in user once',
        () async {
      final calls = <http.Request>[];
      final client = MockClient((request) async {
        calls.add(request);
        return http.Response('', 204);
      });
      await http.runWithClient(() async {
        final api = ApiClient(baseUrl: 'https://api.test')
          ..accessToken = 'access'
          ..deviceToken = 'fcm-token';
        await api.unregisterDevice();
        await api.unregisterDevice();
        expect(api.deviceToken, isNull);
      }, () => client);
      expect(calls, hasLength(1));
      expect(calls.single.method, 'DELETE');
      expect(calls.single.url.path, '/api/notifications/devices');
      expect(calls.single.headers['Authorization'], 'Bearer access');
      expect(jsonDecode(calls.single.body), {'token': 'fcm-token'});
    });

    test('a server failure never blocks sign-out', () async {
      await http.runWithClient(() async {
        final api = ApiClient(baseUrl: 'https://api.test')
          ..accessToken = 'access'
          ..deviceToken = 'fcm-token';
        await api.unregisterDevice();
        expect(api.deviceToken, isNull);
      }, () => MockClient((_) async => http.Response('{}', 500)));
    });
  });
}
