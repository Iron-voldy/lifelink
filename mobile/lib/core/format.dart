import 'package:flutter/material.dart';

const bloodTypes = [
  'APositive',
  'ANegative',
  'BPositive',
  'BNegative',
  'ABPositive',
  'ABNegative',
  'OPositive',
  'ONegative',
];

/// `OPositive` -> `O+`.
String bloodLabel(String type) =>
    type.replaceAll('Positive', '+').replaceAll('Negative', '-');

/// `TemporarilyIneligible` -> `Temporarily ineligible`.
String humanize(String pascal) {
  final spaced = pascal
      .replaceAllMapped(RegExp(r'(?<=[a-z])(?=[A-Z])'), (_) => ' ')
      .toLowerCase();
  return spaced.isEmpty
      ? spaced
      : '${spaced[0].toUpperCase()}${spaced.substring(1)}';
}

String formatDateTime(String iso) {
  final d = DateTime.tryParse(iso)?.toLocal();
  if (d == null) return iso;
  String two(int n) => n.toString().padLeft(2, '0');
  return '${d.year}-${two(d.month)}-${two(d.day)} ${two(d.hour)}:${two(d.minute)}';
}

Color urgencyColor(BuildContext context, String urgency) {
  switch (urgency) {
    case 'Critical':
      return Theme.of(context).colorScheme.error;
    case 'Urgent':
      return Theme.of(context).brightness == Brightness.dark
          ? Colors.orange.shade200
          : Colors.orange.shade800;
    default:
      return Theme.of(context).colorScheme.primary;
  }
}
