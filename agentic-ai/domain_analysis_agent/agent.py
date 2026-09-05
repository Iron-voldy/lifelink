"""Donor/inventory domain analysis agent."""

from lifelink_agents.models import GraphState
from tools import analyse_candidates


def run_domain_analysis(state: GraphState) -> GraphState:
    request = state["request"]
    analysis = analyse_candidates(request.blood_type, request.donors, request.stock)
    output = {
        "matched_donor_count": len(analysis["matched_donor_ids"]),
        "compatible_stock_units": analysis["compatible_stock_units"],
        "compatible_lots": analysis["compatible_lots"],
        "notes_classification": "untrusted-data",
    }
    tool = {
        "name": "analyse_candidates",
        "arguments": {"recipient_blood_type": request.blood_type},
        "result": output,
    }
    step = {
        "sequence": 2,
        "agent_name": "DomainAnalysisAgent",
        "input": {"blood_type": request.blood_type, "notes": request.notes},
        "output": output,
        "tool_calls": [tool],
        "status": "Completed",
    }
    return {
        "matched_donors": analysis["matched_donor_ids"],
        "compatible_lots": analysis["compatible_lots"],
        "available_units": analysis["compatible_stock_units"],
        "steps": state["steps"] + [step],
    }
