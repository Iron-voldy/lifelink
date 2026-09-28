import '../../widgets/life_scaffold.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../core/format.dart';
import '../../models/domain.dart';
import '../../services/auth_controller.dart';
import '../../services/lifelink_service.dart';
import 'requests_screen.dart';

/// Until the blood bank approves a request the hospital may still edit,
/// escalate or withdraw it; afterwards stock is committed (mirrors the API).
const preApprovalStatuses = {
  'Submitted',
  'UnderReview',
  'PendingApproval',
  'Escalated'
};

class RequestDetailScreen extends ConsumerStatefulWidget {
  const RequestDetailScreen({super.key, required this.request});
  final BloodRequest request;
  @override
  ConsumerState<RequestDetailScreen> createState() =>
      _RequestDetailScreenState();
}

class _RequestDetailScreenState extends ConsumerState<RequestDetailScreen> {
  late BloodRequest request = widget.request;
  List<RequestHistoryEntry> history = [];
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
      history = await service.requestHistory(request.id);
      error = null;
    } catch (e) {
      error = e.toString();
    } finally {
      if (mounted) setState(() => loading = false);
    }
  }

  Future<String?> _askReason(String title, String hint) {
    final controller = TextEditingController();
    return showDialog<String>(
        context: context,
        builder: (ctx) => AlertDialog(
                title: Text(title),
                content: TextField(
                    controller: controller,
                    maxLength: 1000,
                    maxLines: 3,
                    decoration: InputDecoration(labelText: hint)),
                actions: [
                  TextButton(
                      onPressed: () => Navigator.pop(ctx),
                      child: const Text('Cancel')),
                  FilledButton(
                      onPressed: () =>
                          Navigator.pop(ctx, controller.text.trim()),
                      child: const Text('Confirm'))
                ]));
  }

  Future<void> _run(Future<BloodRequest?> Function() action) async {
    setState(() {
      busy = true;
      error = null;
    });
    try {
      final updated = await action();
      if (updated != null) request = updated;
      await _load();
    } catch (e) {
      error = e.toString();
    } finally {
      if (mounted) setState(() => busy = false);
    }
  }

  Future<void> _escalate() async {
    final reason = await _askReason('Escalate request', 'Reason');
    if (reason == null) return;
    await _run(() => service.escalate(request.id,
        reason.isEmpty ? 'Escalated from field application' : reason));
  }

  Future<void> _edit() async {
    final updated = await showModalBottomSheet<BloodRequest>(
        context: context,
        isScrollControlled: true,
        showDragHandle: true,
        builder: (_) => RequestFormSheet(service: service, existing: request));
    if (updated == null || !mounted) return;
    setState(() => request = updated);
    await _load();
  }

  Future<void> _close() async {
    final confirmed = await showDialog<bool>(
        context: context,
        builder: (ctx) => AlertDialog(
                title: const Text('Withdraw this request?'),
                content: const Text(
                    'Withdrawing stops donor matching for this request. This cannot be undone.'),
                actions: [
                  TextButton(
                      onPressed: () => Navigator.pop(ctx, false),
                      child: const Text('Keep open')),
                  FilledButton(
                      onPressed: () => Navigator.pop(ctx, true),
                      child: const Text('Withdraw request'))
                ]));
    if (confirmed != true || !mounted) return;
    setState(() {
      busy = true;
      error = null;
    });
    try {
      await service.cancelRequest(request.id);
      if (mounted) Navigator.pop(context);
    } catch (e) {
      if (mounted) {
        setState(() {
          error = e.toString();
          busy = false;
        });
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final open = preApprovalStatuses.contains(request.status);
    final committed =
        request.status == 'Approved' || request.status == 'Dispatched';
    final scheme = Theme.of(context).colorScheme;
    return LifeScaffold(
        appBar: AppBar(title: const Text('Request details')),
        body: ListView(
            physics: const AlwaysScrollableScrollPhysics(),
            padding: const EdgeInsets.all(20),
            children: [
              const PageIntro(
                  title: 'Every step, in view.',
                  subtitle:
                      'Track this request and review its latest status and recorded history.',
                  icon: Icons.timeline_outlined,
                  photo: false),
              Card(
                  child: ListTile(
                      leading: CircleAvatar(
                          child: Text(bloodLabel(request.bloodType))),
                      title: Text('${request.quantityUnits} unit(s)'),
                      subtitle: Text(
                          '${humanize(request.status)} · ${request.urgency}\nRequired ${formatDateTime(request.requiredByUtc)}'),
                      isThreeLine: true)),
              if (request.notes != null && request.notes!.isNotEmpty)
                Padding(
                    padding: const EdgeInsets.symmetric(vertical: 8),
                    child: Text('Notes: ${request.notes}')),
              if (error != null)
                Padding(
                    padding: const EdgeInsets.symmetric(vertical: 8),
                    child: Text(error!, style: TextStyle(color: scheme.error))),
              if (committed)
                const Padding(
                    padding: EdgeInsets.symmetric(vertical: 8),
                    child: Text(
                        'The blood bank has approved this request and committed stock. Contact the blood bank to change it.')),
              if (open) ...[
                const SizedBox(height: 8),
                SizedBox(
                    width: double.infinity,
                    child: OutlinedButton.icon(
                        onPressed: busy ? null : _edit,
                        icon: const Icon(Icons.edit_outlined),
                        label: const Text('Edit details'))),
                const SizedBox(height: 8),
                Row(children: [
                  if (!(request.status == 'Escalated' &&
                      request.urgency == 'Critical')) ...[
                    Expanded(
                        child: OutlinedButton.icon(
                            onPressed: busy ? null : _escalate,
                            icon: const Icon(Icons.priority_high),
                            label: const Text('Escalate'))),
                    const SizedBox(width: 12)
                  ],
                  Expanded(
                      child: OutlinedButton.icon(
                          onPressed: busy ? null : _close,
                          icon: const Icon(Icons.close),
                          label: const Text('Withdraw')))
                ])
              ],
              const SizedBox(height: 24),
              Text('Status history',
                  style: Theme.of(context).textTheme.titleMedium),
              if (loading)
                const Padding(
                    padding: EdgeInsets.all(16),
                    child: Center(child: CircularProgressIndicator()))
              else if (history.isEmpty)
                const Padding(
                    padding: EdgeInsets.symmetric(vertical: 8),
                    child: Text('No status changes yet.')),
              ...history.map((h) => ListTile(
                  dense: true,
                  leading: const Icon(Icons.timeline),
                  title: Text('${humanize(h.previous)} → ${humanize(h.next)}'),
                  subtitle: Text(
                      '${formatDateTime(h.changedAtUtc)}${h.reason == null || h.reason!.isEmpty ? '' : '\n${h.reason}'}')))
            ]));
  }
}
