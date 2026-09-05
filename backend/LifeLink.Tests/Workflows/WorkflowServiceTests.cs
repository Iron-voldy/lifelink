using LifeLink.Application.Notifications;
using LifeLink.Application.Workflows;
using LifeLink.Domain.Entities;
using LifeLink.Domain.Enums;
using LifeLink.Infrastructure.Inventory;
using LifeLink.Infrastructure.Notifications;
using LifeLink.Infrastructure.Persistence;
using LifeLink.Infrastructure.Workflows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace LifeLink.Tests.Workflows;

public sealed class WorkflowServiceTests
{
    [Fact]
    public async Task StartPersistsFourStepsAndRequiresApproval()
    {
        var setup = Create(false);
        var result = await setup.Service.StartAsync(setup.Request.Id, "test", default);
        Assert.True(result.Succeeded);
        Assert.Equal(WorkflowStatus.PendingApproval, result.Value!.Status);
        Assert.Equal(4, result.Value.Steps.Count);
        Assert.Equal(BloodRequestStatus.PendingApproval, (await setup.Db.BloodRequests.SingleAsync()).Status);
    }

    [Fact]
    public async Task ApprovalExecutesReservationDispatchAndQueuesBroadcast()
    {
        var setup = Create(false);
        var started = await setup.Service.StartAsync(setup.Request.Id, "test", default);
        var approved = await setup.Service.ApproveAsync(started.Value!.Id, setup.Admin.Id, "Approved by test admin", default);
        Assert.True(approved.Succeeded);
        Assert.Equal(WorkflowStatus.Completed, approved.Value!.Status);
        Assert.Equal(BloodRequestStatus.Dispatched, (await setup.Db.BloodRequests.SingleAsync()).Status);
        Assert.All(await setup.Db.InventoryReservations.ToListAsync(), x => Assert.Equal(ReservationStatus.Dispatched, x.Status));
        Assert.Single(await setup.Db.DispatchRecords.ToListAsync());
        Assert.Single(await setup.Db.Notifications.ToListAsync());
        Assert.Single(await setup.Db.AgentApprovals.ToListAsync());
    }

    [Fact]
    public async Task AgentFailurePersistsSafeEscalation()
    {
        var setup = Create(true);
        var result = await setup.Service.StartAsync(setup.Request.Id, "test", default);
        Assert.True(result.Succeeded);
        Assert.Equal(WorkflowStatus.EscalationRequired, result.Value!.Status);
        Assert.Equal(BloodRequestStatus.Escalated, (await setup.Db.BloodRequests.SingleAsync()).Status);
    }

    [Fact]
    public async Task StartRejectsDuplicateActiveWorkflowAttempt()
    {
        var setup = Create(false);
        var first = await setup.Service.StartAsync(setup.Request.Id, "first", default);
        var duplicate = await setup.Service.StartAsync(setup.Request.Id, "duplicate", default);

        Assert.True(first.Succeeded);
        Assert.False(duplicate.Succeeded);
        Assert.Equal("workflow_active", duplicate.ErrorCode);
        Assert.Single(await setup.Db.AgentWorkflowExecutions.ToListAsync());
    }

    [Fact]
    public async Task UnexpectedAgentExceptionStillEndsInTerminalEscalationAndUnblocksRetry()
    {
        var setup = Create(false, new InvalidCastException("boom"));
        var result = await setup.Service.StartAsync(setup.Request.Id, "test", default);
        Assert.True(result.Succeeded);
        Assert.Equal(WorkflowStatus.EscalationRequired, result.Value!.Status);
        Assert.DoesNotContain("boom", result.Value.FinalOutcomeJson);
        Assert.Null(await setup.Db.AgentWorkflowExecutions.FirstOrDefaultAsync(x => x.Status == WorkflowStatus.Running));
        var retry = await setup.Service.StartAsync(setup.Request.Id, "retry", default);
        Assert.NotEqual("workflow_active", retry.ErrorCode);
    }

    [Theory]
    [InlineData(999, null, "invalid_workflow_output")]
    [InlineData(2, "not-a-guid", "invalid_workflow_output")]
    [InlineData(2, "00000000-0000-0000-0000-000000000042", "invalid_workflow_output")]
    public async Task ApprovalRejectsUntrustworthyAgentProposalWithoutSideEffects(int units, string? recipient, string expectedCode)
    {
        var setup = Create(false, null, units, recipient);
        var started = await setup.Service.StartAsync(setup.Request.Id, "test", default);
        var approved = await setup.Service.ApproveAsync(started.Value!.Id, setup.Admin.Id, null, default);
        Assert.False(approved.Succeeded);
        Assert.Equal(expectedCode, approved.ErrorCode);
        Assert.Equal(WorkflowStatus.PendingApproval, (await setup.Db.AgentWorkflowExecutions.SingleAsync()).Status);
        Assert.Empty(await setup.Db.InventoryReservations.ToListAsync());
        Assert.Empty(await setup.Db.Notifications.ToListAsync());
    }

    [Fact]
    public async Task SecondDecisionOnSameWorkflowIsRejectedAsNotPending()
    {
        var setup = Create(false);
        var started = await setup.Service.StartAsync(setup.Request.Id, "test", default);
        Assert.True((await setup.Service.ApproveAsync(started.Value!.Id, setup.Admin.Id, null, default)).Succeeded);
        var again = await setup.Service.ApproveAsync(started.Value.Id, setup.Admin.Id, null, default);
        Assert.Equal("workflow_not_pending", again.ErrorCode);
        Assert.Single(await setup.Db.AgentApprovals.ToListAsync());
    }

    private static Setup Create(bool fail, Exception? error = null, int units = 2, string? recipient = null)
    {
        var options = new DbContextOptionsBuilder<LifeLinkDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options; var db = new LifeLinkDbContext(options); var now = new DateTimeOffset(2026, 8, 18, 0, 0, 0, TimeSpan.Zero); var clock = new FixedTimeProvider(now);
        var staff = new User("staff@example.com", "hash", UserRole.HospitalRequester); var admin = new User("admin@example.com", "hash", UserRole.BloodBankAdmin); var donorUser = new User("donor@example.com", "hash", UserRole.Donor); var donor = new Donor(donorUser.Id, BloodType.ONegative, new DateOnly(1990, 1, 1), "Colombo", null, null, "[]"); donor.ApplyEligibility(EligibilityStatus.Eligible); var hospital = new Hospital("Hospital", "WF-1", "Colombo", null, null); hospital.SetVerification(VerificationStatus.Verified); var request = new BloodRequest(hospital.Id, staff.Id, BloodType.ONegative, 3, RequestUrgency.Critical, "clinical data", now.AddHours(1)); var location = new BloodBankLocation("Central", "Colombo", 6.9m, 79.8m); var lot = new InventoryLot(location.Id, BloodType.ONegative, 2, new DateOnly(2026, 9, 1), "Synthetic");
        db.Users.AddRange(staff, admin, donorUser); db.Donors.Add(donor); db.Hospitals.Add(hospital); db.BloodRequests.Add(request); db.BloodBankLocations.Add(location); db.InventoryLots.Add(lot); db.SaveChanges();
        var inventory = new InventoryService(db, clock); var notification = new NotificationService(db, new SuccessfulProvider(), clock); var client = new FakeAgentClient(donorUser.Id, fail, error, units, recipient); var service = new WorkflowService(db, client, inventory, notification, clock, NullLogger<WorkflowService>.Instance); return new(service, db, request, admin);
    }

    private sealed record Setup(WorkflowService Service, LifeLinkDbContext Db, BloodRequest Request, User Admin);
    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider { public override DateTimeOffset GetUtcNow() => now; }
    private sealed class SuccessfulProvider : INotificationProvider { public Task<ProviderResult> SendAsync(NotificationDelivery delivery, CancellationToken ct) => Task.FromResult(new ProviderResult(true, null)); }
    private sealed class FakeAgentClient(Guid donorUserId, bool fail, Exception? error, int units, string? recipient) : IAgentWorkflowClient
    {
        public Task<AgentRunResponse> RunAsync(AgentRunRequest request, CancellationToken ct)
        {
            if (error is not null) throw error;
            if (fail) throw new HttpRequestException("agent unavailable");
            var steps = Enumerable.Range(1, 4).Select(i => new AgentRunStep(i, i switch { 1 => "CoordinatorAgent", 2 => "DomainAnalysisAgent", 3 => "DispatchAgent", _ => "ValidationSafetyAgent" }, new Dictionary<string, object?>(), new Dictionary<string, object?>(), [], "Completed", null, null)).ToList();
            return Task.FromResult(new AgentRunResponse("1.0", request.WorkflowId, new Dictionary<string, object?> { ["objective"] = "test" }, steps, "PendingApproval", true, new Dictionary<string, object?> { ["safe"] = true }, units, [recipient ?? donorUserId.ToString()]));
        }
    }
}
