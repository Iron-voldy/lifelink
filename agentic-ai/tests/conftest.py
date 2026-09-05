import os

# Must be set before the app module is imported by any test (settings validate at import time).
os.environ.setdefault("LIFELINK_ENVIRONMENT", "development")

import pytest


@pytest.fixture(autouse=True)
def use_deterministic_model_by_default(monkeypatch: pytest.MonkeyPatch) -> None:
    """Keep ordinary tests offline; resilience tests explicitly opt into Ollama."""
    monkeypatch.setenv("LIFELINK_MODEL_PROVIDER", "deterministic")

