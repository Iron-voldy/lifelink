import pytest
from fastapi.testclient import TestClient

from lifelink_agents.main import Settings, app, settings, validate_settings


def test_placeholder_key_is_rejected_outside_development():
    with pytest.raises(RuntimeError, match="INTERNAL_API_KEY"):
        validate_settings(Settings(environment="production", internal_api_key="replace-with-a-local-development-key"))


def test_short_key_is_rejected_outside_development():
    with pytest.raises(RuntimeError):
        validate_settings(Settings(environment="production", internal_api_key="short"))


def test_strong_key_is_accepted_in_production():
    validate_settings(Settings(environment="production", internal_api_key="a-long-random-service-key-1234"))


def test_placeholder_key_is_allowed_only_in_development():
    validate_settings(Settings(environment="development", internal_api_key="replace-with-a-local-development-key"))


def test_non_ascii_key_returns_401_not_500():
    response = TestClient(app).post("/v1/workflows/run", headers={"X-Internal-API-Key": "clé-invalide-é".encode()}, json={})
    assert response.status_code == 401


def test_invalid_payload_returns_readable_problem_with_field_errors():
    response = TestClient(app).post(
        "/v1/workflows/run",
        headers={"X-Internal-API-Key": settings.internal_api_key, "X-Correlation-ID": "corr-123"},
        json={"workflow_id": "w", "request_id": "r", "blood_type": "OPositive", "quantity_units": 0, "urgency": "High", "donors": [], "stock": []},
    )
    body = response.json()
    assert response.status_code == 422
    assert body["title"] == "validation_failed"
    assert "quantity_units" in body["errors"]
    assert body["correlationId"] == "corr-123"
