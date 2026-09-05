# Frontend review and redesign

Reviewed 29 September 2026. Scope confirmed: all eight React website routes and all ten Flutter screens.

## Page-by-page investigation and plan

| Page | Findings | Implementation plan |
| --- | --- | --- |
| Login | Heavy text-only split, no theme choice, tiny labels | Shaded healthcare photograph, clear welcome form, password visibility, theme control, responsive brand |
| Overview | Bare metrics, arbitrary stock bars, empty panels, unconditional live claim | Editorial welcome banner, icon metrics, honest query status, labelled relative stock chart, useful empty states, shortcuts |
| Donors | Dense table, weak search hierarchy, silent mutation failures | Clear registry toolbar, readable blood/status badges, phone card layout, action feedback |
| Hospitals | Plain cards, limited facility identity | Facility icons, structured cards, readable registration/address hierarchy, distinct verification actions |
| Requests | Urgency under-emphasized, silent transition failures | Urgency accents, spacious triage cards, clear deadlines and status controls, pending/error feedback |
| Inventory | Dense lots and hidden forms, errors not surfaced | Expiry notice, improved forms, legible lot table, action feedback |
| Camps | Flat list, no community imagery, weak occupancy hierarchy | Shaded community banner, date-led event cards, responsive capacity display, form/action feedback |
| Workflows | Cramped master-detail layout and technical text overflow | Clear queue selection, readable timeline, wrapped identifiers, distinct decision panel, accessible selection |

## Shared design

Warm ivory / deep teal light theme; charcoal / mint dark theme; coral for urgency and brand. Semantic theme tokens cover every control, surface, status, and form. Persist explicit theme choice and initially respect device preference. Provide mobile menu, visible focus rings, touch-sized controls, page entrance and hover motion with reduced-motion support. Use locally stored photographs with dark overlays for readable text. Keep real API operations and authorization intact.

## Review gates

- Production TypeScript/Vite build and existing unit tests.
- Browser review of every route at desktop and mobile widths, both themes, using clearly isolated mock API fixtures if the backend is unavailable.
- Verify navigation, theme persistence, forms, error/empty/loading states, and horizontal page overflow.
- Record evidence and any remaining limitations here after review.
## Flutter investigation and page plan

The user confirmed that both apps are in scope. All ten Flutter screens were inspected before their shared layout and page changes.

| Screen | Findings | Implemented direction |
| --- | --- | --- |
| Login | Default form, little identity, no theme setting | Shaded welcome photograph, clear welcome text, local font, accessible theme control |
| Account registration | Long unstructured form | Intro panel, rounded filled fields, consistent account-type selector and buttons |
| Home | Plain list with raw greeting | Photo welcome, service cards with icon tiles, responsive content width |
| Eligibility profile | Dense inputs/history; long status text at risk of overflow | Clear profile introduction, themed status surface, readable forms and history |
| Camp discovery | Basic list, no visual identity | Donation photograph, themed event cards, readable availability |
| Camp details | Flat location/booking controls | Photo introduction, clearer booking context, themed time chips and buttons |
| Hospital registration | Default fields without context | Facility introduction and consistent validated field layout |
| Hospital requests | Flat list and floating action | Care-focused introduction, urgency-aware colors and blood group cards |
| Request details | Dense summary and history | Clear introduction, request summary, themed action buttons and timeline |
| Notifications | Bare list and empty text | Dedicated introduction, themed delivery cards and readable empty state |

All screens use `LifeScaffold`: a width-bounded layout, light/dark control, safe-area handling, and a short entrance transition that respects reduced-motion settings. Theme preference uses existing secure storage and survives sign-out. Local fonts and photos are bundled for offline rendering.
## Completed review — 29 September 2026

- Web production build passed; 8 existing unit tests passed.
- 11 Playwright browser tests passed: all seven authenticated routes at 320, 390, 820 and 1440 CSS pixels in both themes; login at 390 and 1440; theme persistence, mobile navigation, password visibility, forms, empty states and API failures.
- Flutter analysis passed without issues using the current IDE SDK at `D:\flutter\flutter`.
- 22 Flutter tests passed, including all ten screens at 320, 390 and 820 logical pixels in both themes with text scaled to 130%, plus theme restoration and persistence after logout.
- Inspected screenshots for every web page and every mobile screen. Refined phone dashboard overflow, workflow queue clipping, phone table layouts, component typography and theme contrast after review.
- Web fonts and both photographs are local assets. Font licenses and image-source notes are included alongside the assets.
- Reduced-motion preferences disable web transitions and Flutter page entrance/navigation transitions.

[Open the preview gallery](frontend-previews/README.md).

### Reproduce

From `web`: `npm run build`, `npm test`, `npm run test:browser`. Browser tests start their own Vite instance on port 5174 and intercept the API; no live records are changed. Windows uses installed Edge; other platforms use Playwright Chromium (`npx playwright install chromium`). Override the browser with `PLAYWRIGHT_CHANNEL` if needed. All browser screenshots are in `web/test-results/`.

From `mobile`: `flutter analyze`, `flutter test`. To regenerate mobile review images: `flutter test --dart-define=CAPTURE_UI=true test/redesign_test.dart`. Captures are written to `mobile/build/ui-review/`.

### Verification limits

Browser and Flutter layout checks use isolated sample API fixtures. These verify UI rendering and interactions, not live backend integration, GPS, Firebase delivery, or a physical Android installation. Existing project completion requirements remain separate from this frontend redesign.
