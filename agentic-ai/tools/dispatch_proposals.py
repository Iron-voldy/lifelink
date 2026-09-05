"""Proposal-only dispatch tools with no database or network authority."""

from typing import Any


def propose_inventory_reservation(required_units: int, compatible_lots: list[dict[str, Any]]) -> dict[str, Any]:
    remaining = max(0, required_units)
    allocations: list[dict[str, Any]] = []
    for lot in compatible_lots:
        units = min(remaining, max(0, int(lot["units_available"])))
        if units:
            allocations.append({"lot_id": lot["lot_id"], "units": units})
            remaining -= units
        if remaining == 0:
            break
    return {
        "allocations": allocations,
        "reserve_units": required_units - remaining,
        "shortage_units": remaining,
        "proposal_only": True,
    }


def propose_donor_broadcast(recipient_user_ids: list[str], shortage_units: int) -> dict[str, Any]:
    return {
        "recipient_user_ids": recipient_user_ids[:100],
        "shortage_units": max(0, shortage_units),
        "requires_approval": bool(recipient_user_ids),
        "proposal_only": True,
    }
