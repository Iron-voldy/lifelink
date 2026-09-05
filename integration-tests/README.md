# Cross-platform integration test

`cross_platform_workflow.py` drives the exact public HTTP contracts used by the Flutter
hospital client and React administrator client against the composed ASP.NET API,
PostgreSQL database, and four-agent LangGraph service.

The test proves both required outcomes:

1. Flutter-channel hospital registration and request submission -> persisted agent
   workflow -> React-channel human approval -> inventory dispatch -> Flutter-visible
   `Dispatched` status.
2. An approval-bypass note with no compatible stock -> deterministic
   `EscalationRequired` workflow -> Flutter-visible `Escalated` status, with no approval
   or side effect.

## Run

Create `.env.e2e` from `.env.e2e.example`, replace every placeholder secret, then run:

```powershell
.\integration-tests\run.ps1
```

The runner uses the isolated `lifelink-e2e` Compose project and stops its containers on
completion. Pass `-KeepRunning` to retain them for inspecting API, agent, and PostgreSQL
logs. It deliberately retains the test database volume across runs; generated accounts
and resource names are unique.

The runner is Python-standard-library only. Successful runs write a redacted JSON file
under `artifacts/e2e/` containing request, workflow, agent-step, approval, history, and
HTTP/workflow correlation IDs. It never records access tokens or passwords. Use this
file with API/container logs when collecting demonstration evidence.

The `Cross-platform integration` GitHub Actions workflow runs the same test for changes
to any application tier. It uploads the redacted JSON audit evidence and container logs
as a 30-day CI artifact even when the scenario fails.
