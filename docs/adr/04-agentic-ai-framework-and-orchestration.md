# ADR-04: Agent framework, model, and fallback

- Status: Accepted
- Date: 18 August 2026

## Context

LifeLink needs auditable delegation among four agents without allowing a language model
to authorize dispatch, broadcasts, or arbitrary tool execution. Development and CI must
also work without paid services or a continuously available model.

## Decision

Use LangGraph for the fixed Coordinator -> Domain Analysis -> Dispatch -> Validation
graph. Use Ollama as the optional local planning model, constrained to versioned
structured output and three allow-listed planning steps. Use the deterministic planner
as the mandatory safe fallback and as the CI provider. Business rules, compatibility,
shortage calculation, tool authorization, and approval gates remain deterministic code.

Model calls have bounded timeouts and at most three attempts. Malformed, unavailable,
timed-out, or non-allow-listed responses fall back safely. No raw reasoning trace is
stored.

## Consequences

The workflow remains demonstrable offline and does not depend on an LLM for correctness.
The deterministic fallback is not a second generative model; if the authoritative brief
requires a hosted model fallback, a separately reviewed provider adapter is still needed.
