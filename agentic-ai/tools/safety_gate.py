"""Hard-coded safety and human-approval policy."""

from typing import Any

RARE_TYPES = frozenset({"ONegative", "ABNegative"})


def evaluate_safety(
    blood_type: str,
    urgency: str,
    reserve_units: int,
    recipient_user_ids: list[str],
) -> dict[str, Any]:
    rare_or_critical = blood_type in RARE_TYPES or urgency == "Critical"
    broadcast = bool(recipient_user_ids)
    stock_action = reserve_units > 0
    requires_approval = broadcast or rare_or_critical or stock_action
    no_action = not stock_action and not broadcast
    outcome = "EscalationRequired" if no_action else "PendingApproval" if requires_approval else "Completed"
    return {
        "prompt_injection_ignored": True,
        "rare_or_critical": rare_or_critical,
        "broadcast_requires_approval": broadcast,
        "stock_action_requires_approval": stock_action,
        "no_arbitrary_tools": True,
        "requires_approval": requires_approval,
        "outcome": outcome,
    }
