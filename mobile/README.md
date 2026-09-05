# LifeLink Flutter application

The Flutter client provides distinct field workflows for donors and hospital requesters.

## Implemented feature foundation

- Donor/hospital registration and login
- Rotating refresh-token restoration using secure storage
- Role-specific navigation
- Donor eligibility profile creation/update with optional GPS coordinates
- Hospital registration, request submission/tracking, and escalation
- Scheduled-camp discovery, distance sorting, and booking
- Firebase token registration, token rotation, foreground/background delivery and notification-tap handling
- API failures and location-permission denial degrade to usable manual flows

## Configuration and run

Flutter is not bundled with this repository. After installing a stable Flutter SDK, generate the Android host project if it is absent:

```text
flutter create --project-name lifelink_mobile --platforms android .
flutter pub get
flutter analyze
flutter test
flutter run --dart-define=LIFELINK_API_BASE_URL=http://10.0.2.2:5080 --dart-define=LIFELINK_FIREBASE_ENABLED=false
```

`10.0.2.2` reaches the host from the Android emulator. Use the computer's LAN address for a physical device. The CI workflow performs platform generation, analysis, tests, release APK compilation, and artifact upload.

## Structure

- `lib/models/`: API response models
- `lib/services/`: authenticated HTTP, secure session, domain operations
- `lib/screens/donor/`: donor profile and eligibility
- `lib/screens/hospital/`: hospital onboarding and blood requests
- `lib/screens/camps/`: GPS-aware camp discovery and booking

Set `LIFELINK_FIREBASE_ENABLED=true` only after adding the platform Firebase configuration generated for the evaluator-safe project. Server credentials belong only in the API environment.

## Frontend themes and review

`lib/core/app_theme.dart` defines the shared light/dark palette and persisted theme choice. `lib/widgets/life_scaffold.dart` provides responsive page widths, theme controls, and reduced-motion-aware entrance effects. Photos and fonts are bundled in `assets/`.

`flutter test` includes all-screen layout checks in both themes with enlarged text. To save review images, run `flutter test --dart-define=CAPTURE_UI=true test/redesign_test.dart`; images appear in `build/ui-review/`.

See [the page-by-page redesign plan](../docs/frontend-redesign-plan.md) and [preview gallery](../docs/frontend-previews/README.md).
