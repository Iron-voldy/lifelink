using LifeLink.Domain.Common;
using LifeLink.Domain.Enums;

namespace LifeLink.Domain.Entities;

public sealed class AgentWorkflowExecution : Entity
{
    public Guid BloodRequestId { get; private set; }
    public int AttemptNumber { get; private set; }
    public string Objective { get; private set; } = string.Empty;
    public string PlanJson { get; private set; } = "{}";
    public WorkflowStatus Status { get; private set; } = WorkflowStatus.Pending;
    public string CorrelationId { get; private set; } = string.Empty;
    public DateTimeOffset? StartedAtUtc { get; private set; }
    public DateTimeOffset? CompletedAtUtc { get; private set; }
    public string? FinalOutcomeJson { get; private set; }
    private AgentWorkflowExecution() { }
    public AgentWorkflowExecution(Guid requestId, int attemptNumber, string objective, string correlationId)
    { BloodRequestId = requestId; AttemptNumber = attemptNumber; Objective = objective; CorrelationId = correlationId; }
    public void SetStatus(WorkflowStatus status) { Status = status; UpdatedAtUtc = DateTimeOffset.UtcNow; }
    public void Start(DateTimeOffset now) { Status = WorkflowStatus.Running; StartedAtUtc = now; UpdatedAtUtc = now; }
    public void SetPlan(string planJson) { PlanJson = planJson; UpdatedAtUtc = DateTimeOffset.UtcNow; }
    public void Finish(WorkflowStatus status, string? finalOutcomeJson, DateTimeOffset now) { Status = status; FinalOutcomeJson = finalOutcomeJson; CompletedAtUtc = status is WorkflowStatus.Completed or WorkflowStatus.Rejected or WorkflowStatus.EscalationRequired or WorkflowStatus.Failed ? now : null; UpdatedAtUtc = now; }
}

public sealed class AgentStep : Entity
{
    public Guid WorkflowExecutionId { get; private set; }
    public int Sequence { get; private set; }
    public string AgentName { get; private set; } = string.Empty;
    public string InputJson { get; private set; } = "{}";
    public string? OutputJson { get; private set; }
    public string ToolCallsJson { get; private set; } = "[]";
    public AgentStepStatus Status { get; private set; } = AgentStepStatus.Pending;
    public DateTimeOffset? StartedAtUtc { get; private set; }
    public DateTimeOffset? CompletedAtUtc { get; private set; }
    public string? ErrorCode { get; private set; }
    public string? ErrorMessage { get; private set; }
    private AgentStep() { }
    public AgentStep(Guid workflowExecutionId, int sequence, string agentName, string inputJson, string? outputJson, string toolCallsJson, AgentStepStatus status, DateTimeOffset? startedAtUtc, DateTimeOffset? completedAtUtc, string? errorCode, string? errorMessage)
    { WorkflowExecutionId = workflowExecutionId; Sequence = sequence; AgentName = agentName; InputJson = inputJson; OutputJson = outputJson; ToolCallsJson = toolCallsJson; Status = status; StartedAtUtc = startedAtUtc; CompletedAtUtc = completedAtUtc; ErrorCode = errorCode; ErrorMessage = errorMessage; }
}

public sealed class AgentApproval : Entity
{
    public Guid WorkflowExecutionId { get; private set; }
    public int Version { get; private set; }
    public Guid ApproverUserId { get; private set; }
    public ApprovalDecision Decision { get; private set; }
    public string? Comments { get; private set; }
    public DateTimeOffset DecidedAtUtc { get; private set; }
    private AgentApproval() { }
    public AgentApproval(Guid workflowExecutionId, int version, Guid approverUserId, ApprovalDecision decision, string? comments, DateTimeOffset decidedAtUtc)
    { WorkflowExecutionId = workflowExecutionId; Version = version; ApproverUserId = approverUserId; Decision = decision; Comments = comments; DecidedAtUtc = decidedAtUtc; }
}
