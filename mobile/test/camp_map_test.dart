import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:latlong2/latlong.dart';
import 'package:lifelink_mobile/widgets/camp_map.dart';

void main() {
  test('distance between Colombo and Kandy is about 94 km', () {
    expect(
        distanceKm(
            const LatLng(6.9271, 79.8612), const LatLng(7.2906, 80.6337)),
        closeTo(94, 1.5));
  });

  testWidgets('map announces the camp and the distance to the donor',
      (tester) async {
    await tester.pumpWidget(const MaterialApp(
        home: Scaffold(
            body: CampMap(
                camp: LatLng(7.2906, 80.6337),
                campName: 'Kandy camp',
                you: LatLng(6.9271, 79.8612)))));
    expect(find.bySemanticsLabel(RegExp(r'Kandy camp, 9\d\.\d km from you')),
        findsOneWidget);
  });
}
