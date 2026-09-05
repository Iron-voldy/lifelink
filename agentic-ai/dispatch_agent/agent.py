"""Action/tool-use agent that can produce proposals but cannot execute them."""

from lifelink_agents.models import GraphState
from tools import propose_donor_broadcast, propose_inventory_reservation


def run_dispatch(state: GraphState) -> GraphState:
    request = state["request"]
    reservation = propose_inventory_reservation(
        request.quantity_units,
        state.get("compatible_lots", []),
    )
    recipients = state.get("matched_donors", []) if reservation["shortage_units"] else []
    broadcast = propose_donor_broadcast(recipients, reservation["shortage_units"])
    output = {
        "reserve_units": reservation["reserve_units"],
        "allocations": reservation["allocations"],
        "recipient_user_ids": broadcast["recipient_user_ids"],
        "shortage_units": reservation["shortage_units"],
    }
    calls = [
        {
            "name": "propose_inventory_reservation",
            "arguments": {"required_units": request.quantity_units},
            "result": reservation,
        }
    ]
    if recipients:
        calls.append(
            {
                "name": "propose_donor_broadcast",
                "arguments": {"recipient_count": len(recipients)},
                "result": broadcast,
            }
        )
    step = {
        "sequence": 3,
        "agent_name": "DispatchAgent",
        "input": {"required_units": request.quantity_units},
        "output": output,
        "tool_calls": calls,
        "status": "Completed",
    }
    return {
        "proposed_reservation_units": reservation["reserve_units"],
        "proposed_recipient_user_ids": broadcast["recipient_user_ids"],
        "steps": state["steps"] + [step],
    }
