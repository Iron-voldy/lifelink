"""Coordinator/planning agent owned by the hospital-request component."""

from lifelink_agents.model import create_plan
from lifelink_agents.models import GraphState


def run_coordinator(state: GraphState) -> GraphState:
    request = state["request"]
    plan, model_used = create_plan(
        request.request_id,
        request.blood_type,
        request.quantity_units,
        request.urgency,
    )
    plan["model_used"] = model_used
    step = {
        "sequence": 1,
        "agent_name": "CoordinatorAgent",
        "input": {"request_id": request.request_id},
        "output": plan,
        "tool_calls": [],
        "status": "Completed",
    }
    return {"plan": plan, "steps": [step]}
