/// Client-side mirrors of the API validation rules. The server remains the
/// source of truth; these give immediate, specific feedback before a request.
class Validators {
  Validators._();

  static final _email = RegExp(r'^[^@\s]+@[^@\s]+\.[^@\s]+$');

  static String? required(String? value, String label, {int? max}) {
    final v = value?.trim() ?? '';
    if (v.isEmpty) return '$label is required.';
    if (max != null && v.length > max) {
      return '$label must be at most $max characters.';
    }
    return null;
  }

  static String? email(String? value) {
    final v = value?.trim() ?? '';
    if (v.isEmpty) return 'Email is required.';
    if (!_email.hasMatch(v)) {
      return 'Enter a valid email address, for example name@example.com.';
    }
    return null;
  }

  /// Sign-in only checks presence; strength rules apply at registration.
  static String? passwordPresent(String? value) =>
      (value == null || value.isEmpty) ? 'Password is required.' : null;

  static String? strongPassword(String? value) {
    final v = value ?? '';
    if (v.isEmpty) return 'Password is required.';
    final problems = <String>[
      if (v.length < 12) 'at least 12 characters',
      if (!RegExp('[A-Z]').hasMatch(v)) 'an uppercase letter',
      if (!RegExp('[a-z]').hasMatch(v)) 'a lowercase letter',
      if (!RegExp('[0-9]').hasMatch(v)) 'a digit',
      if (!RegExp(r'[^A-Za-z0-9]').hasMatch(v)) 'a symbol',
    ];
    return problems.isEmpty ? null : 'Password needs ${problems.join(', ')}.';
  }

  static String? Function(String?) matches(TextEditingValueGetter other) =>
      (value) => value == other() ? null : 'Passwords do not match.';

  /// YYYY-MM-DD, a real calendar date, not in the future, donor at least 18
  /// and born no more than 120 years ago (mirrors DonorService.Validate).
  static String? dateOfBirth(String? value, {DateTime? now}) {
    final v = value?.trim() ?? '';
    if (v.isEmpty) return 'Date of birth is required.';
    final parsed = parseIsoDate(v);
    if (parsed == null) return 'Use the format YYYY-MM-DD.';
    final today = now ?? DateTime.now();
    if (parsed.isAfter(today)) return 'Date of birth cannot be in the future.';
    var age = today.year - parsed.year;
    if (today.month < parsed.month ||
        (today.month == parsed.month && today.day < parsed.day)) {
      age--;
    }
    if (age < 18) return 'Donors must be at least 18 years old.';
    if (parsed.isBefore(DateTime(today.year - 120, today.month, today.day))) {
      return 'Date of birth cannot be more than 120 years ago.';
    }
    return null;
  }

  static String? integerInRange(String? value, String label, int min, int max) {
    final n = int.tryParse(value?.trim() ?? '');
    if (n == null) return '$label must be a whole number.';
    if (n < min || n > max) return '$label must be between $min and $max.';
    return null;
  }

  /// Strict YYYY-MM-DD parse that rejects rolled-over dates such as 2024-02-31.
  static DateTime? parseIsoDate(String value) {
    final m = RegExp(r'^(\d{4})-(\d{2})-(\d{2})$').firstMatch(value);
    if (m == null) return null;
    final y = int.parse(m[1]!), mo = int.parse(m[2]!), d = int.parse(m[3]!);
    final date = DateTime(y, mo, d);
    return date.year == y && date.month == mo && date.day == d ? date : null;
  }
}

typedef TextEditingValueGetter = String Function();
