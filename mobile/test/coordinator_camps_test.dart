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
  test('rejects a 12:57 AM start that should have been PM', () {
    final day =
        DateTime.utc(2026, 10, 11, 18, 30); // midnight 12 Oct in Sri Lanka
    expect(
        validateCampSchedule(day.add(const Duration(minutes: 57)),
            day.add(const Duration(hours: 17, minutes: 57)), 15, 15,
            now: now),
        contains('Check AM/PM'));
  });
  test('accepts a 1 PM to 5 PM camp in Sri Lanka time', () {
    final day = DateTime.utc(2026, 10, 11, 18, 30);
    expect(
        validateCampSchedule(day.add(const Duration(hours: 13)),
            day.add(const Duration(hours: 17)), 16, 15,
            now: now),
        isNull);
  });
}
