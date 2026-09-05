import 'dart:convert';

class Session {
  const Session(
      {required this.accessToken,
      required this.refreshToken,
      required this.email,
      required this.role});
  final String accessToken;
  final String refreshToken;
  final String email;
  final String role;

  factory Session.fromJson(Map<String, dynamic> json) {
    final accessToken = json['accessToken'] as String;
    final parts = accessToken.split('.');
    final claims =
        jsonDecode(utf8.decode(base64Url.decode(base64Url.normalize(parts[1]))))
            as Map<String, dynamic>;
    final role = claims['role'] ??
        claims['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'];
    final email = claims['email'] ??
        claims[
            'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress'];
    return Session(
        accessToken: accessToken,
        refreshToken: json['refreshToken'] as String,
        email: email as String,
        role: role as String);
  }
}
