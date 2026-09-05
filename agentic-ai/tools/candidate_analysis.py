"""Least-privilege analysis over candidates already disclosed by the API."""

from typing import Any

from lifelink_agents.models import DonorCandidate, StockCandidate

from .compatibility import compatible_donor_types


def analyse_candidates(
    recipient_type: str,
    donors: list[DonorCandidate],
    stock: list[StockCandidate],
) -> dict[str, Any]:
    compatible = compatible_donor_types(recipient_type)
    matched_donors = sorted(
        (donor for donor in donors if donor.blood_type in compatible),
        key=lambda donor: (donor.distance_km is None, donor.distance_km or 0, donor.user_id),
    )[:100]
    compatible_lots = sorted(
        (lot for lot in stock if lot.blood_type in compatible and lot.units_available > 0),
        key=lambda lot: (lot.expiry_date, lot.lot_id),
    )
    return {
        "matched_donor_ids": [donor.user_id for donor in matched_donors],
        "compatible_lots": [
            {
                "lot_id": lot.lot_id,
                "blood_type": lot.blood_type,
                "units_available": lot.units_available,
                "expiry_date": lot.expiry_date,
            }
            for lot in compatible_lots
        ],
        "compatible_stock_units": sum(lot.units_available for lot in compatible_lots),
    }
