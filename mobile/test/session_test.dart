import 'dart:convert';
import 'package:flutter_test/flutter_test.dart';
import 'package:lifelink_mobile/models/session.dart';

void main() {
  test('reads identity and role from access token claims', () {
    String part(Object value) =>
        base64Url.encode(utf8.encode(jsonEncode(value))).replaceAll('=', '');
    final token = '${part({'alg': 'none'})}.${part({
          'email': 'donor@lifelink.test',
          'role': 'Donor'
        })}.';
    final session =
        Session.fromJson({'accessToken': token, 'refreshToken': 'refresh'});
    expect(session.email, 'donor@lifelink.test');
    expect(session.role, 'Donor');
    expect(session.refreshToken, 'refresh');
  });
}
