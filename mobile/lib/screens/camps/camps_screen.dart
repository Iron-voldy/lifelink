import '../../widgets/life_scaffold.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:geolocator/geolocator.dart';

import '../../core/format.dart';
import '../../models/domain.dart';
import '../../services/auth_controller.dart';
import '../../services/lifelink_service.dart';
import 'camp_detail_screen.dart';

class CampsScreen extends ConsumerStatefulWidget {
  const CampsScreen({super.key});

  @override
  ConsumerState<CampsScreen> createState() => _CampsScreenState();
}

class _CampsScreenState extends ConsumerState<CampsScreen> {
  List<Camp> camps = [];
  Position? position;
  bool loading = true;
  String? message;

  LifeLinkService get service => LifeLinkService(ref.read(apiProvider));

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    try {
      camps = await service.camps();
      message = null;
      try {
        final permission = await Geolocator.checkPermission();
        if (permission == LocationPermission.whileInUse ||
            permission == LocationPermission.always) {
          position = await Geolocator.getCurrentPosition();
        }
      } catch (_) {
        // Distance sorting is optional; camps are still listed by date.
        position = null;
      }
      if (position != null) {
        camps.sort((a, b) => _distance(a).compareTo(_distance(b)));
      }
    } catch (error) {
      message = error.toString();
    } finally {
      if (mounted) setState(() => loading = false);
    }
  }

  double _distance(Camp camp) {
    if (camp.latitude == null || camp.longitude == null || position == null) {
      return double.infinity;
    }
    return Geolocator.distanceBetween(
          position!.latitude,
          position!.longitude,
          camp.latitude!,
          camp.longitude!,
        ) /
        1000;
  }

  Future<void> _open(Camp camp) async {
    final distance = _distance(camp);
    await Navigator.push(
        context,
        MaterialPageRoute(
            builder: (_) => CampDetailScreen(
                camp: camp, distanceKm: distance.isFinite ? distance : null)));
    if (mounted) await _load();
  }

  @override
  Widget build(BuildContext context) {
    return LifeScaffold(
      appBar: AppBar(title: const Text('Donation camps')),
      body: loading
          ? const Center(child: CircularProgressIndicator())
          : RefreshIndicator(
              onRefresh: _load,
              child: ListView(
                physics: const AlwaysScrollableScrollPhysics(),
                padding: const EdgeInsets.all(16),
                children: [
                  const PageIntro(
                      title: 'A little time. A lasting difference.',
                      subtitle:
                          'Find your next donation day and make room for something meaningful.',
                      icon: Icons.event_available_outlined,
                      photo: true,
                      imagePath: 'assets/images/donation.jpg'),
                  if (message != null)
                    Text(message!,
                        style: TextStyle(
                            color: Theme.of(context).colorScheme.error)),
                  if (message == null && camps.isEmpty)
                    const Padding(
                        padding: EdgeInsets.all(32),
                        child: Center(
                            child: Text(
                                'No scheduled camps right now. Pull down to refresh.'))),
                  ...camps.map((camp) {
                    final distance = _distance(camp);
                    final distanceText = distance.isFinite
                        ? ' · ${distance.toStringAsFixed(1)} km'
                        : '';
                    return Card(
                      child: ListTile(
                        onTap: () => _open(camp),
                        title: Text(camp.name),
                        subtitle: Text(
                          '${camp.location}\n${formatDateTime(camp.startsAtUtc)}$distanceText',
                        ),
                        isThreeLine: true,
                        trailing: Column(
                            mainAxisAlignment: MainAxisAlignment.center,
                            children: [
                              Text(camp.availableSlots == 0
                                  ? 'Full'
                                  : '${camp.availableSlots}'),
                              if (camp.availableSlots > 0)
                                const Text('open',
                                    style: TextStyle(fontSize: 11))
                            ]),
                      ),
                    );
                  }),
                ],
              ),
            ),
    );
  }
}
