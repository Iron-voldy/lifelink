# ADR-05: Agent-state persistence and service boundary

- Status: Accepted
- Date: 18 August 2026

## Context

Agent workflows need multiple attempts, human decisions, tool evidence, safe failures,
and correlation IDs. React and Flutter must never call agents or the database directly.

## Decision

ASP.NET Core owns PostgreSQL persistence and all side effects. The Python service is an
internal proposal engine authenticated with a dedicated API key. Persist normalized
workflow execution, step, and approval records; store versioned plan, structured
input/output, validation, and allow-listed tool payloads as JSONB. A request can have
multiple uniquely numbered attempts and approvals are versioned per attempt.

Do not persist prompts containing secrets, credentials, tokens, or raw chain-of-thought.
Only an authenticated administrator can approve, reject, or revise. Reservations and
notifications execute through backend services after approval.

## Consequences

Relational identifiers and lifecycle constraints remain queryable while evolving agent
payloads do not require a migration for every additive field. JSONB fields are deliberate
exceptions to a strict 3NF claim and require schema validation at the service boundary.
Durable LangGraph checkpoints/resume are not yet implemented and remain release work.
