import 'package:flutter_test/flutter_test.dart';
import 'package:lifelink_mobile/core/format.dart';
import 'package:lifelink_mobile/core/validators.dart';

void main() {
  group('email', () {
    test('accepts a normal address', () {
      expect(Validators.email(' donor@lifelink.test '), isNull);
    });
    test('rejects blank and malformed', () {
      expect(Validators.email(''), 'Email is required.');
      expect(Validators.email('not-an-email'), contains('valid email'));
    });
  });

  group('strongPassword', () {
    test('accepts a strong password', () {
      expect(Validators.strongPassword('Str0ng!Passw0rd'), isNull);
    });
    test('lists every missing requirement', () {
      final msg = Validators.strongPassword('short')!;
      expect(msg, contains('12 characters'));
      expect(msg, contains('uppercase'));
      expect(msg, contains('digit'));
      expect(msg, contains('symbol'));
      expect(msg, isNot(contains('lowercase')));
    });
  });

  test('matches compares against the live value', () {
    var other = 'abc';
    final v = Validators.matches(() => other);
    expect(v('abc'), isNull);
    expect(v('abd'), 'Passwords do not match.');
    other = 'abd';
    expect(v('abd'), isNull);
  });

  group('dateOfBirth', () {
    final now = DateTime(2026, 9, 28);
    test('accepts an adult date', () {
      expect(Validators.dateOfBirth('1995-05-20', now: now), isNull);
    });
    test('rejects bad format, impossible, future, and under-age dates', () {
      expect(Validators.dateOfBirth('20/05/1995', now: now),
          contains('YYYY-MM-DD'));
      expect(Validators.dateOfBirth('2024-02-31', now: now),
          contains('YYYY-MM-DD'));
      expect(
          Validators.dateOfBirth('2030-01-01', now: now), contains('future'));
      expect(Validators.dateOfBirth('2015-01-01', now: now), contains('18'));
    });
    test('age is exact on the birthday boundary', () {
      expect(Validators.dateOfBirth('2008-09-28', now: now), isNull);
      expect(Validators.dateOfBirth('2008-09-29', now: now), contains('18'));
    });
  });

  group('integerInRange', () {
    test('enforces the 1-100 unit rule', () {
      expect(Validators.integerInRange('1', 'Units', 1, 100), isNull);
      expect(Validators.integerInRange('100', 'Units', 1, 100), isNull);
      expect(Validators.integerInRange('0', 'Units', 1, 100),
          contains('between 1 and 100'));
      expect(Validators.integerInRange('101', 'Units', 1, 100),
          contains('between 1 and 100'));
      expect(Validators.integerInRange('x', 'Units', 1, 100),
          contains('whole number'));
    });
  });

  test('required trims and enforces max length', () {
    expect(Validators.required('   ', 'Address'), 'Address is required.');
    expect(
        Validators.required('a' * 11, 'Name', max: 10), contains('at most 10'));
    expect(Validators.required(' ok ', 'Name', max: 10), isNull);
  });

  group('format', () {
    test('bloodLabel and humanize', () {
      expect(bloodLabel('ABNegative'), 'AB-');
      expect(bloodLabel('OPositive'), 'O+');
      expect(humanize('TemporarilyIneligible'), 'Temporarily ineligible');
      expect(humanize('Eligible'), 'Eligible');
    });
    test('formatDateTime falls back to the raw text when unparseable', () {
      expect(formatDateTime('garbage'), 'garbage');
    });
  });
}
