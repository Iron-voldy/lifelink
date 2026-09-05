import '../../widgets/life_scaffold.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../core/validators.dart';
import '../../services/auth_controller.dart';
import '../../services/lifelink_service.dart';

class HospitalRegistrationScreen extends ConsumerStatefulWidget {
  const HospitalRegistrationScreen({super.key});
  @override
  ConsumerState<HospitalRegistrationScreen> createState() => _State();
}

class _State extends ConsumerState<HospitalRegistrationScreen> {
  final _form = GlobalKey<FormState>();
  final name = TextEditingController(),
      number = TextEditingController(),
      address = TextEditingController(),
      position = TextEditingController();
  bool loading = false;
  String? error;

  @override
  void dispose() {
    name.dispose();
    number.dispose();
    address.dispose();
    position.dispose();
    super.dispose();
  }

  Future<void> submit() async {
    if (!_form.currentState!.validate()) return;
    setState(() {
      loading = true;
      error = null;
    });
    try {
      await LifeLinkService(ref.read(apiProvider)).registerHospital({
        'name': name.text.trim(),
        'registrationNumber': number.text.trim(),
        'address': address.text.trim(),
        'latitude': null,
        'longitude': null,
        'position': position.text.trim()
      });
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(const SnackBar(
            content: Text('Hospital registration submitted for verification')));
        Navigator.pop(context);
      }
    } catch (e) {
      if (mounted) {
        setState(() {
          error = e.toString();
          loading = false;
        });
      }
    }
  }

  @override
  Widget build(BuildContext context) => LifeScaffold(
      appBar: AppBar(title: const Text('Register hospital')),
      body: Form(
          key: _form,
          child: ListView(
              physics: const AlwaysScrollableScrollPhysics(),
              padding: const EdgeInsets.all(20),
              children: [
                const PageIntro(
                    title: 'Connect your care team.',
                    subtitle:
                        'Register your facility for verification and join the LifeLink network.',
                    icon: Icons.business_outlined,
                    photo: false),
                TextFormField(
                    controller: name,
                    validator: (v) =>
                        Validators.required(v, 'Hospital name', max: 250),
                    decoration:
                        const InputDecoration(labelText: 'Hospital name')),
                const SizedBox(height: 10),
                TextFormField(
                    controller: number,
                    validator: (v) =>
                        Validators.required(v, 'Registration number', max: 100),
                    decoration: const InputDecoration(
                        labelText: 'Registration number')),
                const SizedBox(height: 10),
                TextFormField(
                    controller: address,
                    maxLines: 2,
                    validator: (v) =>
                        Validators.required(v, 'Address', max: 500),
                    decoration: const InputDecoration(labelText: 'Address')),
                const SizedBox(height: 10),
                TextFormField(
                    controller: position,
                    validator: (v) =>
                        Validators.required(v, 'Position', max: 150),
                    decoration: const InputDecoration(
                        labelText: 'Your position',
                        helperText: 'For example: Blood bank manager')),
                if (error != null)
                  Padding(
                      padding: const EdgeInsets.all(8),
                      child: Text(error!,
                          key: const Key('form-error'),
                          style: TextStyle(
                              color: Theme.of(context).colorScheme.error))),
                const SizedBox(height: 16),
                FilledButton(
                    onPressed: loading ? null : submit,
                    child:
                        Text(loading ? 'Submitting...' : 'Submit registration'))
              ])));
}
