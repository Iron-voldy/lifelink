import '../widgets/life_scaffold.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../core/validators.dart';
import '../services/auth_controller.dart';
import 'register_screen.dart';

class LoginScreen extends ConsumerStatefulWidget {
  const LoginScreen({super.key});
  @override
  ConsumerState<LoginScreen> createState() => _LoginScreenState();
}

class _LoginScreenState extends ConsumerState<LoginScreen> {
  final _form = GlobalKey<FormState>();
  final email = TextEditingController();
  final password = TextEditingController();
  bool obscure = true;

  @override
  void dispose() {
    email.dispose();
    password.dispose();
    super.dispose();
  }

  void _submit() {
    if (!_form.currentState!.validate()) return;
    ref.read(authProvider.notifier).login(email.text.trim(), password.text);
  }

  @override
  Widget build(BuildContext context) {
    final auth = ref.watch(authProvider);
    final scheme = Theme.of(context).colorScheme;
    return LifeScaffold(
        body: SafeArea(
            child: Center(
                child: SingleChildScrollView(
                    padding: const EdgeInsets.all(24),
                    child: ConstrainedBox(
                        constraints: const BoxConstraints(maxWidth: 420),
                        child: Form(
                            key: _form,
                            child: Column(
                                crossAxisAlignment: CrossAxisAlignment.stretch,
                                children: [
                                  const PageIntro(
                                      title: 'Every match. One lifeline.',
                                      subtitle: 'LifeLink · Care, connected.',
                                      photo: true),
                                  Text('Welcome back',
                                      style: Theme.of(context)
                                          .textTheme
                                          .headlineMedium
                                          ?.copyWith(
                                              fontWeight: FontWeight.w800,
                                              letterSpacing: -1)),
                                  const SizedBox(height: 8),
                                  const Text(
                                      'Sign in to your donor or hospital account.'),
                                  const SizedBox(height: 24),
                                  TextFormField(
                                      controller: email,
                                      keyboardType: TextInputType.emailAddress,
                                      autofillHints: const [
                                        AutofillHints.email
                                      ],
                                      textInputAction: TextInputAction.next,
                                      validator: Validators.email,
                                      decoration: const InputDecoration(
                                          labelText: 'Email')),
                                  const SizedBox(height: 12),
                                  TextFormField(
                                      controller: password,
                                      obscureText: obscure,
                                      autofillHints: const [
                                        AutofillHints.password
                                      ],
                                      onFieldSubmitted: (_) => _submit(),
                                      validator: Validators.passwordPresent,
                                      decoration: InputDecoration(
                                          labelText: 'Password',
                                          suffixIcon: IconButton(
                                              tooltip: obscure
                                                  ? 'Show password'
                                                  : 'Hide password',
                                              onPressed: () => setState(
                                                  () => obscure = !obscure),
                                              icon: Icon(obscure
                                                  ? Icons.visibility
                                                  : Icons.visibility_off)))),
                                  const SizedBox(height: 20),
                                  FilledButton(
                                      onPressed:
                                          auth.isLoading ? null : _submit,
                                      child: Text(auth.isLoading
                                          ? 'Signing in...'
                                          : 'Sign in')),
                                  TextButton(
                                      onPressed: () => Navigator.push(
                                          context,
                                          MaterialPageRoute(
                                              builder: (_) =>
                                                  const RegisterScreen())),
                                      child: const Text(
                                          'Create donor or hospital account')),
                                  if (auth.hasError)
                                    Padding(
                                        padding: const EdgeInsets.only(top: 12),
                                        child: Text(auth.error.toString(),
                                            key: const Key('auth-error'),
                                            style:
                                                TextStyle(color: scheme.error)))
                                ])))))));
  }
}
