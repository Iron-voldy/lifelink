"""Deterministic validation/safety agent and approval gate."""

from lifelink_agents.models import GraphState
from tools import evaluate_safety


def run_validation(state: GraphState) -> GraphState:
    request = state["request"]
    reserve = state.get("proposed_reservation_units", 0)
    recipients = state.get("proposed_recipient_user_ids", [])
    checks = evaluate_safety(request.blood_type, request.urgency, reserve, recipients)
    outcome = checks["outcome"]
    step = {
        "sequence": 4,
        "agent_name": "ValidationSafetyAgent",
        "input": {"proposal": state["steps"][-1]["output"]},
        "output": checks,
        "tool_calls": [
            {
                "name": "deterministic_safety_gate",
                "arguments": {},
                "result": outcome,
            }
        ],
        "status": "Completed",
    }
    return {
        "validation": {key: value for key, value in checks.items() if key != "outcome"},
        "outcome": outcome,
        "steps": state["steps"] + [step],
    }
