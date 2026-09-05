import 'package:flutter/material.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';

final appThemeMode = ValueNotifier<ThemeMode>(ThemeMode.system);
const _storage = FlutterSecureStorage();
Future<void> restoreTheme() async {
  try {
    final saved = await _storage.read(key: 'lifelink.theme');
    appThemeMode.value = saved == 'dark'
        ? ThemeMode.dark
        : saved == 'light'
            ? ThemeMode.light
            : ThemeMode.system;
  } catch (_) {/* Device preference remains available without storage. */}
}

Future<void> toggleTheme(BuildContext context) async {
  final dark = Theme.of(context).brightness != Brightness.dark;
  appThemeMode.value = dark ? ThemeMode.dark : ThemeMode.light;
  try {
    await _storage.write(key: 'lifelink.theme', value: dark ? 'dark' : 'light');
  } catch (_) {/* Keep the session preference. */}
}

ThemeData lifeLinkTheme(Brightness brightness) {
  final dark = brightness == Brightness.dark;
  final scheme = ColorScheme.fromSeed(
          seedColor: const Color(0xff146451), brightness: brightness)
      .copyWith(
    primary: dark ? const Color(0xff90d9b8) : const Color(0xff146451),
    onPrimary: dark ? const Color(0xff103c30) : Colors.white,
    surface: dark ? const Color(0xff192925) : Colors.white,
    onSurface: dark ? const Color(0xffe4eee8) : const Color(0xff203c37),
    primaryContainer: dark ? const Color(0xff213e33) : const Color(0xffe7f2eb),
    onPrimaryContainer:
        dark ? const Color(0xffb4e9cf) : const Color(0xff104c40),
    error: dark ? const Color(0xfff59aa5) : const Color(0xffb33c48),
    outlineVariant: dark ? const Color(0xff33473f) : const Color(0xffdfe6e0),
  );
  final shape = RoundedRectangleBorder(borderRadius: BorderRadius.circular(16));
  return ThemeData(
    useMaterial3: true,
    fontFamily: 'DMSans',
    brightness: brightness,
    colorScheme: scheme,
    scaffoldBackgroundColor:
        dark ? const Color(0xff101c1a) : const Color(0xfff5f6f3),
    appBarTheme: AppBarTheme(
        backgroundColor:
            dark ? const Color(0xff101c1a) : const Color(0xfff5f6f3),
        foregroundColor: scheme.onSurface,
        elevation: 0,
        scrolledUnderElevation: 0,
        centerTitle: false,
        titleTextStyle: TextStyle(
            fontFamily: 'DMSans',
            color: scheme.onSurface,
            fontSize: 19,
            fontWeight: FontWeight.w700,
            letterSpacing: -.5)),
    cardTheme: CardThemeData(
        color: scheme.surface,
        elevation: 0,
        margin: const EdgeInsets.only(bottom: 14),
        shape: shape.copyWith(side: BorderSide(color: scheme.outlineVariant)),
        clipBehavior: Clip.antiAlias),
    inputDecorationTheme: InputDecorationTheme(
        filled: true,
        fillColor: scheme.surface,
        contentPadding:
            const EdgeInsets.symmetric(horizontal: 18, vertical: 18),
        border: OutlineInputBorder(
            borderRadius: BorderRadius.circular(12),
            borderSide: BorderSide(color: scheme.outlineVariant)),
        enabledBorder: OutlineInputBorder(
            borderRadius: BorderRadius.circular(12),
            borderSide: BorderSide(color: scheme.outlineVariant)),
        focusedBorder: OutlineInputBorder(
            borderRadius: BorderRadius.circular(12),
            borderSide: BorderSide(color: scheme.primary, width: 2))),
    filledButtonTheme: FilledButtonThemeData(
        style: FilledButton.styleFrom(
            minimumSize: const Size(48, 52),
            shape:
                RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
            textStyle: const TextStyle(
                fontFamily: 'DMSans',
                fontWeight: FontWeight.w700,
                fontSize: 14))),
    outlinedButtonTheme: OutlinedButtonThemeData(
        style: OutlinedButton.styleFrom(
            minimumSize: const Size(48, 50),
            shape:
                RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
            side: BorderSide(color: scheme.outlineVariant))),
    listTileTheme: ListTileThemeData(
        contentPadding:
            const EdgeInsets.symmetric(horizontal: 18, vertical: 10),
        iconColor: scheme.primary,
        titleTextStyle: TextStyle(
            fontFamily: 'DMSans',
            color: scheme.onSurface,
            fontSize: 15,
            fontWeight: FontWeight.w600),
        subtitleTextStyle: TextStyle(
            fontFamily: 'DMSans',
            color: scheme.onSurfaceVariant,
            fontSize: 12,
            height: 1.6)),
    chipTheme: ChipThemeData(
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
        side: BorderSide(color: scheme.outlineVariant),
        padding: const EdgeInsets.all(8)),
    snackBarTheme:
        SnackBarThemeData(behavior: SnackBarBehavior.floating, shape: shape),
    bottomSheetTheme: BottomSheetThemeData(
        backgroundColor: scheme.surface,
        shape: const RoundedRectangleBorder(
            borderRadius: BorderRadius.vertical(top: Radius.circular(24)))),
    floatingActionButtonTheme: FloatingActionButtonThemeData(
        backgroundColor: scheme.primary,
        foregroundColor: scheme.onPrimary,
        elevation: 0,
        focusElevation: 0,
        hoverElevation: 0),
    dividerTheme: DividerThemeData(color: scheme.outlineVariant),
    pageTransitionsTheme: const PageTransitionsTheme(builders: {
      TargetPlatform.android: LifePageTransitionsBuilder(),
      TargetPlatform.iOS: LifePageTransitionsBuilder(),
      TargetPlatform.windows: LifePageTransitionsBuilder()
    }),
  );
}

class LifePageTransitionsBuilder extends PageTransitionsBuilder {
  const LifePageTransitionsBuilder();
  @override
  Widget buildTransitions<T>(
      PageRoute<T> route,
      BuildContext context,
      Animation<double> animation,
      Animation<double> secondaryAnimation,
      Widget child) {
    if (MediaQuery.disableAnimationsOf(context)) return child;
    return FadeTransition(
        opacity: animation.drive(CurveTween(curve: Curves.easeOut)),
        child: child);
  }
}
