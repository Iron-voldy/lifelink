import 'package:flutter/material.dart';
import 'package:flutter_map/flutter_map.dart';
import 'package:latlong2/latlong.dart';

/// Great-circle distance in kilometres between two points.
double distanceKm(LatLng a, LatLng b) =>
    const Distance().as(LengthUnit.Meter, a, b) / 1000;

/// Small OpenStreetMap view of a donation camp and the donor ("you"), joined by
/// a dashed line, framed so both pins are visible.
class CampMap extends StatelessWidget {
  const CampMap(
      {super.key,
      required this.camp,
      required this.campName,
      this.you,
      this.youLabel = 'You',
      this.height = 220});
  final LatLng camp;
  final String campName;
  final LatLng? you;
  final String youLabel;
  final double height;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    final points = [camp, if (you != null) you!];
    final fit = points.length > 1 && distanceKm(camp, you!) > 0.05
        ? CameraFit.bounds(
            bounds: LatLngBounds.fromPoints(points),
            padding: const EdgeInsets.all(36),
            maxZoom: 15)
        : CameraFit.coordinates(coordinates: [camp], maxZoom: 14);
    return Semantics(
      label: you == null
          ? 'Map showing $campName'
          : 'Map showing $campName, ${distanceKm(camp, you!).toStringAsFixed(1)} km from you',
      child: ClipRRect(
        borderRadius: BorderRadius.circular(16),
        child: SizedBox(
          height: height,
          child: FlutterMap(
            // A new key re-frames the camera when "you" moves to the phone's position.
            key: ValueKey('${you?.latitude},${you?.longitude}'),
            options: MapOptions(
                initialCameraFit: fit,
                interactionOptions: const InteractionOptions(
                    flags: InteractiveFlag.all & ~InteractiveFlag.rotate)),
            children: [
              TileLayer(
                  urlTemplate: 'https://tile.openstreetmap.org/{z}/{x}/{y}.png',
                  userAgentPackageName: 'lk.lifelink.mobile'),
              if (you != null)
                PolylineLayer(polylines: [
                  Polyline(
                      points: [you!, camp],
                      color: scheme.error,
                      strokeWidth: 3,
                      pattern: StrokePattern.dashed(segments: const [8, 8]))
                ]),
              MarkerLayer(markers: [
                Marker(
                    point: camp,
                    width: 44,
                    height: 44,
                    alignment: Alignment.topCenter,
                    child: Tooltip(
                        message: campName,
                        child: Icon(Icons.location_on,
                            size: 44, color: scheme.error))),
                if (you != null)
                  Marker(
                      point: you!,
                      width: 24,
                      height: 24,
                      child: Tooltip(
                          message: youLabel,
                          child: Container(
                              decoration: BoxDecoration(
                                  color: const Color(0xFF2F6FD6),
                                  shape: BoxShape.circle,
                                  border:
                                      Border.all(color: Colors.white, width: 3),
                                  boxShadow: const [
                                BoxShadow(blurRadius: 6, color: Colors.black38)
                              ])))),
              ]),
              const RichAttributionWidget(
                  showFlutterMapAttribution: false,
                  attributions: [
                    TextSourceAttribution('OpenStreetMap contributors')
                  ]),
            ],
          ),
        ),
      ),
    );
  }
}

/// Legend row explaining the two pins.
class CampMapLegend extends StatelessWidget {
  const CampMapLegend({super.key, required this.youLabel});
  final String youLabel;
  @override
  Widget build(BuildContext context) {
    final style = Theme.of(context).textTheme.bodySmall;
    return Wrap(spacing: 16, runSpacing: 4, children: [
      Row(mainAxisSize: MainAxisSize.min, children: [
        Icon(Icons.location_on,
            size: 18, color: Theme.of(context).colorScheme.error),
        const SizedBox(width: 4),
        Text('Camp', style: style)
      ]),
      Row(mainAxisSize: MainAxisSize.min, children: [
        const Icon(Icons.circle, size: 14, color: Color(0xFF2F6FD6)),
        const SizedBox(width: 4),
        Text(youLabel, style: style)
      ]),
    ]);
  }
}
