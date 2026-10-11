import '../../widgets/life_scaffold.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:geolocator/geolocator.dart';
import 'package:latlong2/latlong.dart';
import '../../core/format.dart';
import '../../models/domain.dart';
import '../../services/auth_controller.dart';
import '../../services/lifelink_service.dart';
import '../../widgets/camp_map.dart';

class CampDetailScreen extends ConsumerStatefulWidget {
  const CampDetailScreen({super.key, required this.camp, this.distanceKm});
  final Camp camp;
  final double? distanceKm;
  @override
  ConsumerState<CampDetailScreen> createState() => _CampDetailScreenState();
}

class _CampDetailScreenState extends ConsumerState<CampDetailScreen> {
  List<CampSlot> slots = [];
  DonorProfile? donor;
  String? selectedSlotId, error, notice, locationMessage;
  bool loading = true, busy = false, locating = false;

  /// The phone's position once the donor shares it; otherwise the saved address is used.
  LatLng? here;

  LatLng? get you =>
      here ??
      (donor?.latitude != null && donor?.longitude != null
          ? LatLng(donor!.latitude!, donor!.longitude!)
          : null);

  Future<void> _locate() async {
    setState(() {
      locating = true;
      locationMessage = null;
    });
    try {
      if (!await Geolocator.isLocationServiceEnabled()) {
        locationMessage =
            'Location services are off, so the map uses your saved address.';
        return;
      }
      var permission = await Geolocator.checkPermission();
      if (permission == LocationPermission.denied) {
        permission = await Geolocator.requestPermission();
      }
      if (permission == LocationPermission.denied ||
          permission == LocationPermission.deniedForever) {
        locationMessage =
            'Location permission denied, so the map uses your saved address.';
        return;
      }
      final p = await Geolocator.getCurrentPosition();
      here = LatLng(p.latitude, p.longitude);
    } catch (_) {
      locationMessage = 'Could not get your location right now.';
    } finally {
      if (mounted) setState(() => locating = false);
    }
  }

  LifeLinkService get service => LifeLinkService(ref.read(apiProvider));

  CampSlot? get mine => donor == null
      ? null
      : slots
          .where((s) => s.donorId == donor!.id && s.status == 'Booked')
          .firstOrNull;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    try {
      final results = await Future.wait<Object?>([
        service.campSlots(widget.camp.id),
        service.myDonorProfile(),
      ]);
      slots = results[0] as List<CampSlot>;
      donor = results[1] as DonorProfile?;
      if (selectedSlotId != null &&
          !slots.any((s) => s.id == selectedSlotId && s.isOpen)) {
        selectedSlotId = null;
      }
      error = null;
    } catch (e) {
      error = e.toString();
    } finally {
      if (mounted) setState(() => loading = false);
    }
  }

  Future<void> _book() async {
    setState(() {
      busy = true;
      error = null;
      notice = null;
    });
    try {
      await service.bookCamp(widget.camp.id, slotId: selectedSlotId);
      notice = 'Your slot is booked.';
      selectedSlotId = null;
    } catch (e) {
      error = e.toString();
    }
    await _load();
    if (mounted) setState(() => busy = false);
  }

  Future<void> _cancel(CampSlot slot) async {
    final ok = await showDialog<bool>(
        context: context,
        builder: (ctx) => AlertDialog(
                title: const Text('Cancel your booking?'),
                content: Text(
                    'Your ${formatDateTime(slot.slotTimeUtc)} slot will be released for other donors.'),
                actions: [
                  TextButton(
                      onPressed: () => Navigator.pop(ctx, false),
                      child: const Text('Keep booking')),
                  FilledButton(
                      onPressed: () => Navigator.pop(ctx, true),
                      child: const Text('Cancel booking'))
                ]));
    if (ok != true) return;
    setState(() {
      busy = true;
      error = null;
      notice = null;
    });
    try {
      await service.cancelBooking(widget.camp.id, slot.id);
      notice = 'Booking cancelled.';
    } catch (e) {
      error = e.toString();
    }
    await _load();
    if (mounted) setState(() => busy = false);
  }

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    final booking = mine;
    final open = slots.where((s) => s.isOpen).toList();
    return LifeScaffold(
        appBar: AppBar(title: Text(widget.camp.name)),
        body: loading
            ? const Center(child: CircularProgressIndicator())
            : RefreshIndicator(
                onRefresh: _load,
                child: ListView(
                    physics: const AlwaysScrollableScrollPhysics(),
                    padding: const EdgeInsets.all(20),
                    children: [
                      const PageIntro(
                          title: 'Make your next connection.',
                          subtitle:
                              'Choose a time that works for you. Your contribution matters.',
                          icon: Icons.water_drop_outlined,
                          photo: true,
                          imagePath: 'assets/images/donation.jpg'),
                      ListTile(
                          contentPadding: EdgeInsets.zero,
                          leading: const Icon(Icons.place),
                          title: Text(widget.camp.location),
                          subtitle: Text(
                              '${formatDateTime(widget.camp.startsAtUtc)}${widget.distanceKm != null ? ' · ${widget.distanceKm!.toStringAsFixed(1)} km away' : ''}')),
                      if (widget.camp.latitude != null &&
                          widget.camp.longitude != null) ...[
                        CampMap(
                            camp: LatLng(
                                widget.camp.latitude!, widget.camp.longitude!),
                            campName: widget.camp.name,
                            you: you,
                            youLabel: here != null
                                ? 'You are here'
                                : 'Your saved address'),
                        const SizedBox(height: 8),
                        Row(children: [
                          Expanded(
                              child: CampMapLegend(
                                  youLabel:
                                      here != null ? 'You' : 'Your address')),
                          TextButton.icon(
                              onPressed: locating ? null : _locate,
                              icon: const Icon(Icons.my_location, size: 18),
                              label: Text(
                                  locating ? 'Locating...' : 'My location'))
                        ]),
                        if (you != null)
                          Text(
                              '${distanceKm(you!, LatLng(widget.camp.latitude!, widget.camp.longitude!)).toStringAsFixed(1)} km from ${here != null ? 'you' : 'your saved address'}',
                              style: Theme.of(context).textTheme.bodySmall),
                        if (locationMessage != null)
                          Text(locationMessage!,
                              style: Theme.of(context).textTheme.bodySmall),
                        const SizedBox(height: 12),
                      ],
                      if (error != null)
                        Padding(
                            padding: const EdgeInsets.symmetric(vertical: 8),
                            child: Text(error!,
                                style: TextStyle(color: scheme.error))),
                      if (notice != null)
                        Padding(
                            padding: const EdgeInsets.symmetric(vertical: 8),
                            child: Text(notice!,
                                style: TextStyle(color: scheme.primary))),
                      if (donor == null)
                        const Card(
                            child: ListTile(
                                leading: Icon(Icons.info_outline),
                                title: Text(
                                    'Create your eligibility profile first'),
                                subtitle: Text(
                                    'You need a donor profile before you can book a slot.')))
                      else if (booking != null)
                        Card(
                            color: scheme.primaryContainer,
                            child: ListTile(
                                leading: const Icon(Icons.event_available),
                                title: Text(
                                    'Booked for ${formatDateTime(booking.slotTimeUtc)}'),
                                trailing: TextButton(
                                    onPressed:
                                        busy ? null : () => _cancel(booking),
                                    child: const Text('Cancel'))))
                      else if (donor!.eligibilityStatus != 'Eligible')
                        Card(
                            key: const Key('booking-needs-eligibility'),
                            child: ListTile(
                                leading: const Icon(Icons.info_outline),
                                title: const Text(
                                    'Booking opens once you are eligible'),
                                subtitle: Text(donor!.eligibilityStatus ==
                                        'PendingVerification'
                                    ? 'Blood bank staff will verify your profile before you can book.'
                                    : 'Your status is ${humanize(donor!.eligibilityStatus)}. Check your eligibility profile for details.')))
                      else ...[
                        Text('Choose a time',
                            style: Theme.of(context).textTheme.titleMedium),
                        const SizedBox(height: 8),
                        if (open.isEmpty)
                          const Text('No open slots remain at this camp.')
                        else
                          Wrap(
                              spacing: 8,
                              runSpacing: 8,
                              children: open
                                  .map((s) => ChoiceChip(
                                      label: Text(formatDateTime(s.slotTimeUtc)
                                          .substring(11)),
                                      selected: selectedSlotId == s.id,
                                      onSelected: (v) => setState(() =>
                                          selectedSlotId = v ? s.id : null)))
                                  .toList()),
                        const SizedBox(height: 16),
                        FilledButton(
                            onPressed: busy || open.isEmpty ? null : _book,
                            child: Text(busy
                                ? 'Booking...'
                                : selectedSlotId == null
                                    ? 'Book next available slot'
                                    : 'Book selected slot'))
                      ]
                    ])));
  }
}
