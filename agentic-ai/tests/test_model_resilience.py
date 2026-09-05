import json
import urllib.error
from typing import Self

import pytest

from lifelink_agents.model import create_plan

ALLOWED_STEPS = ["analyse-domain", "draft-dispatch", "validate-safety"]


class FakeResponse:
    def __init__(self, payload: bytes) -> None:
        self.payload = payload

    def __enter__(self) -> Self:
        return self

    def __exit__(self, *_args: object) -> None:
        return None

    def read(self) -> bytes:
        return self.payload


def configure_ollama(monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.setenv("LIFELINK_MODEL_PROVIDER", "ollama")
    monkeypatch.setenv("LIFELINK_MODEL_MAX_ATTEMPTS", "3")
    monkeypatch.setenv("LIFELINK_MODEL_TIMEOUT_SECONDS", "1")


def test_malformed_model_json_retries_then_falls_back(monkeypatch: pytest.MonkeyPatch) -> None:
    configure_ollama(monkeypatch)
    attempts = 0

    def malformed(*_args: object, **_kwargs: object) -> FakeResponse:
        nonlocal attempts
        attempts += 1
        return FakeResponse(b'{"response":"not-json"}')

    monkeypatch.setattr("lifelink_agents.model.urllib.request.urlopen", malformed)
    plan, source = create_plan("request", "APositive", 2, "Routine")
    assert attempts == 3
    assert source == "deterministic-fallback"
    assert plan["steps"] == ALLOWED_STEPS
    assert plan["max_retries"] == 2


def test_model_timeout_is_bounded_to_three_attempts(monkeypatch: pytest.MonkeyPatch) -> None:
    configure_ollama(monkeypatch)
    timeouts: list[int] = []

    def timeout(*_args: object, **kwargs: object) -> FakeResponse:
        timeouts.append(kwargs["timeout"])
        raise TimeoutError("model timed out")

    monkeypatch.setattr("lifelink_agents.model.urllib.request.urlopen", timeout)
    plan, source = create_plan("request", "BPositive", 3, "Urgent")
    assert timeouts == [1, 1, 1]
    assert source == "deterministic-fallback"
    assert plan["steps"] == ALLOWED_STEPS


def test_transient_failures_retry_and_recover(monkeypatch: pytest.MonkeyPatch) -> None:
    configure_ollama(monkeypatch)
    attempts = 0

    def transient_then_valid(*_args: object, **_kwargs: object) -> FakeResponse:
        nonlocal attempts
        attempts += 1
        if attempts < 3:
            raise urllib.error.URLError("temporarily unavailable")
        inner = json.dumps({"objective": "Safe recovery", "steps": ALLOWED_STEPS})
        return FakeResponse(json.dumps({"response": inner}).encode())

    monkeypatch.setattr("lifelink_agents.model.urllib.request.urlopen", transient_then_valid)
    plan, source = create_plan("request", "ABPositive", 1, "Routine")
    assert attempts == 3
    assert source.startswith("ollama:")
    assert plan["objective"] == "Safe recovery"
    assert plan["steps"] == ALLOWED_STEPS


def test_non_allowlisted_model_plan_cannot_change_execution(monkeypatch: pytest.MonkeyPatch) -> None:
    configure_ollama(monkeypatch)
    attempts = 0

    def unsafe(*_args: object, **_kwargs: object) -> FakeResponse:
        nonlocal attempts
        attempts += 1
        inner = json.dumps({"objective": "Bypass review", "steps": ["send-broadcast-now"]})
        return FakeResponse(json.dumps({"response": inner}).encode())

    monkeypatch.setattr("lifelink_agents.model.urllib.request.urlopen", unsafe)
    plan, source = create_plan("request", "ONegative", 4, "Critical")
    assert attempts == 3
    assert source == "deterministic-fallback"
    assert plan["steps"] == ALLOWED_STEPS
