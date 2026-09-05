import hmac
import logging
import uuid

from fastapi import Depends, FastAPI, Header, HTTPException, Request, status
from fastapi.exceptions import RequestValidationError
from fastapi.responses import JSONResponse
from pydantic import BaseModel
from pydantic_settings import BaseSettings, SettingsConfigDict

from .graph import run_workflow
from .models import WorkflowRequest, WorkflowResponse


class Settings(BaseSettings):
    model_config = SettingsConfigDict(env_prefix="LIFELINK_", env_file=".env", extra="ignore")
    internal_api_key: str = "replace-with-a-local-development-key"
    # Anything other than "development" refuses to start with a missing or placeholder key (fail closed).
    environment: str = "production"


PLACEHOLDER_PREFIX = "replace-with"
logger = logging.getLogger("lifelink.agents")


def validate_settings(current: Settings) -> None:
    key = current.internal_api_key.strip()
    if current.environment.lower() != "development" and (len(key) < 16 or key.startswith(PLACEHOLDER_PREFIX)):
        raise RuntimeError(
            "LIFELINK_INTERNAL_API_KEY must be a random value of at least 16 characters. "
            "Set LIFELINK_ENVIRONMENT=development only for local development."
        )


settings = Settings()
validate_settings(settings)
app = FastAPI(title="LifeLink Agent Service", version="0.1.0")


def problem(status_code: int, code: str, detail: str, request: Request, **extra: object) -> JSONResponse:
    body = {"status": status_code, "title": code, "detail": detail, "correlationId": getattr(request.state, "correlation_id", None), **extra}
    return JSONResponse(body, status_code=status_code, media_type="application/problem+json")


@app.exception_handler(RequestValidationError)
async def handle_validation_error(request: Request, exc: RequestValidationError) -> JSONResponse:
    errors = {
        ".".join(str(part) for part in err["loc"] if part != "body") or "body": err["msg"]
        for err in exc.errors()
    }
    summary = "; ".join(f"{field}: {message}" for field, message in errors.items())
    return problem(422, "validation_failed", f"The workflow request is invalid. {summary}", request, errors=errors)


@app.exception_handler(Exception)
async def handle_unexpected_error(request: Request, exc: Exception) -> JSONResponse:
    logger.exception("Unhandled error (correlation %s)", getattr(request.state, "correlation_id", None))
    return problem(500, "internal_error", "The agent service failed unexpectedly. The caller should escalate for manual review.", request)


@app.middleware("http")
async def correlation_id(request, call_next):
    value = request.headers.get("X-Correlation-ID", "").strip()
    correlation = value[:100] if value else uuid.uuid4().hex
    request.state.correlation_id = correlation
    response = await call_next(request)
    response.headers["X-Correlation-ID"] = correlation
    return response


class HealthResponse(BaseModel):
    status: str
    service: str


def require_internal_key(x_internal_api_key: str = Header(default="")) -> None:
    # Compare as bytes: str comparison raises TypeError (a 500) on non-ASCII input.
    if not hmac.compare_digest(x_internal_api_key.encode("utf-8"), settings.internal_api_key.encode("utf-8")):
        raise HTTPException(status_code=status.HTTP_401_UNAUTHORIZED, detail="Invalid internal API key")


@app.get("/health", response_model=HealthResponse)
def health() -> HealthResponse:
    return HealthResponse(status="healthy", service="lifelink-agentic-ai")


@app.post("/v1/workflows/run", response_model=WorkflowResponse, dependencies=[Depends(require_internal_key)])
def execute_workflow(request: WorkflowRequest) -> WorkflowResponse:
    return run_workflow(request)
