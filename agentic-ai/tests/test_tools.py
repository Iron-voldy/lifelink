from lifelink_agents.graph import run_workflow
from lifelink_agents.models import DonorCandidate, StockCandidate, WorkflowRequest
from tools import analyse_candidates, evaluate_safety, propose_inventory_reservation


def test_candidate_analysis_applies_abo_rh_compatibility_and_distance_order() -> None:
    result = analyse_candidates(
        "APositive",
        [
            DonorCandidate(user_id="far-o-negative", blood_type="ONegative", distance_km=12),
            DonorCandidate(user_id="near-a-positive", blood_type="APositive", distance_km=2),
            DonorCandidate(user_id="incompatible-b", blood_type="BPositive", distance_km=1),
        ],
        [
            StockCandidate(lot_id="later-a", blood_type="APositive", units_available=2, expiry_date="2026-10-01"),
            StockCandidate(lot_id="early-o", blood_type="ONegative", units_available=1, expiry_date="2026-09-01"),
            StockCandidate(lot_id="incompatible-b", blood_type="BPositive", units_available=50, expiry_date="2026-08-20"),
        ],
    )
    assert result["matched_donor_ids"] == ["near-a-positive", "far-o-negative"]
    assert [lot["lot_id"] for lot in result["compatible_lots"]] == ["early-o", "later-a"]
    assert result["compatible_stock_units"] == 3


def test_reservation_proposal_uses_fefo_and_never_exceeds_required_units() -> None:
    proposal = propose_inventory_reservation(
        3,
        [
            {"lot_id": "early", "units_available": 2},
            {"lot_id": "later", "units_available": 10},
        ],
    )
    assert proposal["allocations"] == [
        {"lot_id": "early", "units": 2},
        {"lot_id": "later", "units": 1},
    ]
    assert proposal["reserve_units"] == 3
    assert proposal["shortage_units"] == 0
    assert proposal["proposal_only"] is True


def test_safety_gate_cannot_auto_approve_stock_or_broadcast_actions() -> None:
    assert evaluate_safety("APositive", "Routine", 1, [])["outcome"] == "PendingApproval"
    assert evaluate_safety("APositive", "Routine", 0, ["donor"])["outcome"] == "PendingApproval"
    assert evaluate_safety("APositive", "Routine", 0, [])["outcome"] == "EscalationRequired"


def test_workflow_does_not_count_incompatible_inventory() -> None:
    result = run_workflow(
        WorkflowRequest(
            workflow_id="wf-incompatible",
            request_id="req-incompatible",
            blood_type="ONegative",
            quantity_units=2,
            urgency="Routine",
            donors=[],
            stock=[
                StockCandidate(
                    lot_id="o-positive-only",
                    blood_type="OPositive",
                    units_available=20,
                    expiry_date="2026-09-01",
                )
            ],
        )
    )
    assert result.proposed_reservation_units == 0
    assert result.outcome == "EscalationRequired"
