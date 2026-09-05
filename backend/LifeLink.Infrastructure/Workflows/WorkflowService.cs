using System.Text.Json;
using LifeLink.Application.Inventory;
using LifeLink.Application.Notifications;
using LifeLink.Application.Requests;
using LifeLink.Application.Workflows;
using LifeLink.Domain.Entities;
using LifeLink.Domain.Enums;
using LifeLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LifeLink.Infrastructure.Workflows;

public sealed class WorkflowService(LifeLinkDbContext db, IAgentWorkflowClient agentClient, IInventoryService inventory, INotificationService notifications, TimeProvider timeProvider, ILogger<WorkflowService> logger) : IWorkflowService, IRequestWorkflowTrigger
{
    private const int MaxProposedRecipients = 500;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    // Revising is the superseded attempt; it must not block the replacement attempt.
    private static readonly WorkflowStatus[] ActiveStatuses = [WorkflowStatus.Running, WorkflowStatus.PendingApproval, WorkflowStatus.Approved];

    public async Task TriggerAsync(Guid requestId, string reason, CancellationToken ct)
    {
        var result = await StartAsync(requestId, reason, ct);
        if (!result.Succeeded) logger.LogWarning("Workflow start for request {RequestId} failed: {Code} {Message}", requestId, result.ErrorCode, result.ErrorMessage);
    }

    public async Task<WorkflowResult<WorkflowView>> StartAsync(Guid requestId, string reason, CancellationToken ct)
    {
        var request = await db.BloodRequests.SingleOrDefaultAsync(x => x.Id == requestId, ct);
        if (request is null) return Fail("request_not_found", "Blood request was not found.");
        if (request.Status is BloodRequestStatus.Fulfilled or BloodRequestStatus.ClosedUnfulfilled) return Fail("request_closed", "Closed requests cannot start workflows.");
        // Approved/Dispatched requests already have stock moving; a new attempt would reset them to UnderReview and could dispatch twice.
        if (request.Status is BloodRequestStatus.Approved or BloodRequestStatus.Dispatched) return Fail("request_in_progress", $"The request is already {request.Status}; a new workflow cannot be started.");
        if (await db.InventoryReservations.Where(x => x.BloodRequestId == requestId && x.Status == ReservationStatus.Dispatched).SumAsync(x => x.Units, ct) >= request.QuantityUnits)
            return Fail("request_in_progress", "Every requested unit has already been dispatched; there is nothing left for a workflow to do.");
        if (await db.AgentWorkflowExecutions.AnyAsync(x => x.BloodRequestId == requestId && ActiveStatuses.Contains(x.Status), ct))
            return Fail("workflow_active", "This request already has an active workflow attempt.");

        var attempt = await db.AgentWorkflowExecutions.CountAsync(x => x.BloodRequestId == requestId, ct) + 1;
        var execution = new AgentWorkflowExecution(requestId, attempt, $"Fulfil request {requestId}: {reason}", Guid.NewGuid().ToString("N"));
        execution.Start(timeProvider.GetUtcNow());
        db.AgentWorkflowExecutions.Add(execution);
        SetRequestStatus(request, BloodRequestStatus.UnderReview, request.RequestedByUserId, $"Agent workflow attempt {attempt} started.");
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex)
        {
            // The (request, attempt) unique index rejects a concurrent start that raced past the check above.
            logger.LogWarning(ex, "Concurrent workflow start rejected for request {RequestId}", requestId);
            return Fail("workflow_conflict", "Another workflow attempt was started at the same time. Refresh and try again.");
        }

        try
        {
            var response = await agentClient.RunAsync(await BuildAgentRequestAsync(request, execution, ct), ct);
            var now = timeProvider.GetUtcNow();
            execution.SetPlan(JsonSerializer.Serialize(response.Plan, Json));
            foreach (var step in response.Steps) db.AgentSteps.Add(ToStep(execution.Id, step, now));
            var finalJson = JsonSerializer.Serialize(response, Json);
            switch (response.Outcome)
            {
                case "PendingApproval":
                    execution.Finish(WorkflowStatus.PendingApproval, finalJson, now);
                    SetRequestStatus(request, BloodRequestStatus.PendingApproval, request.RequestedByUserId, "Agent proposal passed deterministic validation and awaits human approval.");
                    break;
                case "EscalationRequired":
                    execution.Finish(WorkflowStatus.EscalationRequired, finalJson, now);
                    SetRequestStatus(request, BloodRequestStatus.Escalated, request.RequestedByUserId, "Agent workflow found no safe actionable match.");
                    break;
                default:
                    execution.Finish(WorkflowStatus.Completed, finalJson, now);
                    break;
            }
            await db.SaveChangesAsync(ct);
            return WorkflowResult<WorkflowView>.Success(await Map(execution, ct));
        }
        catch (Exception ex)
        {
            // Any failure (network, timeout, malformed output, database) must leave the attempt in a terminal state,
            // otherwise a Running attempt would block every future workflow for this request.
            var callerCancelled = ct.IsCancellationRequested;
            logger.LogError(ex, "Agent workflow {WorkflowId} failed safely (correlation {CorrelationId})", execution.Id, execution.CorrelationId);
            (execution, request) = await ReloadAsync(execution.Id, request.Id);
            execution.Finish(WorkflowStatus.EscalationRequired, JsonSerializer.Serialize(new { error = "agent_service_failure", message = "The agent service failed or timed out. Manual review is required.", correlationId = execution.CorrelationId }, Json), timeProvider.GetUtcNow());
            SetRequestStatus(request, BloodRequestStatus.Escalated, request.RequestedByUserId, "Agent service failed or timed out; manual review required.");
            await db.SaveChangesAsync(CancellationToken.None);
            if (callerCancelled) throw;
            return WorkflowResult<WorkflowView>.Success(await Map(execution, CancellationToken.None));
        }
    }

    public async Task<WorkflowResult<WorkflowView>> GetAsync(Guid workflowId, CancellationToken ct)
    {
        var execution = await db.AgentWorkflowExecutions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == workflowId, ct);
        return execution is null ? Fail("workflow_not_found", "Workflow was not found.") : WorkflowResult<WorkflowView>.Success(await Map(execution, ct));
    }

    public async Task<IReadOnlyList<WorkflowView>> ListAsync(WorkflowStatus? status, int limit, CancellationToken ct)
    {
        limit = Math.Clamp(limit, 1, 200);
        var query = db.AgentWorkflowExecutions.AsNoTracking();
        if (status is not null) query = query.Where(x => x.Status == status);
        var executions = await query.OrderByDescending(x => x.CreatedAtUtc).Take(limit).ToListAsync(ct);
        return await MapMany(executions, ct);
    }

    public async Task<WorkflowResult<WorkflowView>> ApproveAsync(Guid workflowId, Guid approverUserId, string? comments, CancellationToken ct)
    {
        var execution = await db.AgentWorkflowExecutions.SingleOrDefaultAsync(x => x.Id == workflowId, ct);
        if (execution is null) return Fail("workflow_not_found", "Workflow was not found.");
        if (execution.Status != WorkflowStatus.PendingApproval) return Fail("workflow_not_pending", "Workflow is not awaiting approval.");
        var response = DeserializeOutcome(execution);
        if (response is null) return Fail("invalid_workflow_output", "Workflow output cannot be applied.");
        var request = await db.BloodRequests.SingleOrDefaultAsync(x => x.Id == execution.BloodRequestId, ct);
        if (request is null) return Fail("request_not_found", "Blood request for this workflow was not found.");
        // The request may have been closed, escalated or dispatched by hand while this proposal waited; approving must not reopen it or move stock for it.
        if (request.Status != BloodRequestStatus.PendingApproval) return Fail("request_not_pending", $"The request is now {request.Status}, not awaiting approval; reject this proposal instead.");
        // The agent is an untrusted proposer: re-check its proposal against authoritative data before acting on it.
        var (recipients, proposalError) = await ValidateProposalAsync(response, request, ct);
        if (proposalError is not null) return Fail("invalid_workflow_output", proposalError);

        var version = await db.AgentApprovals.CountAsync(x => x.WorkflowExecutionId == workflowId, ct) + 1;
        db.AgentApprovals.Add(new AgentApproval(workflowId, version, approverUserId, ApprovalDecision.Approved, comments, timeProvider.GetUtcNow()));
        execution.SetStatus(WorkflowStatus.Approved);
        SetRequestStatus(request, BloodRequestStatus.Approved, approverUserId, comments ?? "Agent proposal approved.");
        if (!await TrySaveDecisionAsync(workflowId, ct)) return Fail("workflow_conflict", "This workflow was decided by someone else at the same time. Refresh to see the result.");

        var undispatched = new List<Guid>();
        try
        {
            if (response.ProposedReservationUnits > 0)
            {
                var plan = await inventory.ReserveAsync(new(request.Id, response.ProposedReservationUnits, $"workflow-{workflowId:N}", 60), ct);
                if (!plan.Succeeded) return await FailAfterApproval(execution, request, approverUserId, plan.ErrorCode!, plan.ErrorMessage!, undispatched, ct);
                undispatched.AddRange(plan.Value!.Reservations.Select(x => x.Id));
                // Stock can be used elsewhere while the proposal waits; dispatching part of it would mark the request Dispatched while short.
                if (!plan.Value.FullyReserved) return await FailAfterApproval(execution, request, approverUserId, "stock_unavailable", $"Only {plan.Value.ReservedUnits} of the {response.ProposedReservationUnits} proposed unit(s) are still in stock. Nothing was dispatched.", undispatched, ct);
                foreach (var reservationId in undispatched.ToList())
                {
                    var dispatch = await inventory.DispatchAsync(reservationId, approverUserId, ct);
                    if (!dispatch.Succeeded) return await FailAfterApproval(execution, request, approverUserId, dispatch.ErrorCode!, dispatch.ErrorMessage!, undispatched, ct);
                    undispatched.Remove(reservationId);
                }
            }
            if (recipients.Count > 0)
            {
                var payload = new Dictionary<string, string> { ["requestId"] = request.Id.ToString(), ["bloodType"] = request.BloodType.ToString(), ["urgency"] = request.Urgency.ToString() };
                var broadcast = await notifications.QueueApprovedBroadcastAsync(new(workflowId, recipients, "urgent-blood-request", payload, $"workflow-{workflowId:N}-broadcast"), ct);
                if (!broadcast.Succeeded) return await FailAfterApproval(execution, request, approverUserId, broadcast.ErrorCode!, broadcast.ErrorMessage!, undispatched, ct);
            }
            SetRequestStatus(request, BloodRequestStatus.Dispatched, approverUserId, "Approved workflow actions dispatched.");
            execution.Finish(WorkflowStatus.Completed, execution.FinalOutcomeJson, timeProvider.GetUtcNow());
            await db.SaveChangesAsync(ct);
            return WorkflowResult<WorkflowView>.Success(await Map(execution, ct));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Approved workflow {WorkflowId} failed while applying actions", workflowId);
            (execution, request) = await ReloadAsync(execution.Id, request.Id);
            return await FailAfterApproval(execution, request, approverUserId, "approval_actions_failed", "Approved actions could not be applied; the request was escalated for manual handling.", undispatched, CancellationToken.None);
        }
    }

    public async Task<WorkflowResult<WorkflowView>> RejectAsync(Guid workflowId, Guid approverUserId, string? comments, CancellationToken ct)
    {
        var execution = await db.AgentWorkflowExecutions.SingleOrDefaultAsync(x => x.Id == workflowId, ct);
        if (execution is null) return Fail("workflow_not_found", "Workflow was not found.");
        if (execution.Status != WorkflowStatus.PendingApproval) return Fail("workflow_not_pending", "Workflow is not awaiting approval.");
        var request = await db.BloodRequests.SingleOrDefaultAsync(x => x.Id == execution.BloodRequestId, ct);
        if (request is null) return Fail("request_not_found", "Blood request for this workflow was not found.");
        var version = await db.AgentApprovals.CountAsync(x => x.WorkflowExecutionId == workflowId, ct) + 1;
        db.AgentApprovals.Add(new AgentApproval(workflowId, version, approverUserId, ApprovalDecision.Rejected, comments, timeProvider.GetUtcNow()));
        execution.Finish(WorkflowStatus.Rejected, execution.FinalOutcomeJson, timeProvider.GetUtcNow());
        SetRequestStatus(request, BloodRequestStatus.ClosedUnfulfilled, approverUserId, comments ?? "Agent proposal rejected.");
        if (!await TrySaveDecisionAsync(workflowId, ct)) return Fail("workflow_conflict", "This workflow was decided by someone else at the same time. Refresh to see the result.");
        return WorkflowResult<WorkflowView>.Success(await Map(execution, ct));
    }

    public async Task<WorkflowResult<WorkflowView>> ReviseAsync(Guid workflowId, Guid approverUserId, string comments, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(comments)) return Fail("comments_required", "Revision comments are required.");
        var execution = await db.AgentWorkflowExecutions.SingleOrDefaultAsync(x => x.Id == workflowId, ct);
        if (execution is null) return Fail("workflow_not_found", "Workflow was not found.");
        if (execution.Status != WorkflowStatus.PendingApproval) return Fail("workflow_not_pending", "Workflow is not awaiting approval.");
        var version = await db.AgentApprovals.CountAsync(x => x.WorkflowExecutionId == workflowId, ct) + 1;
        db.AgentApprovals.Add(new AgentApproval(workflowId, version, approverUserId, ApprovalDecision.RevisionRequested, comments, timeProvider.GetUtcNow()));
        execution.SetStatus(WorkflowStatus.Revising);
        if (!await TrySaveDecisionAsync(workflowId, ct)) return Fail("workflow_conflict", "This workflow was decided by someone else at the same time. Refresh to see the result.");
        return await StartAsync(execution.BloodRequestId, $"revision requested: {comments}", ct);
    }

    private async Task<AgentRunRequest> BuildAgentRequestAsync(BloodRequest request, AgentWorkflowExecution execution, CancellationToken ct)
    {
        var compatible = inventory.Compatibility(request.BloodType).CompatibleDonorTypes;
        var donors = await db.Donors.AsNoTracking()
            .Where(x => x.IsActive && x.EligibilityStatus == EligibilityStatus.Eligible && x.BloodType == request.BloodType)
            .Take(MaxProposedRecipients)
            .Select(x => new AgentDonorCandidate(x.UserId.ToString(), x.BloodType.ToString(), null)).ToListAsync(ct);
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var stock = await db.InventoryLots.AsNoTracking()
            .Where(x => compatible.Contains(x.BloodType) && x.UnitsAvailable > 0 && x.ExpiryDate >= today && x.Status == InventoryLotStatus.Available)
            .OrderBy(x => x.ExpiryDate).Take(500)
            .Select(x => new AgentStockCandidate(x.Id.ToString(), x.BloodType.ToString(), x.UnitsAvailable, x.ExpiryDate.ToString("yyyy-MM-dd"))).ToListAsync(ct);
        var dispatched = await db.InventoryReservations.AsNoTracking().Where(x => x.BloodRequestId == request.Id && x.Status == ReservationStatus.Dispatched).SumAsync(x => x.Units, ct);
        // Only the outstanding units are offered to the agent (at least 1, the agent contract's minimum); approval re-checks the same bound.
        return new(execution.Id.ToString(), request.Id.ToString(), request.BloodType.ToString(), Math.Max(1, request.QuantityUnits - dispatched), request.Urgency.ToString(), request.Notes, donors, stock, execution.CorrelationId);
    }

    private static AgentStep ToStep(Guid workflowId, AgentRunStep step, DateTimeOffset now) => new(
        workflowId, step.Sequence, step.AgentName,
        JsonSerializer.Serialize(step.Input, Json), JsonSerializer.Serialize(step.Output, Json), JsonSerializer.Serialize(step.ToolCalls, Json),
        Enum.TryParse<AgentStepStatus>(step.Status, true, out var status) ? status : AgentStepStatus.Completed, now, now, step.ErrorCode, step.ErrorMessage);

    /// <summary>Re-validates the agent proposal so a faulty or manipulated agent cannot over-reserve stock or notify ineligible people.</summary>
    private async Task<(List<Guid> Recipients, string? Error)> ValidateProposalAsync(AgentRunResponse response, BloodRequest request, CancellationToken ct)
    {
        if (response.ProposedReservationUnits < 0 || response.ProposedReservationUnits > request.QuantityUnits)
            return ([], $"Proposed reservation of {response.ProposedReservationUnits} unit(s) is outside the requested quantity of {request.QuantityUnits}.");
        var dispatched = await db.InventoryReservations.Where(x => x.BloodRequestId == request.Id && x.Status == ReservationStatus.Dispatched).SumAsync(x => x.Units, ct);
        if (response.ProposedReservationUnits > request.QuantityUnits - dispatched)
            return ([], $"Proposed reservation of {response.ProposedReservationUnits} unit(s) exceeds the {Math.Max(0, request.QuantityUnits - dispatched)} unit(s) still outstanding; {dispatched} were already dispatched.");
        if (response.ProposedRecipientUserIds.Count > MaxProposedRecipients) return ([], $"Proposal lists more than {MaxProposedRecipients} recipients.");
        var recipients = new List<Guid>();
        foreach (var raw in response.ProposedRecipientUserIds.Distinct())
        {
            if (!Guid.TryParse(raw, out var id)) return ([], "Proposal contains a recipient identifier that is not a valid ID.");
            recipients.Add(id);
        }
        if (recipients.Count == 0) return (recipients, null);
        var valid = await db.Donors.AsNoTracking().CountAsync(x => recipients.Contains(x.UserId) && x.IsActive && x.EligibilityStatus == EligibilityStatus.Eligible && x.BloodType == request.BloodType, ct);
        return valid == recipients.Count ? (recipients, null) : ([], "Proposal includes recipients who are not active, eligible donors of the requested blood type.");
    }

    /// <summary>After a failed save the tracker still holds the rejected entities; drop them so the recovery save cannot fail the same way.</summary>
    private async Task<(AgentWorkflowExecution Execution, BloodRequest Request)> ReloadAsync(Guid executionId, Guid requestId)
    {
        db.ChangeTracker.Clear();
        return (await db.AgentWorkflowExecutions.SingleAsync(x => x.Id == executionId, CancellationToken.None), await db.BloodRequests.SingleAsync(x => x.Id == requestId, CancellationToken.None));
    }

    private async Task<bool> TrySaveDecisionAsync(Guid workflowId, CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); return true; }
        catch (DbUpdateException ex)
        {
            // The (workflow, version) unique index rejects a second concurrent decision.
            logger.LogWarning(ex, "Concurrent decision rejected for workflow {WorkflowId}", workflowId);
            return false;
        }
    }

    private async Task<WorkflowResult<WorkflowView>> FailAfterApproval(AgentWorkflowExecution execution, BloodRequest request, Guid actor, string code, string message, IReadOnlyCollection<Guid> undispatchedReservations, CancellationToken ct)
    {
        foreach (var reservationId in undispatchedReservations)
        {
            try { await inventory.ReleaseAsync(reservationId, $"Approval failed: {code}", ct); }
            catch (Exception ex) { logger.LogError(ex, "Could not release reservation {ReservationId} after failed approval", reservationId); }
        }
        execution.Finish(WorkflowStatus.EscalationRequired, execution.FinalOutcomeJson, timeProvider.GetUtcNow());
        SetRequestStatus(request, BloodRequestStatus.Escalated, actor, $"Approved action failed safely: {code}. {message}");
        await db.SaveChangesAsync(ct);
        return Fail(code, message);
    }

    private static AgentRunResponse? DeserializeOutcome(AgentWorkflowExecution execution)
    {
        try { return execution.FinalOutcomeJson is null ? null : JsonSerializer.Deserialize<AgentRunResponse>(execution.FinalOutcomeJson, Json); }
        catch (JsonException) { return null; }
    }

    private void SetRequestStatus(BloodRequest request, BloodRequestStatus status, Guid actor, string reason)
    {
        if (request.Status == status) return;
        var previous = request.Status;
        request.TransitionTo(status);
        db.RequestStatusHistory.Add(new RequestStatusHistory(request.Id, previous, status, actor, reason));
    }

    private static WorkflowResult<WorkflowView> Fail(string code, string message) => WorkflowResult<WorkflowView>.Failure(code, message);

    private async Task<WorkflowView> Map(AgentWorkflowExecution execution, CancellationToken ct) => (await MapMany([execution], ct))[0];

    /// <summary>Maps executions with two queries in total, regardless of how many executions are listed.</summary>
    private async Task<IReadOnlyList<WorkflowView>> MapMany(IReadOnlyList<AgentWorkflowExecution> executions, CancellationToken ct)
    {
        if (executions.Count == 0) return [];
        var ids = executions.Select(x => x.Id).ToList();
        var steps = (await db.AgentSteps.AsNoTracking().Where(s => ids.Contains(s.WorkflowExecutionId)).OrderBy(s => s.Sequence)
            .Select(s => new { s.WorkflowExecutionId, View = new WorkflowStepView(s.Id, s.Sequence, s.AgentName, s.InputJson, s.OutputJson, s.ToolCallsJson, s.Status, s.StartedAtUtc, s.CompletedAtUtc, s.ErrorCode, s.ErrorMessage) })
            .ToListAsync(ct)).ToLookup(x => x.WorkflowExecutionId, x => x.View);
        var approvals = (await db.AgentApprovals.AsNoTracking().Where(a => ids.Contains(a.WorkflowExecutionId)).OrderBy(a => a.Version)
            .Select(a => new { a.WorkflowExecutionId, View = new ApprovalView(a.Id, a.Version, a.ApproverUserId, a.Decision, a.Comments, a.DecidedAtUtc) })
            .ToListAsync(ct)).ToLookup(x => x.WorkflowExecutionId, x => x.View);
        return executions.Select(x => new WorkflowView(x.Id, x.BloodRequestId, x.AttemptNumber, x.Objective, x.PlanJson, x.Status, x.CorrelationId, x.StartedAtUtc, x.CompletedAtUtc, x.FinalOutcomeJson, steps[x.Id].ToList(), approvals[x.Id].ToList())).ToList();
    }
}
