import json
from pathlib import Path

import pytest

from lifelink_agents.graph import run_workflow
from lifelink_agents.models import WorkflowRequest

GOLDEN_SCENARIOS = json.loads(
    Path(__file__).with_name("golden_scenarios.json").read_text(encoding="utf-8")
)


@pytest.mark.parametrize(
    "scenario",
    GOLDEN_SCENARIOS,
    ids=[scenario["name"] for scenario in GOLDEN_SCENARIOS],
)
def test_golden_workflow_scenario(scenario: dict, monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.setenv("LIFELINK_MODEL_PROVIDER", "deterministic")
    request = WorkflowRequest(
        workflow_id=f"wf-{scenario['name']}",
        request_id=f"req-{scenario['name']}",
        blood_type=scenario["blood_type"],
        quantity_units=scenario["quantity_units"],
        urgency=scenario["urgency"],
        notes=scenario["notes"],
        donors=scenario["donors"],
        stock=scenario["stock"],
    )
    result = run_workflow(request)
    expected = scenario["expected"]

    assert result.outcome == expected["outcome"]
    assert result.requires_approval is expected["requires_approval"]
    assert result.proposed_reservation_units == expected["reserve_units"]
    assert result.proposed_recipient_user_ids == expected["recipient_user_ids"]
    assert [step.agent_name for step in result.steps] == [
        "CoordinatorAgent",
        "DomainAnalysisAgent",
        "DispatchAgent",
        "ValidationSafetyAgent",
    ]
    assert all(step.status == "Completed" for step in result.steps)
    assert result.validation["no_arbitrary_tools"] is True
    if "prompt_injection_ignored" in expected:
        assert result.validation["prompt_injection_ignored"] is expected["prompt_injection_ignored"]
        assert result.steps[1].output["notes_classification"] == "untrusted-data"
    if "rare_or_critical" in expected:
        assert result.validation["rare_or_critical"] is expected["rare_or_critical"]
