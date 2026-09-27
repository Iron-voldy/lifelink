import '../widgets/life_scaffold.dart';
import 'dart:async';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../core/format.dart';
import '../services/auth_controller.dart';
import '../services/lifelink_service.dart';
import '../services/push_notification_service.dart';
import 'camps/camps_screen.dart';
import 'donor/profile_screen.dart';
import 'hospital/hospital_registration_screen.dart';
import 'notifications_screen.dart';

class HomeScreen extends ConsumerStatefulWidget {
  const HomeScreen({super.key});
  @override
  ConsumerState<HomeScreen> createState() => _HomeScreenState();
}

class _HomeScreenState extends ConsumerState<HomeScreen> {
  PushNotificationService? push;
  StreamSubscription<dynamic>? messageSubscription;
  Map<String, dynamic>? hospitalProfile;
  bool hospitalLoading = true;
  String? hospitalError;

  Future<void> _loadHospital() async {
    if (ref.read(authProvider).value?.role != 'HospitalRequester') return;
    try {
      hospitalProfile =
          await LifeLinkService(ref.read(apiProvider)).myHospital();
      hospitalError = null;
    } catch (e) {
      hospitalError = e.toString();
    } finally {
      if (mounted) setState(() => hospitalLoading = false);
    }
  }

  @override
  void initState() {
    super.initState();
    unawaited(_loadHospital());
    push = PushNotificationService(ref.read(apiProvider));
    unawaited(push!.start());
    messageSubscription = push!.messages.listen((message) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(SnackBar(
            content: Text(message.notification?.body ??
                message.data['message'] ??
                'LifeLink update received')));
      }
    });
  }

  @override
  void dispose() {
    unawaited(messageSubscription?.cancel());
    unawaited(push?.dispose());
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final session = ref.watch(authProvider).value!;
    final hospital = session.role == 'HospitalRequester';
    final supported = hospital || session.role == 'Donor';
    void open(Widget page) =>
        Navigator.push(context, MaterialPageRoute(builder: (_) => page));
    return LifeScaffold(
        appBar: AppBar(title: const Text('LifeLink'), actions: [
          IconButton(
              tooltip: 'Sign out',
              onPressed: () => ref.read(authProvider.notifier).logout(),
              icon: const Icon(Icons.logout))
        ]),
        body: ListView(
            physics: const AlwaysScrollableScrollPhysics(),
            padding: const EdgeInsets.all(20),
            children: [
              const PageIntro(
                  title: 'Together, we keep life moving.',
                  subtitle:
                      'A connected community. A shared purpose. Your next life-changing connection starts here.',
                  photo: true),
              Text(
                  hospital ? 'Your hospital workspace' : 'Your donor dashboard',
                  style: Theme.of(context).textTheme.headlineSmall),
              const SizedBox(height: 8),
              Text(session.email,
                  style: Theme.of(context).textTheme.bodyMedium),
              const SizedBox(height: 24),
              if (!supported)
                const Card(
                    child: ListTile(
                        leading: Icon(Icons.desktop_windows_outlined),
                        title: Text('Use the LifeLink web workspace'),
                        subtitle: Text(
                            'This account manages LifeLink from the admin web app. The mobile app is for donors and hospital staff.')))
              else if (hospital) ...[
                if (hospitalLoading)
                  const Card(
                      child: ListTile(
                          leading: Icon(Icons.hourglass_empty),
                          title: Text('Checking your hospital status...')))
                else if (hospitalProfile == null && hospitalError == null)
                  _Action(
                      icon: Icons.business,
                      title: 'Register hospital',
                      subtitle:
                          'Required before this account can submit blood requests',
                      onTap: () async {
                        await Navigator.push(
                            context,
                            MaterialPageRoute(
                                builder: (_) =>
                                    const HospitalRegistrationScreen()));
                        if (mounted) await _loadHospital();
                      })
                else
                  HospitalStatusCard(
                      hospital: hospitalProfile, error: hospitalError),
              ] else ...[
                _Action(
                    icon: Icons.health_and_safety,
                    title: 'Eligibility profile',
                    subtitle: 'Review donation readiness',
                    onTap: () => open(const DonorProfileScreen())),
                _Action(
                    icon: Icons.event,
                    title: 'Donation camps',
                    subtitle: 'Discover nearby camps and book a slot',
                    onTap: () => open(const CampsScreen()))
              ],
              if (supported)
                _Action(
                    icon: Icons.notifications,
                    title: 'Notifications',
                    subtitle: 'Recent alerts and delivery status',
                    onTap: () => open(const NotificationsScreen()))
            ]));
  }
}

class _Action extends StatelessWidget {
  const _Action(
      {required this.icon,
      required this.title,
      required this.subtitle,
      required this.onTap});
  final IconData icon;
  final String title;
  final String subtitle;
  final VoidCallback onTap;
  @override
  Widget build(BuildContext context) => Card(
      child: ListTile(
          onTap: onTap,
          leading: Container(
              padding: const EdgeInsets.all(12),
              decoration: BoxDecoration(
                  color: Theme.of(context).colorScheme.primaryContainer,
                  borderRadius: BorderRadius.circular(14)),
              child: Icon(icon)),
          title: Text(title),
          subtitle: Text(subtitle),
          trailing: const Icon(Icons.chevron_right)));
}

/// Shows hospital staff where their facility stands, so a pending or suspended
/// hospital understands why new requests are refused.
class HospitalStatusCard extends StatelessWidget {
  const HospitalStatusCard({super.key, this.hospital, this.error});
  final Map<String, dynamic>? hospital;
  final String? error;

  static const _explanations = {
    'Pending':
        'Awaiting verification by the blood bank. You can submit requests once it is verified.',
    'Verified': 'Verified. You can submit, edit and escalate blood requests.',
    'Rejected':
        'Registration was rejected. Contact the blood bank to resolve it before submitting requests.',
    'Suspended':
        'Suspended by the blood bank. You can view or withdraw existing requests, but not submit new ones.'
  };

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    if (hospital == null) {
      return Card(
          child: ListTile(
              leading: Icon(Icons.error_outline, color: scheme.error),
              title: const Text('Hospital status unavailable'),
              subtitle: Text(error ?? 'Please try again later.')));
    }
    final status = '${hospital!['verificationStatus']}';
    final verified = status == 'Verified';
    return Card(
        child: ListTile(
            leading: Icon(verified ? Icons.verified : Icons.pending_actions,
                color: verified ? scheme.primary : scheme.error),
            title: Text('${hospital!['name']}'),
            subtitle: Text(
                '${hospital!['registrationNumber']} · ${humanize(status)}\n${_explanations[status] ?? ''}'),
            isThreeLine: true));
  }
}
