from typing import Any, Literal, TypedDict

from pydantic import BaseModel, ConfigDict, Field

BloodTypeName = Literal[
    "APositive", "ANegative", "BPositive", "BNegative", "ABPositive", "ABNegative", "OPositive", "ONegative"
]
# ISO calendar date; FEFO ordering compares these strings, so other formats would sort wrongly.
ISO_DATE = r"^\d{4}-\d{2}-\d{2}$"


class DonorCandidate(BaseModel):
    model_config = ConfigDict(extra="forbid")
    user_id: str = Field(min_length=1)
    blood_type: str
    distance_km: float | None = Field(default=None, ge=0)


class StockCandidate(BaseModel):
    model_config = ConfigDict(extra="forbid")
    lot_id: str = Field(min_length=1)
    blood_type: str
    units_available: int = Field(ge=0)
    expiry_date: str = Field(pattern=ISO_DATE)


class WorkflowRequest(BaseModel):
    model_config = ConfigDict(extra="forbid")

    workflow_id: str = Field(min_length=1)
    request_id: str = Field(min_length=1)
    correlation_id: str | None = Field(default=None, max_length=100)
    blood_type: BloodTypeName
    quantity_units: int = Field(gt=0, le=100)
    urgency: Literal["Routine", "Urgent", "Critical"]
    notes: str | None = Field(default=None, max_length=2000)
    donors: list[DonorCandidate] = Field(max_length=500)
    stock: list[StockCandidate] = Field(max_length=500)


class AgentStepResult(BaseModel):
    model_config = ConfigDict(extra="forbid")
    sequence: int
    agent_name: str
    input: dict[str, Any]
    output: dict[str, Any]
    tool_calls: list[dict[str, Any]] = Field(default_factory=list)
    status: Literal["Completed", "Failed"] = "Completed"
    error_code: str | None = None
    error_message: str | None = None


class WorkflowResponse(BaseModel):
    model_config = ConfigDict(extra="forbid")
    schema_version: str = "1.0"
    workflow_id: str
    plan: dict[str, Any]
    steps: list[AgentStepResult]
    outcome: Literal["PendingApproval", "EscalationRequired", "Completed"]
    requires_approval: bool
    validation: dict[str, Any]
    proposed_reservation_units: int
    proposed_recipient_user_ids: list[str]


class GraphState(TypedDict, total=False):
    request: WorkflowRequest
    plan: dict[str, Any]
    steps: list[dict[str, Any]]
    matched_donors: list[str]
    available_units: int
    compatible_lots: list[dict[str, Any]]
    proposed_reservation_units: int
    proposed_recipient_user_ids: list[str]
    validation: dict[str, Any]
    outcome: str
