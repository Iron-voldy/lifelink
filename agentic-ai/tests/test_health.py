from fastapi.testclient import TestClient

from lifelink_agents.main import app


def test_health() -> None:
    response = TestClient(app).get("/health")
    assert response.status_code == 200
    assert response.json()["status"] == "healthy"


def test_workflow_requires_internal_authentication() -> None:
    response = TestClient(app).post("/v1/workflows/run", json={})
    assert response.status_code == 401
