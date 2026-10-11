import 'package:flutter_test/flutter_test.dart';
import 'package:lifelink_mobile/screens/coordinator/coordinator_camps_screen.dart';

void main() {
  final now = DateTime(2026, 10, 11, 9);
  test('accepts a schedule long enough for every slot', () {
    expect(
        validateCampSchedule(now.add(const Duration(days: 1)),
            now.add(const Duration(days: 1, hours: 2)), 8, 15,
            now: now),
        isNull);
  });
  test('explains when slots do not fit in the camp', () {
    expect(
        validateCampSchedule(now.add(const Duration(days: 1)),
            now.add(const Duration(days: 1, hours: 1)), 10, 15,
            now: now),
        contains('150 minutes'));
  });
  test('rejects a start in the past', () {
    expect(
        validateCampSchedule(now.subtract(const Duration(hours: 1)),
            now.add(const Duration(hours: 1)), 1, 15,
            now: now),
        'Start time must be in the future.');
  });
}
