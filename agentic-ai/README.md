# Agentic AI Subsystem

Internal service only — called by ASP.NET Core (`AgentOrchestration`), never directly by
React or Flutter. Framework: LangGraph, model via Ollama (local) with a documented hosted
fallback (see `docs/adr/`).

## Layout
- `coordinator_agent/` — Student 2. Plans + delegates steps.
- `domain_analysis_agent/` — Student 1. Donor-matching against inventory/eligibility.
- `dispatch_agent/` — Student 3. Drafts stock reservation / donor-broadcast plan.
- `validation_agent/` — Student 4. Deterministic validation + approval gating.
- `lifelink_agents/graph.py` — shared orchestration wiring only (Coordinator → the other three).
- `tools/` — allow-listed tool implementations (one file per tool, least-privilege).
- `tests/` — golden-case scenarios (10-15) + the mandatory prompt-injection test.

## Hard rules (spec §9.1 / §10.5)
- Agents call only allow-listed tools with validated inputs/structured outputs.
- Every donor broadcast, or any dispatch against rare-type/critical-shortage stock,
  always sets `requires_approval = true` in code — never left to model judgment.
- No raw model reasoning traces, credentials, or tokens are ever persisted.
- Each step has a bounded timeout and max-retry count; exceeding either routes to
  the safe-failure path (`EscalationRequired`), never an infinite loop.

Each named agent folder is an installable Python package with its own `agent.py`. The
shared graph imports those agents; it does not duplicate their logic. `tools/` contains
pure proposal/validation functions for blood compatibility, distance ordering, FEFO
allocation, donor broadcast proposals, and the deterministic safety gate. These tools
have no database, filesystem, arbitrary HTTP, or code-execution access.

## Evaluation suite

`tests/golden_scenarios.json` is the reviewable 12-scenario corpus. It covers exact,
partial, and missing inventory; ABO/Rh compatibility; expired/empty lots; nearest-donor
ordering; rare and critical-stock approvals; and two approval-bypass prompt attempts.
`tests/test_model_resilience.py` separately verifies malformed responses, bounded
timeouts, retry-then-success behavior, and rejection of non-allow-listed model steps.

Run the complete quality gate from this directory:

```powershell
.\.venv\Scripts\python.exe -m ruff check .
.\.venv\Scripts\python.exe -m pytest -q
```

Tests use the deterministic planner unless a resilience test explicitly enables the
Ollama adapter, so CI never depends on a running model server.

The strict, versioned API contracts are published under `schemas/`. Unknown request
fields are rejected, and the API-provided `X-Correlation-ID` is echoed by the agent
service so a workflow can be traced across service logs without recording secrets.
