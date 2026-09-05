import '../widgets/life_scaffold.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../core/validators.dart';
import '../services/auth_controller.dart';

class RegisterScreen extends ConsumerStatefulWidget {
  const RegisterScreen({super.key});
  @override
  ConsumerState<RegisterScreen> createState() => _RegisterScreenState();
}

class _RegisterScreenState extends ConsumerState<RegisterScreen> {
  final _form = GlobalKey<FormState>();
  final email = TextEditingController(),
      password = TextEditingController(),
      confirm = TextEditingController();
  String role = 'Donor';
  bool obscure = true;

  @override
  void dispose() {
    email.dispose();
    password.dispose();
    confirm.dispose();
    super.dispose();
  }

  void _submit() {
    if (!_form.currentState!.validate()) return;
    ref
        .read(authProvider.notifier)
        .register(email.text.trim(), password.text, role);
  }

  @override
  Widget build(BuildContext context) {
    final auth = ref.watch(authProvider);
    ref.listen(authProvider, (_, next) {
      if (next.hasValue && next.value != null && Navigator.canPop(context)) {
        Navigator.pop(context);
      }
    });
    return LifeScaffold(
        appBar: AppBar(title: const Text('Create account')),
        body: Form(
            key: _form,
            child: ListView(
                physics: const AlwaysScrollableScrollPhysics(),
                padding: const EdgeInsets.all(24),
                children: [
                  const PageIntro(
                      title: 'Join a network of care.',
                      subtitle:
                          'Create your account and take the first step toward making a difference.',
                      icon: Icons.favorite_outline,
                      photo: false),
                  TextFormField(
                      controller: email,
                      keyboardType: TextInputType.emailAddress,
                      validator: Validators.email,
                      decoration: const InputDecoration(labelText: 'Email')),
                  const SizedBox(height: 12),
                  TextFormField(
                      controller: password,
                      obscureText: obscure,
                      validator: Validators.strongPassword,
                      decoration: InputDecoration(
                          labelText: 'Password',
                          helperText:
                              '12+ characters with upper, lower, digit and symbol',
                          helperMaxLines: 2,
                          suffixIcon: IconButton(
                              tooltip:
                                  obscure ? 'Show password' : 'Hide password',
                              onPressed: () =>
                                  setState(() => obscure = !obscure),
                              icon: Icon(obscure
                                  ? Icons.visibility
                                  : Icons.visibility_off)))),
                  const SizedBox(height: 12),
                  TextFormField(
                      controller: confirm,
                      obscureText: obscure,
                      validator: Validators.matches(() => password.text),
                      decoration:
                          const InputDecoration(labelText: 'Confirm password')),
                  const SizedBox(height: 16),
                  SegmentedButton<String>(segments: const [
                    ButtonSegment(
                        value: 'Donor',
                        label: Text('Donor'),
                        icon: Icon(Icons.favorite)),
                    ButtonSegment(
                        value: 'HospitalRequester',
                        label: Text('Hospital'),
                        icon: Icon(Icons.local_hospital))
                  ], selected: {
                    role
                  }, onSelectionChanged: (x) => setState(() => role = x.first)),
                  if (auth.hasError)
                    Padding(
                        padding: const EdgeInsets.only(top: 12),
                        child: Text(auth.error.toString(),
                            key: const Key('auth-error'),
                            style: TextStyle(
                                color: Theme.of(context).colorScheme.error))),
                  const SizedBox(height: 20),
                  FilledButton(
                      onPressed: auth.isLoading ? null : _submit,
                      child: Text(auth.isLoading
                          ? 'Creating account...'
                          : 'Create account'))
                ])));
  }
}
