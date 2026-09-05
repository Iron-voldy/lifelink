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

/// <summary>Approval must act on the state of the world at approval time, not at proposal time.</summary>
public sealed class WorkflowApprovalSafetyTests
{
    [Fact]
    public async Task ApprovalAfterStockWasUsedElsewhereEscalatesWithoutPartialDispatch()
    {
        var setup = Create();
        var started = await setup.Service.StartAsync(setup.Request.Id, "test", default);
        var lot = await setup.Db.InventoryLots.SingleAsync(); lot.AdjustAvailable(1); await setup.Db.SaveChangesAsync();

        var approved = await setup.Service.ApproveAsync(started.Value!.Id, setup.Admin.Id, null, default);

        Assert.Equal("stock_unavailable", approved.ErrorCode);
        Assert.Equal(BloodRequestStatus.Escalated, (await setup.Db.BloodRequests.SingleAsync()).Status);
        Assert.Empty(await setup.Db.DispatchRecords.ToListAsync());
        Assert.All(await setup.Db.InventoryReservations.ToListAsync(), x => Assert.Equal(ReservationStatus.Released, x.Status));
        Assert.Equal(1, (await setup.Db.InventoryLots.SingleAsync()).UnitsAvailable);
    }

    [Fact]
    public async Task ApprovalOfAClosedRequestChangesNothing()
    {
        var setup = Create();
        var started = await setup.Service.StartAsync(setup.Request.Id, "test", default);
        var request = await setup.Db.BloodRequests.SingleAsync(); request.TransitionTo(BloodRequestStatus.ClosedUnfulfilled); await setup.Db.SaveChangesAsync();

        var approved = await setup.Service.ApproveAsync(started.Value!.Id, setup.Admin.Id, null, default);

        Assert.Equal("request_not_pending", approved.ErrorCode);
        Assert.Equal(BloodRequestStatus.ClosedUnfulfilled, (await setup.Db.BloodRequests.SingleAsync()).Status);
        Assert.Equal(WorkflowStatus.PendingApproval, (await setup.Db.AgentWorkflowExecutions.SingleAsync()).Status);
        Assert.Empty(await setup.Db.InventoryReservations.ToListAsync());
        Assert.Empty(await setup.Db.AgentApprovals.ToListAsync());
    }

    [Fact]
    public async Task ApprovalCannotDispatchMoreThanTheUnitsStillOutstanding()
    {
        var setup = Create();
        var started = await setup.Service.StartAsync(setup.Request.Id, "test", default);
        Assert.Equal(3, (await setup.Inventory.DispatchRequestAsync(setup.Request.Id, setup.Admin.Id, default)).Value);

        var approved = await setup.Service.ApproveAsync(started.Value!.Id, setup.Admin.Id, null, default);

        Assert.Equal("invalid_workflow_output", approved.ErrorCode);
        Assert.Contains("outstanding", approved.ErrorMessage);
        Assert.Equal(3, await setup.Db.DispatchRecords.SumAsync(x => x.UnitsDispatched));
    }

    [Fact]
    public async Task StartIsRefusedOnceStockIsMovingAndAgentOnlySeesOutstandingUnits()
    {
        var setup = Create();
        var request = await setup.Db.BloodRequests.SingleAsync(); request.TransitionTo(BloodRequestStatus.Dispatched); await setup.Db.SaveChangesAsync();
        Assert.Equal("request_in_progress", (await setup.Service.StartAsync(setup.Request.Id, "again", default)).ErrorCode);
        Assert.Equal(BloodRequestStatus.Dispatched, (await setup.Db.BloodRequests.SingleAsync()).Status);

        request.TransitionTo(BloodRequestStatus.Escalated); await setup.Db.SaveChangesAsync();
        var lot = await setup.Db.InventoryLots.SingleAsync(); lot.Reserve(1); var hold = new InventoryReservation(request.Id, lot.Id, 1, DateTimeOffset.MaxValue, "manual:1"); hold.MarkDispatched(); setup.Db.InventoryReservations.Add(hold); await setup.Db.SaveChangesAsync();
        var started = await setup.Service.StartAsync(setup.Request.Id, "retry", default);
        Assert.Equal(WorkflowStatus.PendingApproval, started.Value!.Status);
        Assert.Equal(2, setup.Agent.LastRequest!.QuantityUnits);
        Assert.True((await setup.Service.ApproveAsync(started.Value.Id, setup.Admin.Id, null, default)).Succeeded);
        Assert.Equal(3, await setup.Db.InventoryReservations.Where(x => x.Status == ReservationStatus.Dispatched).SumAsync(x => x.Units));
    }

    private static Setup Create()
    {
        var options = new DbContextOptionsBuilder<LifeLinkDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options; var db = new LifeLinkDbContext(options); var now = new DateTimeOffset(2026, 8, 18, 0, 0, 0, TimeSpan.Zero); var clock = new FixedTimeProvider(now);
        var staff = new User("staff@example.com", "hash", UserRole.HospitalRequester); var admin = new User("admin@example.com", "hash", UserRole.BloodBankAdmin);
        var hospital = new Hospital("Hospital", "WF-2", "Colombo", null, null); hospital.SetVerification(VerificationStatus.Verified);
        var request = new BloodRequest(hospital.Id, staff.Id, BloodType.ONegative, 3, RequestUrgency.Critical, null, now.AddHours(4));
        var location = new BloodBankLocation("Central", "Colombo", 6.9m, 79.8m); var lot = new InventoryLot(location.Id, BloodType.ONegative, 3, new DateOnly(2026, 9, 1), "Synthetic");
        db.Users.AddRange(staff, admin); db.Hospitals.Add(hospital); db.BloodRequests.Add(request); db.BloodBankLocations.Add(location); db.InventoryLots.Add(lot); db.SaveChanges();
        var inventory = new InventoryService(db, clock); var notification = new NotificationService(db, new SuccessfulProvider(), clock);
        var agent = new ReserveAllAgent();
        return new(new WorkflowService(db, agent, inventory, notification, clock, NullLogger<WorkflowService>.Instance), inventory, db, request, admin, agent);
    }

    private sealed record Setup(WorkflowService Service, InventoryService Inventory, LifeLinkDbContext Db, BloodRequest Request, User Admin, ReserveAllAgent Agent);
    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider { public override DateTimeOffset GetUtcNow() => now; }
    private sealed class SuccessfulProvider : INotificationProvider { public Task<ProviderResult> SendAsync(NotificationDelivery delivery, CancellationToken ct) => Task.FromResult(new ProviderResult(true, null)); }
    /// <summary>Proposes reserving the whole request from stock, with no donor broadcast.</summary>
    private sealed class ReserveAllAgent : IAgentWorkflowClient
    {
        public AgentRunRequest? LastRequest { get; private set; }
        public Task<AgentRunResponse> RunAsync(AgentRunRequest request, CancellationToken ct)
        {
            LastRequest = request;
            var steps = Enumerable.Range(1, 4).Select(i => new AgentRunStep(i, "Agent" + i, new Dictionary<string, object?>(), new Dictionary<string, object?>(), [], "Completed", null, null)).ToList();
            return Task.FromResult(new AgentRunResponse("1.0", request.WorkflowId, new Dictionary<string, object?>(), steps, "PendingApproval", true, new Dictionary<string, object?>(), request.QuantityUnits, []));
        }
    }
}
