import '../widgets/life_scaffold.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../core/format.dart';
import '../models/domain.dart';
import '../services/auth_controller.dart';
import '../services/lifelink_service.dart';

class NotificationsScreen extends ConsumerStatefulWidget {
  const NotificationsScreen({super.key});
  @override
  ConsumerState<NotificationsScreen> createState() =>
      _NotificationsScreenState();
}

class _NotificationsScreenState extends ConsumerState<NotificationsScreen> {
  List<AppNotification> items = [];
  bool loading = true;
  String? error;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    try {
      items = await LifeLinkService(ref.read(apiProvider)).notifications();
      error = null;
    } catch (e) {
      error = e.toString();
    } finally {
      if (mounted) setState(() => loading = false);
    }
  }

  @override
  Widget build(BuildContext context) => LifeScaffold(
      appBar: AppBar(title: const Text('Notifications')),
      body: loading
          ? const Center(child: CircularProgressIndicator())
          : RefreshIndicator(
              onRefresh: _load,
              child: ListView(
                  physics: const AlwaysScrollableScrollPhysics(),
                  padding: const EdgeInsets.all(16),
                  children: [
                    const PageIntro(
                        title: 'Stay close to what matters.',
                        subtitle:
                            'Your latest alerts and updates, together in one place.',
                        icon: Icons.notifications_none_outlined,
                        photo: false),
                    if (error != null)
                      Text(error!,
                          style: TextStyle(
                              color: Theme.of(context).colorScheme.error)),
                    if (error == null && items.isEmpty)
                      const Padding(
                          padding: EdgeInsets.all(32),
                          child: Center(child: Text('No notifications yet.'))),
                    ...items.map((n) => Card(
                        child: ListTile(
                            leading: Icon(n.status == 'Failed'
                                ? Icons.error_outline
                                : Icons.notifications),
                            title: Text(humanize(n.type.replaceAll('-', ' '))),
                            subtitle: Text(
                                '${humanize(n.status)} · ${formatDateTime(n.sentAtUtc ?? n.createdAtUtc)}'))))
                  ])));
}
