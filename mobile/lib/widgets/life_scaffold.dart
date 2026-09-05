import 'package:flutter/material.dart';
import '../core/app_theme.dart';

/// A consistent, width-bounded field workspace with motion and theme access.
class LifeScaffold extends StatelessWidget {
  const LifeScaffold(
      {super.key, this.appBar, required this.body, this.floatingActionButton});
  final AppBar? appBar;
  final Widget body;
  final Widget? floatingActionButton;
  @override
  Widget build(BuildContext context) {
    final reduced = MediaQuery.disableAnimationsOf(context);
    final dark = Theme.of(context).brightness == Brightness.dark;
    return Scaffold(
      appBar: AppBar(
          title: appBar?.title,
          automaticallyImplyLeading: appBar?.automaticallyImplyLeading ?? true,
          leading: appBar?.leading,
          actions: [
            IconButton(
                tooltip: dark ? 'Switch to light mode' : 'Switch to dark mode',
                onPressed: () => toggleTheme(context),
                icon: Icon(dark
                    ? Icons.light_mode_outlined
                    : Icons.dark_mode_outlined)),
            ...?appBar?.actions,
            const SizedBox(width: 8),
          ]),
      floatingActionButton: floatingActionButton,
      body: SafeArea(
          top: false,
          child: Center(
              child: ConstrainedBox(
                  constraints: const BoxConstraints(maxWidth: 900),
                  child: TweenAnimationBuilder<double>(
                    tween: Tween(begin: reduced ? 1 : 0, end: 1),
                    duration: reduced
                        ? Duration.zero
                        : const Duration(milliseconds: 380),
                    builder: (context, value, child) => Opacity(
                        opacity: value,
                        child: Transform.translate(
                            offset: Offset(0, 12 * (1 - value)), child: child)),
                    child: body,
                  )))),
    );
  }
}

class PageIntro extends StatelessWidget {
  const PageIntro(
      {super.key,
      required this.title,
      required this.subtitle,
      this.icon = Icons.favorite_outline,
      this.photo = false,
      this.imagePath = 'assets/images/care-team.jpg'});
  final String title, subtitle;
  final IconData icon;
  final bool photo;
  final String imagePath;
  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    return Container(
      width: double.infinity,
      margin: const EdgeInsets.only(bottom: 24),
      clipBehavior: Clip.antiAlias,
      decoration: BoxDecoration(
          color: photo ? const Color(0xff123e33) : scheme.primaryContainer,
          borderRadius: BorderRadius.circular(20),
          image: photo
              ? DecorationImage(image: AssetImage(imagePath), fit: BoxFit.cover)
              : null),
      child: Container(
        padding: const EdgeInsets.all(24),
        decoration: photo
            ? const BoxDecoration(
                gradient: LinearGradient(
                    colors: [Color(0xf5103f37), Color(0xb8103f37)],
                    begin: Alignment.centerLeft,
                    end: Alignment.centerRight))
            : null,
        child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
          Icon(icon,
              color: photo ? const Color(0xffedc0a6) : scheme.primary,
              size: 26),
          const SizedBox(height: 18),
          Text(title,
              style: TextStyle(
                  fontSize: 25,
                  height: 1.15,
                  fontWeight: FontWeight.w800,
                  letterSpacing: -.8,
                  color: photo ? Colors.white : scheme.onPrimaryContainer)),
          const SizedBox(height: 10),
          Text(subtitle,
              style: TextStyle(
                  fontSize: 13,
                  height: 1.6,
                  color: photo
                      ? const Color(0xffd4e4db)
                      : scheme.onPrimaryContainer)),
        ]),
      ),
    );
  }
}
