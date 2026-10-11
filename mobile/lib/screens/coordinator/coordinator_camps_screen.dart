import '../../widgets/life_scaffold.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/format.dart';
import '../../models/domain.dart';
import '../../services/auth_controller.dart';
import '../../services/lifelink_service.dart';

/// Next lifecycle step a coordinator can move a camp to, mirroring the web camps page.
const _nextStatus = {
  'Draft': 'Scheduled',
  'Scheduled': 'InProgress',
  'InProgress': 'Closed'
};

/// Mirrors CampService.Validate so an impossible schedule is explained before it is sent.
String? validateCampSchedule(
    DateTime? start, DateTime? end, int capacity, int slotMinutes,
    {DateTime? now}) {
  now ??= DateTime.now();
  if (start == null || end == null) {
    return 'Choose both a start and an end date and time.';
  }
  if (!start.isAfter(now)) return 'Start time must be in the future.';
  if (start.isAfter(
      DateTime(now.year + 2, now.month, now.day, now.hour, now.minute))) {
    return 'Start time must be within two years; check the year.';
  }
  if (!end.isAfter(start)) return 'End time must be after the start time.';
  // Same rule as the server: one Sri Lanka day (UTC+5:30, no daylight saving), 6:00 AM to 9:00 PM.
  final lkStart = start.toUtc().add(const Duration(hours: 5, minutes: 30)),
      lkEnd = end.toUtc().add(const Duration(hours: 5, minutes: 30));
  int minutes(DateTime d) => d.hour * 60 + d.minute;
  if (lkStart.year != lkEnd.year ||
      lkStart.month != lkEnd.month ||
      lkStart.day != lkEnd.day ||
      minutes(lkStart) < 6 * 60 ||
      minutes(lkEnd) > 21 * 60) {
    return 'Camps must run between 6:00 AM and 9:00 PM Sri Lanka time on one day. Check AM/PM.';
  }
  if (capacity < 1 || capacity > 1000) {
    return 'Capacity must be between 1 and 1000.';
  }
  if (slotMinutes < 5 || slotMinutes > 240) {
    return 'Slot minutes must be between 5 and 240.';
  }
  final needed = capacity * slotMinutes;
  if (needed > end.difference(start).inMinutes) {
    return '$capacity slots of $slotMinutes minutes need $needed minutes, which is longer than the camp. Extend the end time, or reduce capacity or slot length.';
  }
  return null;
}

/// Camps organised by the signed-in coordinator: schedule, open, close and cancel them.
class CoordinatorCampsScreen extends ConsumerStatefulWidget {
  const CoordinatorCampsScreen({super.key});
  @override
  ConsumerState<CoordinatorCampsScreen> createState() =>
      _CoordinatorCampsScreenState();
}

class _CoordinatorCampsScreenState
    extends ConsumerState<CoordinatorCampsScreen> {
  List<Camp> camps = [];
  bool loading = true, busy = false;
  String? error, notice;

  LifeLinkService get service => LifeLinkService(ref.read(apiProvider));

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    try {
      camps = await service.myCamps();
      error = null;
    } catch (e) {
      error = e.toString();
    } finally {
      if (mounted) setState(() => loading = false);
    }
  }

  Future<void> _transition(Camp camp, String status) async {
    if (status == 'Cancelled') {
      final ok = await showDialog<bool>(
          context: context,
          builder: (ctx) => AlertDialog(
                  title: Text('Cancel ${camp.name}?'),
                  content: const Text('Booked donors lose their slots.'),
                  actions: [
                    TextButton(
                        onPressed: () => Navigator.pop(ctx, false),
                        child: const Text('Keep camp')),
                    FilledButton(
                        onPressed: () => Navigator.pop(ctx, true),
                        child: const Text('Cancel camp'))
                  ]));
      if (ok != true) return;
    }
    setState(() {
      busy = true;
      error = null;
      notice = null;
    });
    try {
      final updated = await service.transitionCamp(camp.id, status);
      notice = '${updated.name} is now ${humanize(updated.status)}.';
    } catch (e) {
      error = e.toString();
    }
    await _load();
    if (mounted) setState(() => busy = false);
  }

  Future<void> _create() async {
    final created = await Navigator.push<Camp>(
        context, MaterialPageRoute(builder: (_) => const CreateCampScreen()));
    if (created == null) return;
    notice =
        '${created.name} was created as a draft with ${created.capacity} slots. Move it to Scheduled to open bookings.';
    await _load();
  }

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    return LifeScaffold(
        appBar: AppBar(title: const Text('My camps')),
        floatingActionButton: FloatingActionButton.extended(
            onPressed: busy ? null : _create,
            icon: const Icon(Icons.add),
            label: const Text('Create camp')),
        body: loading
            ? const Center(child: CircularProgressIndicator())
            : RefreshIndicator(
                onRefresh: _load,
                child: ListView(
                    physics: const AlwaysScrollableScrollPhysics(),
                    padding: const EdgeInsets.fromLTRB(16, 16, 16, 96),
                    children: [
                      const PageIntro(
                          title: 'Give together. Grow together.',
                          subtitle:
                              'Schedule your donation days, open bookings and check donors in on the day.',
                          icon: Icons.event_note_outlined,
                          photo: true,
                          imagePath: 'assets/images/donation.jpg'),
                      if (error != null)
                        Text(error!, style: TextStyle(color: scheme.error)),
                      if (notice != null)
                        Text(notice!, style: TextStyle(color: scheme.primary)),
                      if (error == null && camps.isEmpty)
                        const Padding(
                            padding: EdgeInsets.all(32),
                            child: Center(
                                child: Text(
                                    'You have no camps yet. Tap Create camp to schedule one.'))),
                      ...camps.map((camp) => _CampCard(
                          camp: camp,
                          busy: busy,
                          onTransition: (s) => _transition(camp, s),
                          onOpen: () async {
                            await Navigator.push(
                                context,
                                MaterialPageRoute(
                                    builder: (_) => CoordinatorCampDetailScreen(
                                        camp: camp)));
                            if (mounted) await _load();
                          }))
                    ])));
  }
}

class _CampCard extends StatelessWidget {
  const _CampCard(
      {required this.camp,
      required this.busy,
      required this.onTransition,
      required this.onOpen});
  final Camp camp;
  final bool busy;
  final ValueChanged<String> onTransition;
  final VoidCallback onOpen;

  @override
  Widget build(BuildContext context) {
    final next = _nextStatus[camp.status];
    final occupancy =
        camp.capacity == 0 ? 0.0 : (camp.bookedSlots / camp.capacity);
    return Card(
        child: Padding(
            padding: const EdgeInsets.all(16),
            child:
                Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
              Row(children: [
                Expanded(
                    child: Text(camp.name,
                        style: Theme.of(context).textTheme.titleMedium)),
                Chip(label: Text(humanize(camp.status)))
              ]),
              Text(
                  '${camp.location}\n${formatDateTime12(camp.startsAtUtc)}${camp.endsAtUtc.isEmpty ? '' : ' – ${formatTime(camp.endsAtUtc)}'}'),
              const SizedBox(height: 8),
              Text('${camp.bookedSlots} / ${camp.capacity} booked'),
              const SizedBox(height: 4),
              LinearProgressIndicator(value: occupancy.clamp(0, 1)),
              const SizedBox(height: 8),
              Wrap(spacing: 8, runSpacing: 4, children: [
                if (next != null)
                  FilledButton.tonal(
                      onPressed: busy ? null : () => onTransition(next),
                      child: Text('Move to ${humanize(next)}')),
                if (camp.status == 'Draft' || camp.status == 'Scheduled')
                  TextButton(
                      onPressed: busy ? null : () => onTransition('Cancelled'),
                      child: const Text('Cancel camp')),
                if (camp.status != 'Draft')
                  TextButton(onPressed: onOpen, child: const Text('Attendance'))
              ])
            ])));
  }
}

/// Form for a new camp; returns the created camp to the list.
class CreateCampScreen extends ConsumerStatefulWidget {
  const CreateCampScreen({super.key});
  @override
  ConsumerState<CreateCampScreen> createState() => _CreateCampScreenState();
}

class _CreateCampScreenState extends ConsumerState<CreateCampScreen> {
  final name = TextEditingController(),
      location = TextEditingController(),
      capacity = TextEditingController(),
      slotMinutes = TextEditingController(text: '15');
  DateTime? start, end;
  String? error;
  bool busy = false;

  @override
  void dispose() {
    for (final c in [name, location, capacity, slotMinutes]) {
      c.dispose();
    }
    super.dispose();
  }

  Future<DateTime?> _pick(DateTime? initial, DateTime first,
      {TimeOfDay defaultTime = const TimeOfDay(hour: 9, minute: 0)}) async {
    final date = await showDatePicker(
        context: context,
        initialDate: initial ?? first,
        firstDate: DateTime(first.year, first.month, first.day),
        lastDate: DateTime.now().add(const Duration(days: 730)));
    if (date == null || !mounted) return null;
    final time = await showTimePicker(
        context: context,
        helpText: 'Camps run between 6:00 AM and 9:00 PM',
        initialTime:
            initial != null ? TimeOfDay.fromDateTime(initial) : defaultTime);
    if (time == null) return null;
    return DateTime(date.year, date.month, date.day, time.hour, time.minute);
  }

  Future<void> _submit() async {
    final cap = int.tryParse(capacity.text) ?? 0,
        slot = int.tryParse(slotMinutes.text) ?? 0;
    final problem = name.text.trim().isEmpty || location.text.trim().isEmpty
        ? 'Name and location are required.'
        : validateCampSchedule(start, end, cap, slot);
    setState(() => error = problem);
    if (problem != null) return;
    setState(() => busy = true);
    try {
      final camp = await LifeLinkService(ref.read(apiProvider)).createCamp({
        'name': name.text.trim(),
        'location': location.text.trim(),
        'startsAtUtc': start!.toUtc().toIso8601String(),
        'endsAtUtc': end!.toUtc().toIso8601String(),
        'capacity': cap,
        'slotMinutes': slot
      });
      if (mounted) Navigator.pop(context, camp);
    } catch (e) {
      if (mounted) setState(() => error = e.toString());
    } finally {
      if (mounted) setState(() => busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    return LifeScaffold(
        appBar: AppBar(title: const Text('Schedule a camp')),
        body: ListView(padding: const EdgeInsets.all(20), children: [
          TextField(
              controller: name,
              decoration: const InputDecoration(labelText: 'Name')),
          const SizedBox(height: 12),
          TextField(
              controller: location,
              decoration: const InputDecoration(labelText: 'Location')),
          const SizedBox(height: 12),
          ListTile(
              contentPadding: EdgeInsets.zero,
              leading: const Icon(Icons.play_circle_outline),
              title: const Text('Starts'),
              subtitle: Text(start == null
                  ? 'Choose date and time'
                  : formatDateTime12(start!.toIso8601String())),
              onTap: () async {
                final picked = await _pick(start, DateTime.now());
                if (picked != null) {
                  setState(() {
                    start = picked;
                    if (end != null && !end!.isAfter(picked)) end = null;
                  });
                }
              }),
          ListTile(
              contentPadding: EdgeInsets.zero,
              enabled: start != null,
              leading: const Icon(Icons.stop_circle_outlined),
              title: const Text('Ends'),
              subtitle: Text(end == null
                  ? (start == null
                      ? 'Choose the start time first'
                      : 'Must be after the start time')
                  : formatDateTime12(end!.toIso8601String())),
              onTap: () async {
                final suggested = start!.add(const Duration(hours: 4));
                final picked = await _pick(end, start!,
                    defaultTime: suggested.day == start!.day
                        ? TimeOfDay.fromDateTime(suggested)
                        : const TimeOfDay(hour: 21, minute: 0));
                if (picked != null) setState(() => end = picked);
              }),
          TextField(
              controller: capacity,
              keyboardType: TextInputType.number,
              decoration:
                  const InputDecoration(labelText: 'Capacity (donor slots)')),
          const SizedBox(height: 12),
          TextField(
              controller: slotMinutes,
              keyboardType: TextInputType.number,
              decoration: const InputDecoration(labelText: 'Slot minutes')),
          if (error != null)
            Padding(
                padding: const EdgeInsets.only(top: 12),
                child: Text(error!,
                    style:
                        TextStyle(color: Theme.of(context).colorScheme.error))),
          const SizedBox(height: 20),
          FilledButton(
              onPressed: busy ? null : _submit,
              child: Text(busy ? 'Scheduling...' : 'Schedule camp'))
        ]));
  }
}

/// Roster and attendance for one camp; donors can be checked in while it is in progress.
class CoordinatorCampDetailScreen extends ConsumerStatefulWidget {
  const CoordinatorCampDetailScreen({super.key, required this.camp});
  final Camp camp;
  @override
  ConsumerState<CoordinatorCampDetailScreen> createState() =>
      _CoordinatorCampDetailScreenState();
}

class _CoordinatorCampDetailScreenState
    extends ConsumerState<CoordinatorCampDetailScreen> {
  List<CampRosterEntry> roster = [];
  AttendanceReport? report;
  bool loading = true, busy = false;
  String? error;

  LifeLinkService get service => LifeLinkService(ref.read(apiProvider));

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    try {
      final results = await Future.wait<Object>([
        service.campRoster(widget.camp.id),
        service.campAttendance(widget.camp.id)
      ]);
      roster = results[0] as List<CampRosterEntry>;
      report = results[1] as AttendanceReport;
      error = null;
    } catch (e) {
      error = e.toString();
    } finally {
      if (mounted) setState(() => loading = false);
    }
  }

  Future<void> _checkIn(CampRosterEntry entry) async {
    setState(() => busy = true);
    try {
      await service.checkIn(widget.camp.id, entry.slotId);
    } catch (e) {
      error = e.toString();
    }
    await _load();
    if (mounted) setState(() => busy = false);
  }

  @override
  Widget build(BuildContext context) {
    final r = report;
    return LifeScaffold(
        appBar: AppBar(title: Text(widget.camp.name)),
        body: loading
            ? const Center(child: CircularProgressIndicator())
            : RefreshIndicator(
                onRefresh: _load,
                child: ListView(
                    physics: const AlwaysScrollableScrollPhysics(),
                    padding: const EdgeInsets.all(16),
                    children: [
                      if (error != null)
                        Text(error!,
                            style: TextStyle(
                                color: Theme.of(context).colorScheme.error)),
                      if (r != null)
                        Card(
                            child: ListTile(
                                leading: const Icon(Icons.how_to_reg),
                                title: Text(
                                    '${r.checkedIn} checked in of ${r.booked} booked'),
                                subtitle: Text(
                                    '${r.noShows} no-show(s) · ${r.cancelled} cancelled · ${r.attendanceRate}% attendance'))),
                      if (roster.isEmpty)
                        const Padding(
                            padding: EdgeInsets.all(32),
                            child: Center(child: Text('No bookings yet.'))),
                      ...roster.map((entry) => Card(
                          child: ListTile(
                              title: Text(entry.donorEmail),
                              subtitle: Text(
                                  '${formatTime(entry.slotTimeUtc)} · ${bloodLabel(entry.bloodType)} · ${humanize(entry.status)}'),
                              trailing: widget.camp.status == 'InProgress' &&
                                      entry.status == 'Booked'
                                  ? TextButton(
                                      onPressed:
                                          busy ? null : () => _checkIn(entry),
                                      child: const Text('Check in'))
                                  : null)))
                    ])));
  }
}
