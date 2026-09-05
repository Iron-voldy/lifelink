# LifeLink Completion Plan and TODO

Last verified: 18 August 2026

This is the canonical remaining-work list. A checked item means an implementation artifact
exists and its available local quality gate passed; it does not claim external deployment,
physical-device, or evaluator verification. The assignment PDF remains authoritative.

## Verified implementation baseline

- [x] .NET 8 layered API solution, EF Core PostgreSQL model, three migrations, health,
  Swagger, ProblemDetails, CORS, JWT/refresh rotation, RBAC, ownership, and correlation IDs.
- [x] Donor, hospital-request, inventory, camp, notification, and workflow services with
  lifecycle/domain operations, persistence, API endpoints, and baseline tests.
- [x] Four packaged LangGraph agents, allow-listed proposal tools, deterministic safety
  gate, Ollama adapter with bounded fallback, 12 golden scenarios, and resilience tests.
- [x] React admin application with protected routing, server-state caching, domain screens,
  inventory/camp operations, and workflow approve/reject/revise monitor.
- [x] Flutter donor/hospital application with secure refresh-token storage, GPS fallback,
  camp booking, hospital requests, and Firebase token/message handling.
- [x] Dockerfiles, local Compose topology, CI workflows, and a cross-platform live-system
  harness that records redacted workflow/audit evidence.
- [x] Reviewed ER model and six accepted ADRs. Student-number-to-name mapping remains a
  report task because names are not present in the repository.
- [x] Local gates currently pass: 36 backend tests, 27 agent tests, 3 React tests and web
  production build. Flutter previously passed analysis and its current unit test; rerun on
  the release machine.

## P0 — required before claiming application complete

- [ ] Manually verify the assignment PDF: exact rubric, ADR/report limits, naming,
  submission mechanics, team rules, demo/video rules, late policy, and prohibited tools.
- [ ] Run `integration-tests/run.ps1` with Docker and retain the JSON evidence/container
  logs. Docker is not installed in the current workspace, so the real PostgreSQL E2E has
  not yet executed.
- [ ] Add real PostgreSQL migration, constraint, rollback, transaction, reservation-race,
  camp-capacity-race, and approval-race tests (Testcontainers or CI PostgreSQL).
- [ ] Complete donor nearby urgent-request discovery/response and donation analytics.
- [x] Add hospital verification and suspension/rejection operations to the React admin.
- [ ] Complete hospital request detail/history in Flutter.
- [ ] Complete React donor histories, inventory reservation visibility, and camp attendance
  reporting; add real pagination controls and conflict/confirmation UX.
- [x] Add versioned JSON Schema files, strict boundary validation, and authenticated
  correlation ID propagation through API -> agent responses/log context.
- [ ] Add durable checkpoint/resume and idempotent graph retry behavior.
- [ ] Add API tests for double approval, concurrent workflow start, model/service failure,
  notification failure, and every RBAC/ownership boundary. Keep the new duplicate-active
  workflow guard covered.
- [ ] Expand React and Flutter unit/widget/navigation/API-mock suites; add Playwright browser
  automation and Flutter `integration_test` device automation over the live workflow.
- [ ] Run performance tests with CRUD and agent latency reported separately; retain
  throughput, percentile latency, error-rate, and environment evidence.
- [ ] Add dependency, secret, and static-security scanning plus coverage thresholds/artifacts.
- [ ] Configure Android release signing outside Git, build/install the release APK, and test
  GPS/FCM permission, background, denied, disabled-location, and notification-tap flows on
  a physical Android device.

## P0 — deployment and submission (requires team accounts/input)

- [ ] Implement provider configuration for accepted ADR-06 (Vercel web, Render API/agent,
  Neon PostgreSQL) under `infra/`; configure secrets, TLS, CORS, migrations, health probes,
  logs/alerts, backups, retention, staging/prod separation, smoke checks, and rollback.
- [ ] Deploy and record live URLs, evaluator accounts, APK link, uptime/cold-start plan, and
  verified access through 21 October 2026.
- [ ] Map Student 1–4 to legal names; assign implementer/reviewer ownership; configure remote,
  protected default branch, project board, pull requests, and contribution evidence.
- [ ] Complete four individual reports, dated AI-use logs, reflections, signed declarations,
  consolidated group PDF, diagrams/screenshots, test/performance evidence, and limitations.
- [ ] Rehearse the 10-minute demonstration/viva without external AI and perform clean-clone,
  incognito, evaluator-account, live-link, and APK-install checks.
- [ ] Tag and submit before 30 September 2026 at 11:50 PM.

## P1 quality completion

- [ ] Add API versioning and publish a frozen OpenAPI artifact or generated client contract.
- [ ] Add structured Serilog sinks and explicit audit actor/correlation fields where domain
  history currently relies on related records and request logging.
- [ ] Add synthetic seed/demo-data tooling that is repeatable and excludes personal data.
- [ ] Add backend-owned distance/ranking semantics; mobile may display location but must not
  become the business-authoritative matching engine.
- [ ] Add notification delivery-attempt history and bounded retry/backoff evidence.
- [ ] Complete accessibility audit, privacy/data-minimization review, backup/restore drill,
  observability review, and failure-recovery runbook.

## Next execution order

1. Verify the PDF and provide team names/provider accounts; these cannot be inferred.
2. Run the Compose E2E and repair any real PostgreSQL/contract defect it exposes.
3. Add real-PostgreSQL concurrency tests and agent JSON schemas/checkpoint semantics.
4. Finish the missing donor, hospital-admin, React detail/report, and Flutter history paths.
5. Add browser/device E2E, performance/security suites, then produce the signed APK.
6. Provision staging, run release checks, and finish submission evidence/reports.

The application must not be marked complete until every P0 item is checked with retained
evidence. Passing scaffold or in-memory tests alone is insufficient.

## Frontend redesign — 29 September 2026

The React website and Flutter app now share light/dark themes, local photography and fonts, responsive layouts, accessible controls, and reduced-motion-aware transitions. All 8 web pages and 10 Flutter screens were reviewed and updated. See [the page-by-page plan and verification record](frontend-redesign-plan.md) and [preview gallery](frontend-previews/README.md). This frontend milestone does not complete the live integration, deployment, or physical-device items above.
