using LifeLink.Domain.Enums;

namespace LifeLink.Application.Workflows;

public sealed record AgentDonorCandidate(string UserId, string BloodType, double? DistanceKm);
public sealed record AgentStockCandidate(string LotId, string BloodType, int UnitsAvailable, string ExpiryDate);
public sealed record AgentRunRequest(string WorkflowId, string RequestId, string BloodType, int QuantityUnits, string Urgency, string? Notes, IReadOnlyList<AgentDonorCandidate> Donors, IReadOnlyList<AgentStockCandidate> Stock, string? CorrelationId = null);
public sealed record AgentRunStep(int Sequence, string AgentName, IReadOnlyDictionary<string, object?> Input, IReadOnlyDictionary<string, object?> Output, IReadOnlyList<IReadOnlyDictionary<string, object?>> ToolCalls, string Status, string? ErrorCode, string? ErrorMessage);
public sealed record AgentRunResponse(string SchemaVersion, string WorkflowId, IReadOnlyDictionary<string, object?> Plan, IReadOnlyList<AgentRunStep> Steps, string Outcome, bool RequiresApproval, IReadOnlyDictionary<string, object?> Validation, int ProposedReservationUnits, IReadOnlyList<string> ProposedRecipientUserIds);
public sealed record WorkflowStepView(Guid Id, int Sequence, string AgentName, string InputJson, string? OutputJson, string ToolCallsJson, AgentStepStatus Status, DateTimeOffset? StartedAtUtc, DateTimeOffset? CompletedAtUtc, string? ErrorCode, string? ErrorMessage);
public sealed record ApprovalView(Guid Id, int Version, Guid ApproverUserId, ApprovalDecision Decision, string? Comments, DateTimeOffset DecidedAtUtc);
public sealed record WorkflowView(Guid Id, Guid BloodRequestId, int AttemptNumber, string Objective, string PlanJson, WorkflowStatus Status, string CorrelationId, DateTimeOffset? StartedAtUtc, DateTimeOffset? CompletedAtUtc, string? FinalOutcomeJson, IReadOnlyList<WorkflowStepView> Steps, IReadOnlyList<ApprovalView> Approvals);
public sealed record WorkflowResult<T>(T? Value, string? ErrorCode, string? ErrorMessage)
{
    public bool Succeeded => ErrorCode is null;
    public static WorkflowResult<T> Success(T value) => new(value, null, null);
    public static WorkflowResult<T> Failure(string code, string message) => new(default, code, message);
}
public interface IAgentWorkflowClient { Task<AgentRunResponse> RunAsync(AgentRunRequest request, CancellationToken ct); }
public interface IWorkflowService
{
    Task<WorkflowResult<WorkflowView>> StartAsync(Guid requestId, string reason, CancellationToken ct);
    Task<WorkflowResult<WorkflowView>> GetAsync(Guid workflowId, CancellationToken ct);
    Task<IReadOnlyList<WorkflowView>> ListAsync(WorkflowStatus? status, int limit, CancellationToken ct);
    Task<WorkflowResult<WorkflowView>> ApproveAsync(Guid workflowId, Guid approverUserId, string? comments, CancellationToken ct);
    Task<WorkflowResult<WorkflowView>> RejectAsync(Guid workflowId, Guid approverUserId, string? comments, CancellationToken ct);
    Task<WorkflowResult<WorkflowView>> ReviseAsync(Guid workflowId, Guid approverUserId, string comments, CancellationToken ct);
}
