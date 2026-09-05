# ADR-06: Cloud deployment platform

- Status: Accepted
- Date: 18 August 2026

## Context

The evaluator needs a deployed React application, API, PostgreSQL database, and agent
service without a paid subscription. The original Azure placeholder conflicts with the
free-platform recommendation in the project design.

## Decision

Use Vercel for the React static application, Render container services for ASP.NET Core
and the Python agent, and Neon for managed PostgreSQL. Retain Docker Compose as the local
and integration-test topology. All environments use provider secret stores, TLS, explicit
CORS origins, migration-on-release, health probes, and synthetic evaluator accounts.

If free-tier availability or sleep behavior cannot satisfy the three-week access window,
Azure for Students is the documented fallback; switching requires this ADR to be
superseded and the infrastructure folder updated.

## Consequences

The selected topology separates public web/API endpoints from the internal agent service
and keeps PostgreSQL managed. Provider accounts, live URLs, backup policy, monitoring,
rollback tests, and uptime verification still require authorized external setup.
