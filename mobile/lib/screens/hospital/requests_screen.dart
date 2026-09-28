import '../../widgets/life_scaffold.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../core/format.dart';
import '../../core/validators.dart';
import '../../models/domain.dart';
import '../../services/auth_controller.dart';
import '../../services/lifelink_service.dart';
import 'request_detail_screen.dart';

class RequestsScreen extends ConsumerStatefulWidget {
  const RequestsScreen({super.key});
  @override
  ConsumerState<RequestsScreen> createState() => _RequestsScreenState();
}

class _RequestsScreenState extends ConsumerState<RequestsScreen> {
  List<BloodRequest> items = [];
  bool loading = true;
  String? error;

  LifeLinkService get service => LifeLinkService(ref.read(apiProvider));

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    try {
      final result = await service.requests();
      items = result;
      error = null;
    } catch (e) {
      error = e.toString();
    } finally {
      if (mounted) setState(() => loading = false);
    }
  }

  Future<void> _openForm() async {
    final created = await showModalBottomSheet<BloodRequest>(
        context: context,
        isScrollControlled: true,
        showDragHandle: true,
        builder: (_) => RequestFormSheet(service: service));
    if (created != null) {
      setState(() => loading = true);
      await _load();
      if (mounted) {
        ScaffoldMessenger.of(context)
            .showSnackBar(const SnackBar(content: Text('Request submitted')));
      }
    }
  }

  @override
  Widget build(BuildContext context) => LifeScaffold(
      appBar: AppBar(title: const Text('Hospital requests')),
      floatingActionButton: FloatingActionButton.extended(
          onPressed: _openForm,
          icon: const Icon(Icons.add),
          label: const Text('New request')),
      body: loading
          ? const Center(child: CircularProgressIndicator())
          : RefreshIndicator(
              onRefresh: _load,
              child: ListView(
                  physics: const AlwaysScrollableScrollPhysics(),
                  padding: const EdgeInsets.fromLTRB(16, 16, 16, 96),
                  children: [
                    const PageIntro(
                        title: 'Care cannot wait.',
                        subtitle:
                            'Create a blood request and follow its progress, from review to fulfilment.',
                        icon: Icons.local_hospital_outlined,
                        photo: false),
                    if (error != null)
                      Padding(
                          padding: const EdgeInsets.only(bottom: 12),
                          child: Text(error!,
                              style: TextStyle(
                                  color: Theme.of(context).colorScheme.error))),
                    if (error == null && items.isEmpty)
                      const Padding(
                          padding: EdgeInsets.all(32),
                          child: Center(
                              child: Text(
                                  'No blood requests yet. Tap "New request" to create one.'))),
                    ...items.map((x) => Card(
                        child: ListTile(
                            onTap: () async {
                              await Navigator.push(
                                  context,
                                  MaterialPageRoute(
                                      builder: (_) =>
                                          RequestDetailScreen(request: x)));
                              if (mounted) await _load();
                            },
                            leading: CircleAvatar(
                                child: Text(bloodLabel(x.bloodType))),
                            title: Text(
                                '${x.quantityUnits} unit(s) · ${x.urgency}',
                                style: TextStyle(
                                    color: urgencyColor(context, x.urgency),
                                    fontWeight: FontWeight.w600)),
                            subtitle: Text(
                                '${humanize(x.status)}\nRequired ${formatDateTime(x.requiredByUtc)}'),
                            isThreeLine: true,
                            trailing: const Icon(Icons.chevron_right)))),
                  ])));
}

/// Creates a request, or edits [existing]; pops with the saved request.
class RequestFormSheet extends StatefulWidget {
  const RequestFormSheet({super.key, required this.service, this.existing});
  final LifeLinkService service;
  final BloodRequest? existing;
  @override
  State<RequestFormSheet> createState() => _RequestFormSheetState();
}

class _RequestFormSheetState extends State<RequestFormSheet> {
  /// Hours from now; 0 keeps the existing deadline when editing.
  static const _keepDeadline = 0;
  final _form = GlobalKey<FormState>();
  late final units =
      TextEditingController(text: '${widget.existing?.quantityUnits ?? 1}');
  late final notes = TextEditingController(text: widget.existing?.notes ?? '');
  late String bloodType = widget.existing?.bloodType ?? 'OPositive',
      urgency = widget.existing?.urgency ?? 'Routine';
  late int hours = widget.existing == null ? 24 : _keepDeadline;
  bool submitting = false;
  String? error;

  @override
  void dispose() {
    units.dispose();
    notes.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (!_form.currentState!.validate()) return;
    setState(() {
      submitting = true;
      error = null;
    });
    try {
      final input = {
        'bloodType': bloodType,
        'quantityUnits': int.parse(units.text.trim()),
        'requestedUrgency': urgency,
        'notes': notes.text.trim().isEmpty ? null : notes.text.trim(),
        'requiredByUtc': hours == _keepDeadline
            ? widget.existing!.requiredByUtc
            : DateTime.now()
                .toUtc()
                .add(Duration(hours: hours))
                .toIso8601String()
      };
      final existing = widget.existing;
      final saved = existing == null
          ? await widget.service.createRequest(input)
          : await widget.service.updateRequest(existing.id, input);
      if (mounted) Navigator.pop(context, saved);
    } catch (e) {
      if (mounted) {
        setState(() {
          error = e.toString();
          submitting = false;
        });
      }
    }
  }

  @override
  Widget build(BuildContext context) => Padding(
      padding: EdgeInsets.fromLTRB(
          20, 0, 20, MediaQuery.viewInsetsOf(context).bottom + 20),
      child: SingleChildScrollView(
          child: Form(
              key: _form,
              child: Column(mainAxisSize: MainAxisSize.min, children: [
                DropdownButtonFormField<String>(
                    initialValue: bloodType,
                    decoration: const InputDecoration(labelText: 'Blood type'),
                    items: bloodTypes
                        .map((x) => DropdownMenuItem(
                            value: x, child: Text(bloodLabel(x))))
                        .toList(),
                    onChanged: (x) => setState(() => bloodType = x!)),
                const SizedBox(height: 10),
                TextFormField(
                    controller: units,
                    keyboardType: TextInputType.number,
                    validator: (v) =>
                        Validators.integerInRange(v, 'Units', 1, 100),
                    decoration: const InputDecoration(labelText: 'Units')),
                const SizedBox(height: 10),
                DropdownButtonFormField<String>(
                    initialValue: urgency,
                    decoration: const InputDecoration(
                        labelText: 'Urgency',
                        helperText:
                            'The system may raise this based on units and deadline'),
                    items: const ['Routine', 'Urgent', 'Critical']
                        .map((x) => DropdownMenuItem(value: x, child: Text(x)))
                        .toList(),
                    onChanged: (x) => setState(() => urgency = x!)),
                const SizedBox(height: 10),
                DropdownButtonFormField<int>(
                    initialValue: hours,
                    decoration:
                        const InputDecoration(labelText: 'Needed within'),
                    items: [
                      if (widget.existing != null)
                        DropdownMenuItem(
                            value: _keepDeadline,
                            child: Text(
                                'Keep ${formatDateTime(widget.existing!.requiredByUtc)}',
                                overflow: TextOverflow.ellipsis)),
                      for (final h in const [2, 6, 12, 24, 48, 72, 168])
                        DropdownMenuItem(
                            value: h,
                            child: Text(h == 168 ? '7 days' : '$h hours'))
                    ],
                    onChanged: (x) => setState(() => hours = x!)),
                const SizedBox(height: 10),
                TextFormField(
                    controller: notes,
                    maxLines: 2,
                    maxLength: 2000,
                    validator: (v) => (v ?? '').trim().length > 2000
                        ? 'Notes must be at most 2000 characters.'
                        : null,
                    decoration:
                        const InputDecoration(labelText: 'Clinical notes')),
                if (error != null)
                  Padding(
                      padding: const EdgeInsets.only(top: 12),
                      child: Text(error!,
                          key: const Key('form-error'),
                          style: TextStyle(
                              color: Theme.of(context).colorScheme.error))),
                const SizedBox(height: 16),
                SizedBox(
                    width: double.infinity,
                    child: FilledButton(
                        onPressed: submitting ? null : _submit,
                        child: Text(submitting
                            ? 'Saving...'
                            : widget.existing == null
                                ? 'Submit request'
                                : 'Save changes')))
              ]))));
}
