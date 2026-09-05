import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:lifelink_mobile/screens/login_screen.dart';
import 'package:lifelink_mobile/screens/register_screen.dart';

Widget _host(Widget child) => ProviderScope(child: MaterialApp(home: child));

void main() {
  // No stored refresh token, so session restore settles to signed-out instead
  // of hanging on the platform channel and leaving the form in a loading state.
  setUp(() => FlutterSecureStorage.setMockInitialValues({}));

  testWidgets('login shows field messages and does not call the API',
      (tester) async {
    await tester.pumpWidget(_host(const LoginScreen()));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Sign in'));
    await tester.pump();
    expect(find.text('Email is required.'), findsOneWidget);
    expect(find.text('Password is required.'), findsOneWidget);
  });

  testWidgets('register explains a weak password and mismatched confirmation',
      (tester) async {
    await tester.pumpWidget(_host(const RegisterScreen()));
    await tester.pumpAndSettle();
    await tester.enterText(
        find.byType(TextFormField).at(0), 'me@lifelink.test');
    await tester.enterText(find.byType(TextFormField).at(1), 'weak');
    await tester.enterText(find.byType(TextFormField).at(2), 'other');
    // The submit button is below the fold of the lazily-built ListView.
    final submit = find.widgetWithText(FilledButton, 'Create account');
    await tester.scrollUntilVisible(submit, 200,
        scrollable: find.byType(Scrollable).first);
    await tester.tap(submit);
    await tester.pump();
    expect(find.textContaining('at least 12 characters'), findsOneWidget);
    expect(find.text('Passwords do not match.'), findsOneWidget);
  });
}
