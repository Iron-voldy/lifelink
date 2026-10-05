# LifeLink Industry Readiness Research

**Reviewed:** 5 October 2026  
**Scope:** Repository review of the implemented application, deployment configuration, recorded completion plan, and relevant public standards.  
**Purpose:** An engineering gap assessment and prioritized roadmap. This is not a certification, penetration test, clinical validation, or legal opinion.

## Executive assessment

LifeLink is a credible academic full-stack prototype with a stronger-than-usual safety architecture for an agentic application: the ASP.NET Core API owns business authority and persistence, model output is constrained to proposals, deterministic validation is separate from the model, sensitive actions require administrator approval, and workflow evidence is persisted. The project has meaningful breadth across web, mobile, API, database, notifications, and AI orchestration.

The repository does **not** yet support a claim of production readiness for real donor or patient data. The completion plan itself marks real-PostgreSQL concurrency coverage, workflow recovery/idempotency, several role-specific features, security scanning, operational controls, deployment evidence, privacy review, and physical-device validation as incomplete. No live production environment or independent security/privacy/clinical assessment is evidenced here. The practical rating is **strong prototype; production readiness unproven**.

The assessment uses OWASP ASVS 5.0 as a verification baseline, OWASP API Security Top 10 (2023) for API risk categories, OWASP LLM Top 10 (2025) for agent risks, NIST AI RMF as a voluntary governance lens, W3C WCAG 2.2 for accessibility, and Sri Lanka's current data-protection materials. These are reference frameworks; citing or mapping to them does not imply conformance.

## What the repository demonstrates

- **Architecture and authority:** React and Flutter use the API; the API controls database access, validation, authorization, notification providers, and approval effects. Layered .NET projects, EF Core migrations, PostgreSQL, health checks, ProblemDetails, CORS, JWT validation, correlation IDs, and rate limiting are present.
- **Agent safety:** LangGraph orchestration is internal; tool functions are allow-listed; schemas are versioned; a deterministic safety gate and fallback are present; prompts/reasoning traces are not intended for persistence; administrator approvals are part of the workflow.
- **Business scope:** Donors, hospitals, requests, inventory, camps, notifications, and workflow approvals are represented across clients and backend services.
- **Quality practices:** Unit tests exist for the backend, agent service, and web client. GitHub Actions cover these stacks and build the container and Flutter artifacts. An integration harness records workflow evidence.
- **Operational awareness:** Docker Compose health checks, generated deployment secrets, startup checks for insecure placeholders, a deployment script, and a remaining-work plan show attention to operational risks.

These are implementation signals, not independent proof that controls resist adversarial use or operational failure.

## Standards-informed findings

| Area | Evidence and gap | Priority | Recommended closure evidence |
|---|---|---:|---|
| Workflow correctness under concurrency | The canonical build plan explicitly calls out real PostgreSQL reservation, camp-capacity, approval, and migration tests. In-memory/unit coverage cannot establish transaction isolation, uniqueness, locking, or rollback behavior under concurrent requests. | P0 | PostgreSQL-backed CI tests for simultaneous reservation, duplicate approval/start, camp overbooking, migration from empty and prior schema, rollback, and restart after partial failure; retain CI artifacts. |
| Agent recovery and idempotency | The ADR and build plan say durable checkpoint/resume and idempotent retry are unfinished. A human approval may trigger stock reservation and notifications; retries or process crashes around these side effects need exactly-defined outcomes. | P0 | Durable state transitions, idempotency keys/unique constraints for effects, transactional outbox or equivalent, recovery tests for crash points, duplicate callbacks, and rejected/revised plans. |
| Authorization and sensitive flows | Controllers show role policies and auth throttling; completion notes call for exhaustive ownership/RBAC and duplicate-approval tests. OWASP's API categories emphasize object/function-level authorization and protection for sensitive business flows. | P0 | Automated per-role/per-owner matrix covering every route and object ID, negative tests for property over-posting, workflow decision race tests, and abuse controls for registration, request creation, donor broadcasts, and notification endpoints. |
| Privacy and health-related data | Donor blood type, eligibility, location/address, donation history, hospital requests, and device push tokens are sensitive in context. The repo calls for a privacy review but does not evidence a data inventory, retention/deletion rules, subject-rights process, breach procedure, or transfer/vendor assessment. | P0 | Data-flow and purpose inventory; minimize collected fields; documented retention/deletion and access policy; encryption/key management review; access/audit review; processor and hosting locations; incident/breach runbook; documented counsel/DPA assessment against the amended Sri Lankan law and current gazettes. |
| Security assurance and supply chain | CI builds and tests, but the completion plan records dependency, secret, and static-security scanning as outstanding. ASVS is a verification standard; OWASP API risks also include misconfiguration, inventory management, and unsafe API consumption. | P1 | Pin direct/transitive dependency versions; automated dependency/SBOM, secret, SAST, and container scans with triage; ASVS 5.0 control checklist and evidence; external penetration test before real-data launch; API inventory/OpenAPI review. |
| Agent risk measurement | Deterministic safety gates and golden scenarios are strong controls. The recorded corpus is small and primarily functional. OWASP LLM guidance highlights prompt injection, sensitive information disclosure, output handling, excessive agency, misinformation, and unbounded consumption. NIST AI RMF calls for ongoing governance, mapping, measurement, and management of risk. | P1 | Threat model including direct/indirect prompt injection and compromised model/service; broaden adversarial and property-based cases; score matching accuracy, unsafe proposal rate, false positives/negatives and fallback frequency; model/version change evaluation; human override review; service-level budgets and kill switch. Keep final eligibility/compatibility decisions deterministic and clinically reviewed. |
| Reliability and observability | Health checks and correlation IDs exist. The plan identifies missing durable retry evidence, structured audit fields, logs/alerts, backups, restore drills, and failure runbooks. | P1 | SLOs for API/workflow/notification latency and availability; structured redacted logs, metrics and traces; alert routes; tested backup restore and RPO/RTO; incident and recovery runbooks; dependency outage and queue backlog drills. |
| Notification delivery | An outbox worker and Firebase/logging providers are present; delivery attempt history/retry evidence remains open. A log provider is useful for development but is not delivery proof. | P1 | Durable attempts, bounded exponential retry with jitter, dead-letter/manual replay, provider receipts, deduplication, opt-out/consent semantics, token invalidation, delivery and failure metrics; prevent PHI in push text. |
| Mobile and client security | Flutter includes secure refresh storage according to project docs, location fallback, and Firebase support. Physical Android behavior and release signing/device flows are not verified in the completion plan. | P1 | Signed release build; verify storage, token revocation, permissions, denied/disabled location, background notifications, deep links, screenshots/backup leakage, and TLS on physical supported Android devices; do not put service credentials in clients. |
| Accessibility and usability | The redesign plan reports accessible controls and responsive themes, but no formal WCAG audit is recorded. W3C WCAG 2.2 is the current cited recommendation baseline. | P2 | Aim for WCAG 2.2 AA for web; keyboard/screen-reader/manual checks and automated scans; test contrast, focus, touch target size, zoom/reflow, error announcements, and reduced motion. Validate mobile assistive technology separately. |
| API lifecycle and contracts | The completion plan identifies API versioning and a frozen OpenAPI artifact as unfinished. Clients independently encode DTO shapes. | P2 | Publish versioned OpenAPI in CI, validate backward compatibility, define deprecation policy, generate or contract-test clients, and bound pagination/filter inputs consistently. |
| Blood-service domain validation | Matching and expiry/compatibility tooling is described and tested, but this repository does not evidence approval by a transfusion-medicine authority or validation against operational blood-bank procedures. | P0 before real operations | Have local licensed transfusion professionals review compatibility, component, expiry, allocation, donor eligibility, escalation, and override rules. Treat software output as coordination support, not a clinical decision; record rule provenance and version. |

### Sri Lanka data protection note

The official Data Protection Authority site now lists both the Personal Data Protection Act No. 9 of 2022 and the Personal Data Protection (Amendment) Act No. 22 of 2025, and lists a Gazette dated 22 July 2026. Therefore, older project notes or assumptions based only on the original Act or earlier announced commencement dates need re-checking. The legal applicability, commencement, cross-border processing, and duties for a particular operator must be confirmed from the current Act, gazettes, DPA directions, and Sri Lankan counsel; this repository review cannot settle those questions.

## Prioritized roadmap

### Gate 1 — safe correctness before real data

1. Close real-PostgreSQL transaction and concurrency coverage for requests, stock, camp capacity, and workflow decisions.
2. Make approval effects idempotent and recoverable across API/worker restarts; test all duplicate and partial-failure paths.
3. Complete endpoint-level authorization, ownership, rate/abuse, and data-exposure checks using an explicit test matrix.
4. Obtain domain-expert review and sign-off for blood compatibility, eligibility, inventory, and escalation rules.
5. Complete the data inventory, minimization, retention, deletion, consent/communication, access, and current Sri Lankan legal assessment.

### Gate 2 — production operations and security assurance

1. Add automated SAST, dependency/SBOM, secret, and image scans; define severity-based patch deadlines and release blocking.
2. Define and test backups/restores, key rotation, incident response, monitoring, retention, uptime/latency targets, and failure recovery.
3. Complete staging deployment, TLS/domain/CORS review, migration/rollback rehearsal, provider configuration, and external security test.
4. Verify mobile release build and physical device flows; assess accessibility with WCAG 2.2 AA as the web target.
5. Set explicit launch criteria and prevent production mode from enabling demo seeding or development credentials.

### Gate 3 — evidence-based improvement

1. Expand agent red-team and golden evaluation to foreseeable misuse and subgroup fairness/coverage; have clinical/domain experts review outcomes.
2. Publish API contracts and lifecycle policy; complete dashboards and notification delivery monitoring.
3. Produce a versioned evidence pack: test reports, threat model, data map, model/system cards, operational runbooks, accessibility findings, and release sign-offs.

The current project completion plan is a useful source of tasks and should remain synchronized with these gates. Its “last verified” date (18 August 2026) and already elapsed submission deadline (30 September 2026) should be refreshed against the team’s actual status before relying on it as the live tracker.

## Research references

- [OWASP Application Security Verification Standard (ASVS), stable 5.0.0](https://github.com/OWASP/ASVS)
- [OWASP API Security Top 10 (2023)](https://api-security.owasp.org/editions/2023/en/0x00-header/)
- [OWASP Top 10 for LLM and Generative AI Applications (2025)](https://genai.owasp.org/llm-top-10/)
- [NIST AI Risk Management Framework](https://www.nist.gov/itl/ai-risk-management-framework)
- [W3C WCAG 2.2 Recommendation announcement](https://www.w3.org/WAI/news/2023-10-05/wcag22rec/)
- [Sri Lanka Data Protection Authority: Acts, gazettes, and guidance](https://www.dpa.gov.lk/guidelines.php)
- [Sri Lanka Personal Data Protection (Amendment) Act, No. 22 of 2025](https://documents.gov.lk/view/acts/2025/10/22-2025_E.pdf)

## Method and limitations

This assessment reviewed tracked source/configuration and project documentation on 5 October 2026 and cross-checked the standards list against the primary sources above. It did not run the application, execute tests, inspect the untracked/secret contents of `.env`, inspect cloud accounts, verify live infrastructure, interview the team or clinical stakeholders, or conduct penetration, load, usability, privacy, or legal testing. The existing local change to `docker-compose.yml` (PostgreSQL host port 5433) was present during the review and was not changed.
