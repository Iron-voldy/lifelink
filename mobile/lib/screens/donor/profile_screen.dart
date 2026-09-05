import '../../widgets/life_scaffold.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:geolocator/geolocator.dart';
import '../../core/format.dart';
import '../../core/validators.dart';
import '../../models/domain.dart';
import '../../services/auth_controller.dart';
import '../../services/lifelink_service.dart';

class DonorProfileScreen extends ConsumerStatefulWidget {
  const DonorProfileScreen({super.key});
  @override
  ConsumerState<DonorProfileScreen> createState() => _DonorProfileScreenState();
}

class _DonorProfileScreenState extends ConsumerState<DonorProfileScreen> {
  final _form = GlobalKey<FormState>();
  final address = TextEditingController();
  final dob = TextEditingController();
  final flagInput = TextEditingController();
  DonorProfile? profile;
  EligibilityEvaluation? evaluation;
  List<EligibilityHistoryEntry> eligibilityHistory = [];
  List<DonationRecord> donations = [];
  List<String> flags = [];
  String bloodType = 'OPositive';
  double? latitude, longitude;
  bool loading = true, saving = false, checking = false, locating = false;
  String? error, notice;

  LifeLinkService get service => LifeLinkService(ref.read(apiProvider));

  @override
  void initState() {
    super.initState();
    _load();
  }

  @override
  void dispose() {
    address.dispose();
    dob.dispose();
    flagInput.dispose();
    super.dispose();
  }

  Future<void> _load() async {
    try {
      profile = await service.myDonorProfile();
      if (profile != null) {
        _fill(profile!);
        await _loadHistory();
      }
    } catch (e) {
      error = e.toString();
    } finally {
      if (mounted) setState(() => loading = false);
    }
  }

  void _fill(DonorProfile p) {
    address.text = p.address;
    dob.text = p.dateOfBirth;
    bloodType = p.bloodType;
    latitude = p.latitude;
    longitude = p.longitude;
    flags = [...p.medicalFlags];
  }

  /// History is supplementary; a failure here must not hide the profile.
  Future<void> _loadHistory() async {
    try {
      final id = profile!.id;
      final results = await Future.wait([
        service.eligibilityHistory(id),
        service.donationHistory(id),
      ]);
      eligibilityHistory = results[0] as List<EligibilityHistoryEntry>;
      donations = results[1] as List<DonationRecord>;
    } catch (_) {
      eligibilityHistory = [];
      donations = [];
    }
  }

  Future<void> _locate() async {
    setState(() {
      locating = true;
      error = null;
    });
    try {
      if (!await Geolocator.isLocationServiceEnabled()) {
        throw const _Friendly(
            'Location services are turned off. Enter your address manually or enable location.');
      }
      var permission = await Geolocator.checkPermission();
      if (permission == LocationPermission.denied) {
        permission = await Geolocator.requestPermission();
      }
      if (permission == LocationPermission.denied ||
          permission == LocationPermission.deniedForever) {
        throw const _Friendly(
            'Location permission denied. You can still enter an address manually.');
      }
      final p = await Geolocator.getCurrentPosition();
      latitude = p.latitude;
      longitude = p.longitude;
    } on _Friendly catch (e) {
      error = e.message;
    } catch (_) {
      error = 'Could not read your location. Enter your address manually.';
    } finally {
      if (mounted) setState(() => locating = false);
    }
  }

  void _addFlag() {
    final v = flagInput.text.trim();
    if (v.isEmpty) return;
    if (v.length > 100) {
      setState(() => error = 'A medical flag must be at most 100 characters.');
      return;
    }
    if (flags.length >= 20) {
      setState(() => error = 'You can add at most 20 medical flags.');
      return;
    }
    if (flags.any((f) => f.toLowerCase() == v.toLowerCase())) {
      flagInput.clear();
      return;
    }
    setState(() {
      flags = [...flags, v];
      error = null;
    });
    flagInput.clear();
  }

  Future<void> _pickDate() async {
    final now = DateTime.now();
    // Same range the validator and API accept: at least 18, at most 120 years ago.
    final first = DateTime(now.year - 120, now.month, now.day);
    final last = DateTime(now.year - 18, now.month, now.day);
    var initial = Validators.parseIsoDate(dob.text) ??
        DateTime(now.year - 30, now.month, now.day);
    if (initial.isBefore(first)) initial = first;
    if (initial.isAfter(last)) initial = last;
    final picked = await showDatePicker(
        context: context,
        initialDate: initial,
        firstDate: first,
        lastDate: last);
    if (picked != null) {
      String two(int n) => n.toString().padLeft(2, '0');
      dob.text = '${picked.year}-${two(picked.month)}-${two(picked.day)}';
    }
  }

  Future<void> _save() async {
    if (!_form.currentState!.validate()) return;
    setState(() {
      saving = true;
      error = null;
      notice = null;
    });
    try {
      profile = await service.saveDonorProfile(
          id: profile?.id,
          bloodType: bloodType,
          dateOfBirth: dob.text.trim(),
          address: address.text.trim(),
          latitude: latitude,
          longitude: longitude,
          flags: flags);
      notice = 'Profile saved.';
      await _loadHistory();
    } catch (e) {
      error = e.toString();
    } finally {
      if (mounted) setState(() => saving = false);
    }
  }

  Future<void> _check() async {
    setState(() {
      checking = true;
      error = null;
      notice = null;
    });
    try {
      evaluation = await service.checkEligibility(profile!.id);
      profile = await service.myDonorProfile();
      await _loadHistory();
    } catch (e) {
      error = e.toString();
    } finally {
      if (mounted) setState(() => checking = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    return LifeScaffold(
        appBar: AppBar(title: const Text('Eligibility profile')),
        body: loading
            ? const Center(child: CircularProgressIndicator())
            : RefreshIndicator(
                onRefresh: () async {
                  await _load();
                },
                child: Form(
                    key: _form,
                    child: ListView(
                        physics: const AlwaysScrollableScrollPhysics(),
                        padding: const EdgeInsets.all(20),
                        children: [
                          const PageIntro(
                              title: 'Your next donation starts here.',
                              subtitle:
                                  'Keep your profile up to date so every match starts with the right information.',
                              icon: Icons.health_and_safety_outlined,
                              photo: false),
                          if (profile != null) _statusCard(context),
                          const SizedBox(height: 12),
                          DropdownButtonFormField<String>(
                              initialValue: bloodType,
                              decoration: const InputDecoration(
                                  labelText: 'Blood type'),
                              items: bloodTypes
                                  .map((x) => DropdownMenuItem(
                                      value: x, child: Text(bloodLabel(x))))
                                  .toList(),
                              onChanged: (x) => setState(() => bloodType = x!)),
                          const SizedBox(height: 12),
                          TextFormField(
                              controller: dob,
                              keyboardType: TextInputType.datetime,
                              validator: Validators.dateOfBirth,
                              decoration: InputDecoration(
                                  labelText: 'Date of birth (YYYY-MM-DD)',
                                  suffixIcon: IconButton(
                                      tooltip: 'Pick a date',
                                      onPressed: _pickDate,
                                      icon: const Icon(Icons.calendar_month)))),
                          const SizedBox(height: 12),
                          TextFormField(
                              controller: address,
                              maxLines: 2,
                              validator: (v) =>
                                  Validators.required(v, 'Address', max: 500),
                              decoration:
                                  const InputDecoration(labelText: 'Address')),
                          const SizedBox(height: 12),
                          OutlinedButton.icon(
                              onPressed: locating ? null : _locate,
                              icon: locating
                                  ? const SizedBox(
                                      width: 16,
                                      height: 16,
                                      child: CircularProgressIndicator(
                                          strokeWidth: 2))
                                  : const Icon(Icons.my_location),
                              label: Text(latitude == null
                                  ? 'Use current location'
                                  : 'Location attached (tap to refresh)')),
                          const SizedBox(height: 20),
                          Text('Medical flags',
                              style: Theme.of(context).textTheme.titleSmall),
                          const Text(
                              'Add anything a clinician should review, e.g. "On antibiotics". Any flag requires medical review.',
                              style: TextStyle(fontSize: 12)),
                          const SizedBox(height: 8),
                          Row(children: [
                            Expanded(
                                child: TextField(
                                    controller: flagInput,
                                    onSubmitted: (_) => _addFlag(),
                                    decoration: const InputDecoration(
                                        labelText: 'Add a flag'))),
                            IconButton(
                                tooltip: 'Add flag',
                                onPressed: _addFlag,
                                icon: const Icon(Icons.add_circle))
                          ]),
                          if (flags.isNotEmpty)
                            Wrap(
                                spacing: 8,
                                children: flags
                                    .map((f) => InputChip(
                                        label: Text(f),
                                        onDeleted: () => setState(() => flags =
                                            flags
                                                .where((x) => x != f)
                                                .toList())))
                                    .toList()),
                          if (error != null)
                            Padding(
                                padding:
                                    const EdgeInsets.symmetric(vertical: 12),
                                child: Text(error!,
                                    key: const Key('form-error'),
                                    style: TextStyle(color: scheme.error))),
                          if (notice != null)
                            Padding(
                                padding:
                                    const EdgeInsets.symmetric(vertical: 12),
                                child: Text(notice!,
                                    style: TextStyle(color: scheme.primary))),
                          const SizedBox(height: 8),
                          FilledButton(
                              onPressed: saving ? null : _save,
                              child: Text(saving
                                  ? 'Saving...'
                                  : profile == null
                                      ? 'Create profile'
                                      : 'Save profile')),
                          if (profile != null) ...[
                            const SizedBox(height: 8),
                            OutlinedButton.icon(
                                onPressed: checking ? null : _check,
                                icon: const Icon(Icons.fact_check),
                                label: Text(checking
                                    ? 'Checking...'
                                    : 'Re-check eligibility')),
                            _historySection(context)
                          ]
                        ]))));
  }

  Widget _statusCard(BuildContext context) {
    final eligible = profile!.eligibilityStatus == 'Eligible';
    return Card(
        color: eligible
            ? Theme.of(context).colorScheme.primaryContainer
            : Theme.of(context).colorScheme.surfaceContainerHighest,
        child: Padding(
            padding: const EdgeInsets.all(16),
            child:
                Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
              Row(children: [
                Icon(eligible ? Icons.check_circle : Icons.info_outline),
                const SizedBox(width: 8),
                Text(humanize(profile!.eligibilityStatus),
                    style: Theme.of(context).textTheme.titleMedium)
              ]),
              if (profile!.eligibilityStatus == 'PendingVerification') ...[
                const SizedBox(height: 8),
                const Text(
                    'Blood bank staff will verify your profile before you can book a camp. Changing your blood type, date of birth or medical flags sends it back for review.',
                    key: Key('pending-verification-hint'))
              ],
              if (evaluation != null && evaluation!.reasons.isNotEmpty) ...[
                const SizedBox(height: 8),
                ...evaluation!.reasons.map((r) => Text('• $r'))
              ]
            ])));
  }

  Widget _historySection(BuildContext context) =>
      Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
        const SizedBox(height: 24),
        Text('Donation history',
            style: Theme.of(context).textTheme.titleMedium),
        if (donations.isEmpty)
          const Padding(
              padding: EdgeInsets.symmetric(vertical: 8),
              child: Text('No donations recorded yet.')),
        ...donations.map((d) => ListTile(
            dense: true,
            leading: const Icon(Icons.bloodtype),
            title: Text('${d.units} unit(s) on ${d.date}'),
            subtitle: Text(d.location))),
        const SizedBox(height: 16),
        Text('Eligibility history',
            style: Theme.of(context).textTheme.titleMedium),
        if (eligibilityHistory.isEmpty)
          const Padding(
              padding: EdgeInsets.symmetric(vertical: 8),
              child: Text('No eligibility changes yet.')),
        ...eligibilityHistory.map((h) => ListTile(
            dense: true,
            leading: const Icon(Icons.history),
            title: Text('${humanize(h.previous)} → ${humanize(h.next)}'),
            subtitle: Text('${h.reason}\n${formatDateTime(h.changedAtUtc)}'),
            isThreeLine: true))
      ]);
}

class _Friendly implements Exception {
  const _Friendly(this.message);
  final String message;
}
