import json
from pathlib import Path

from fastapi.testclient import TestClient

from lifelink_agents.main import app

SCHEMAS = Path(__file__).parents[1] / "schemas"


def test_versioned_json_schemas_are_strict_and_loadable() -> None:
    for name in ("workflow-request-v1.schema.json", "workflow-response-v1.schema.json"):
        schema = json.loads((SCHEMAS / name).read_text(encoding="utf-8"))
        assert schema["$schema"].endswith("2020-12/schema")
        assert schema["additionalProperties"] is False
        assert schema["required"]


def test_request_boundary_rejects_unknown_properties() -> None:
    response = TestClient(app).post(
        "/v1/workflows/run",
        headers={"X-Internal-API-Key": "replace-with-a-local-development-key"},
        json={
            "workflow_id": "wf-contract",
            "request_id": "request-contract",
            "blood_type": "APositive",
            "quantity_units": 1,
            "urgency": "Routine",
            "donors": [],
            "stock": [],
            "execute_arbitrary_code": True,
        },
    )
    assert response.status_code == 422


def test_agent_echoes_api_correlation_id() -> None:
    correlation = "contract-correlation-123"
    response = TestClient(app).get("/health", headers={"X-Correlation-ID": correlation})
    assert response.status_code == 200
    assert response.headers["X-Correlation-ID"] == correlation


def _run(**overrides: object) -> int:
    body = {
        "workflow_id": "wf-bounds",
        "request_id": "request-bounds",
        "blood_type": "APositive",
        "quantity_units": 1,
        "urgency": "Routine",
        "donors": [],
        "stock": [],
    }
    body.update(overrides)
    response = TestClient(app).post(
        "/v1/workflows/run",
        headers={"X-Internal-API-Key": "replace-with-a-local-development-key"},
        json=body,
    )
    return response.status_code


def test_request_model_enforces_the_published_schema_bounds() -> None:
    lot = {"lot_id": "l1", "blood_type": "APositive", "units_available": 1, "expiry_date": "2026-11-01"}
    assert _run(stock=[lot]) == 200
    assert _run(blood_type="A+") == 422
    assert _run(urgency="High") == 422
    assert _run(stock=[{**lot, "units_available": -1}]) == 422
    assert _run(stock=[{**lot, "expiry_date": "11/01/2026"}]) == 422
    assert _run(donors=[{"user_id": "u1", "blood_type": "APositive", "distance_km": -2}]) == 422
