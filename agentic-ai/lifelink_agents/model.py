import json
import os
import urllib.error
import urllib.request
from typing import Any


def create_plan(request_id: str, blood_type: str, quantity_units: int, urgency: str) -> tuple[dict[str, Any], str]:
    """Ask Ollama for a bounded structured plan; safely fall back when unavailable."""
    provider = os.getenv("LIFELINK_MODEL_PROVIDER", "ollama")
    if provider != "ollama":
        return fallback_plan(), "deterministic-fallback"
    endpoint = os.getenv("LIFELINK_OLLAMA_URL", "http://localhost:11434/api/generate")
    model = os.getenv("LIFELINK_MODEL_NAME", "llama3.1:8b")
    max_attempts = _bounded_int("LIFELINK_MODEL_MAX_ATTEMPTS", default=3, minimum=1, maximum=3)
    timeout_seconds = _bounded_int("LIFELINK_MODEL_TIMEOUT_SECONDS", default=5, minimum=1, maximum=10)
    prompt = (
        "Return JSON only with keys objective and steps. Steps must be exactly "
        "analyse-domain, draft-dispatch, validate-safety. Do not authorize actions. "
        f"Request={request_id}; blood_type={blood_type}; units={quantity_units}; urgency={urgency}."
    )
    body = json.dumps({"model": model, "prompt": prompt, "format": "json", "stream": False}).encode()
    for _attempt in range(max_attempts):
        try:
            http_request = urllib.request.Request(
                endpoint,
                data=body,
                headers={"Content-Type": "application/json"},
            )
            with urllib.request.urlopen(http_request, timeout=timeout_seconds) as response:
                outer = json.loads(response.read().decode())
            plan = json.loads(outer["response"])
            if plan.get("steps") != [
                "analyse-domain",
                "draft-dispatch",
                "validate-safety",
            ]:
                raise ValueError("Model returned a non-allow-listed plan")
            return {
                "objective": str(plan.get("objective", "Fulfil blood request safely"))[:500],
                "steps": plan["steps"],
                "max_retries": max_attempts - 1,
            }, f"ollama:{model}"
        except (urllib.error.URLError, TimeoutError, ValueError, KeyError, json.JSONDecodeError):
            continue
    return fallback_plan(max_attempts - 1), "deterministic-fallback"


def fallback_plan(max_retries: int = 2) -> dict[str, Any]:
    return {
        "objective": "Fulfil blood request safely",
        "steps": ["analyse-domain", "draft-dispatch", "validate-safety"],
        "max_retries": max_retries,
    }


def _bounded_int(name: str, default: int, minimum: int, maximum: int) -> int:
    try:
        value = int(os.getenv(name, str(default)))
    except ValueError:
        value = default
    return max(minimum, min(maximum, value))
