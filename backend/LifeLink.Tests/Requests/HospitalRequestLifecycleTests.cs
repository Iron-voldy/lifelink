using LifeLink.Application.Requests;
using LifeLink.Domain.Entities;
using LifeLink.Domain.Enums;
using LifeLink.Infrastructure.Persistence;
using LifeLink.Infrastructure.Requests;
using Microsoft.EntityFrameworkCore;

namespace LifeLink.Tests.Requests;

/// <summary>Hospital requester rules: input normalisation, validation bounds and which lifecycle actions a hospital may take.</summary>
public sealed class HospitalRequestLifecycleTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 4, 6, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task RequiredByWithLocalOffsetIsStoredAsUtc()
    {
        var (service, _, user, _) = await VerifiedHospitalAsync();
        var local = new DateTimeOffset(2026, 10, 5, 15, 30, 0, TimeSpan.FromHours(5.5));
        var result = await service.CreateAsync(user.Id, new(BloodType.APositive, 1, RequestUrgency.Routine, "  ", local), default);
        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.Equal(TimeSpan.Zero, result.Value!.RequiredByUtc.Offset);
        Assert.Equal(local.UtcDateTime, result.Value.RequiredByUtc.UtcDateTime);
        Assert.Null(result.Value.Notes);
    }

    [Theory]
    [InlineData(-1, false)]
    [InlineData(29 * 24, true)]
    [InlineData(31 * 24, false)]
    public async Task RequiredByMustBeInTheFutureAndWithinThirtyDays(int hoursFromNow, bool accepted)
    {
        var (service, _, user, _) = await VerifiedHospitalAsync();
        var result = await service.CreateAsync(user.Id, new(BloodType.APositive, 1, RequestUrgency.Routine, null, Now.AddHours(hoursFromNow)), default);
        Assert.Equal(accepted, result.Succeeded);
        if (!accepted) Assert.Equal("invalid_request", result.ErrorCode);
    }

    [Theory]
    [InlineData(1, RequestUrgency.Routine, 48, RequestUrgency.Routine)]
    [InlineData(3, RequestUrgency.Routine, 48, RequestUrgency.Urgent)]
    [InlineData(1, RequestUrgency.Routine, 10, RequestUrgency.Urgent)]
    [InlineData(6, RequestUrgency.Routine, 48, RequestUrgency.Critical)]
    [InlineData(1, RequestUrgency.Routine, 2, RequestUrgency.Critical)]
    [InlineData(1, RequestUrgency.Critical, 48, RequestUrgency.Critical)]
    public async Task UrgencyIsTheHigherOfRequestedAndCalculated(int units, RequestUrgency requested, int hours, RequestUrgency expected)
    {
        var (service, _, user, _) = await VerifiedHospitalAsync();
        var result = await service.CreateAsync(user.Id, new(BloodType.OPositive, units, requested, null, Now.AddHours(hours)), default);
        Assert.Equal(expected, result.Value!.Urgency);
    }

    [Fact]
    public async Task RequestWithUndefinedEnumValueIsRejected()
    {
        var (service, _, user, _) = await VerifiedHospitalAsync();
        var result = await service.CreateAsync(user.Id, new((BloodType)42, 1, RequestUrgency.Routine, null, Now.AddDays(1)), default);
        Assert.Equal("invalid_request", result.ErrorCode);
    }

    [Theory]
    [InlineData("City Hospital", "  qa reg #1 !! ", "Colombo", "Doctor", null, null)]
    [InlineData("City Hospital", "AB", "Colombo", "Doctor", null, null)]
    [InlineData("12345", "REG-100", "Colombo", "Doctor", null, null)]
    [InlineData("City Hospital", "REG-100", "  ", "Doctor", null, null)]
    [InlineData("City Hospital", "REG-100", "Colombo", " ", null, null)]
    [InlineData("City Hospital", "REG-100", "Colombo", "Doctor", 6.9, null)]
    public async Task InvalidHospitalRegistrationIsRejected(string name, string registration, string address, string position, double? latitude, double? longitude)
    {
        var (service, _, user, _) = await CreateAsync();
        var result = await service.RegisterHospitalAsync(user.Id, new(name, registration, address, (decimal?)latitude, (decimal?)longitude, position), default);
        Assert.Equal("invalid_hospital", result.ErrorCode);
    }

    [Fact]
    public async Task RegistrationNumberIsNormalisedAndDuplicatesAreCaseInsensitive()
    {
        var (service, db, user, _) = await CreateAsync();
        var first = await service.RegisterHospitalAsync(user.Id, new("  City Hospital ", "  phsrc/mh-001 ", " Colombo 07 ", 6.9m, 79.8m, " Doctor "), default);
        Assert.True(first.Succeeded, first.ErrorMessage);
        Assert.Equal("PHSRC/MH-001", first.Value!.RegistrationNumber);
        Assert.Equal("City Hospital", first.Value.Name);
        var second = new User("second@example.com", "hash", UserRole.HospitalRequester); db.Users.Add(second); await db.SaveChangesAsync();
        var duplicate = await service.RegisterHospitalAsync(second.Id, new("Other Hospital", "Phsrc/MH-001", "Kandy", null, null, "Nurse"), default);
        Assert.Equal("registration_exists", duplicate.ErrorCode);
    }

    [Fact]
    public async Task StaffCanReadTheirOwnHospitalStatus()
    {
        var (service, _, user, _) = await CreateAsync();
        Assert.Equal("hospital_profile_required", (await service.GetHospitalForStaffAsync(user.Id, default)).ErrorCode);
        await service.RegisterHospitalAsync(user.Id, new("City Hospital", "REG-200", "Colombo", null, null, "Doctor"), default);
        var mine = await service.GetHospitalForStaffAsync(user.Id, default);
        Assert.Equal(VerificationStatus.Pending, mine.Value!.VerificationStatus);
    }

    [Theory]
    [InlineData(BloodRequestStatus.Approved)]
    [InlineData(BloodRequestStatus.Dispatched)]
    public async Task CommittedRequestCannotBeEscalatedEditedOrWithdrawn(BloodRequestStatus status)
    {
        var (service, db, user, trigger) = await VerifiedHospitalAsync();
        var request = await CreateRequestAsync(service, user);
        await SetStatusAsync(db, request.Id, status); trigger.Reasons.Clear();
        Assert.Equal("request_not_escalatable", (await service.EscalateAsync(request.Id, user.Id, null, default)).ErrorCode);
        Assert.Equal("request_not_editable", (await service.UpdateAsync(request.Id, Input(), user.Id, default)).ErrorCode);
        Assert.Equal("request_not_cancellable", (await service.CancelAsync(request.Id, user.Id, default)).ErrorCode);
        Assert.Empty(trigger.Reasons);
        Assert.Equal(status, (await db.BloodRequests.AsNoTracking().SingleAsync(x => x.Id == request.Id)).Status);
    }

    [Fact]
    public async Task CriticalEscalatedRequestCannotBeEscalatedAgain()
    {
        var (service, _, user, _) = await VerifiedHospitalAsync();
        var request = await CreateRequestAsync(service, user);
        Assert.Equal(RequestUrgency.Urgent, (await service.EscalateAsync(request.Id, user.Id, null, default)).Value!.Urgency);
        Assert.Equal(RequestUrgency.Critical, (await service.EscalateAsync(request.Id, user.Id, null, default)).Value!.Urgency);
        Assert.Equal("request_not_escalatable", (await service.EscalateAsync(request.Id, user.Id, null, default)).ErrorCode);
    }

    [Theory]
    [InlineData(VerificationStatus.Suspended)]
    [InlineData(VerificationStatus.Rejected)]
    public async Task HospitalThatLostVerificationCanOnlyWithdrawRequests(VerificationStatus verification)
    {
        var (service, _, user, _) = await VerifiedHospitalAsync();
        var request = await CreateRequestAsync(service, user);
        await service.SetHospitalVerificationAsync(request.HospitalId, verification, default);
        Assert.Equal("hospital_not_verified", (await service.EscalateAsync(request.Id, user.Id, null, default)).ErrorCode);
        Assert.Equal("hospital_not_verified", (await service.UpdateAsync(request.Id, Input(), user.Id, default)).ErrorCode);
        Assert.Equal("hospital_not_verified", (await service.CreateAsync(user.Id, Input(), default)).ErrorCode);
        Assert.Equal(BloodRequestStatus.ClosedUnfulfilled, (await service.CancelAsync(request.Id, user.Id, default)).Value!.Status);
    }

    [Fact]
    public async Task WithdrawingARequestRetiresItsPendingAgentProposal()
    {
        var (service, db, user, _) = await VerifiedHospitalAsync();
        var request = await CreateRequestAsync(service, user);
        var workflow = await PendingWorkflowAsync(db, request.Id);
        var closed = await service.CancelAsync(request.Id, user.Id, default);
        Assert.Equal(BloodRequestStatus.ClosedUnfulfilled, closed.Value!.Status);
        Assert.Equal(WorkflowStatus.Rejected, (await db.AgentWorkflowExecutions.AsNoTracking().SingleAsync(x => x.Id == workflow.Id)).Status);
        Assert.Equal("request_closed", (await service.CancelAsync(request.Id, user.Id, default)).ErrorCode);
    }

    [Fact]
    public async Task WithdrawingARequestReleasesItsHeldStock()
    {
        var (service, db, user, _) = await VerifiedHospitalAsync();
        var request = await CreateRequestAsync(service, user);
        var lot = new InventoryLot(Guid.NewGuid(), BloodType.APositive, 5, DateOnly.FromDateTime(Now.UtcDateTime).AddDays(20), "test"); lot.Reserve(2);
        var reservation = new InventoryReservation(request.Id, lot.Id, 2, Now.AddHours(1), "test-hold");
        db.InventoryLots.Add(lot); db.InventoryReservations.Add(reservation); await db.SaveChangesAsync();
        await service.CancelAsync(request.Id, user.Id, default);
        Assert.Equal(ReservationStatus.Released, (await db.InventoryReservations.AsNoTracking().SingleAsync(x => x.Id == reservation.Id)).Status);
        Assert.Equal(5, (await db.InventoryLots.AsNoTracking().SingleAsync(x => x.Id == lot.Id)).UnitsAvailable);
    }

    [Fact]
    public async Task EditingARequestAwaitingApprovalSupersedesTheProposalAndReanalyses()
    {
        var (service, db, user, trigger) = await VerifiedHospitalAsync();
        var request = await CreateRequestAsync(service, user);
        var workflow = await PendingWorkflowAsync(db, request.Id); trigger.Reasons.Clear();
        var updated = await service.UpdateAsync(request.Id, Input() with { QuantityUnits = 4 }, user.Id, default);
        Assert.True(updated.Succeeded, updated.ErrorMessage);
        Assert.Equal(4, updated.Value!.QuantityUnits);
        Assert.Equal(WorkflowStatus.Revising, (await db.AgentWorkflowExecutions.AsNoTracking().SingleAsync(x => x.Id == workflow.Id)).Status);
        Assert.Equal("request-updated", trigger.Reasons.Single());
    }

    [Fact]
    public async Task EditingAnEscalatedRequestKeepsItsEscalatedUrgency()
    {
        var (service, _, user, _) = await VerifiedHospitalAsync();
        var request = await CreateRequestAsync(service, user);
        await service.EscalateAsync(request.Id, user.Id, null, default); await service.EscalateAsync(request.Id, user.Id, null, default);
        var updated = await service.UpdateAsync(request.Id, Input(), user.Id, default);
        Assert.Equal(RequestUrgency.Critical, updated.Value!.Urgency);
    }

    [Fact]
    public async Task ManualApprovalIsRefusedWhileAnAgentProposalIsPending()
    {
        var (service, db, user, _) = await VerifiedHospitalAsync();
        var request = await CreateRequestAsync(service, user);
        await PendingWorkflowAsync(db, request.Id);
        Assert.Equal("workflow_pending", (await service.TransitionAsync(request.Id, BloodRequestStatus.Approved, user.Id, null, default)).ErrorCode);
    }

    private static RequestInput Input() => new(BloodType.APositive, 1, RequestUrgency.Routine, null, Now.AddDays(2));

    private static async Task<BloodRequestView> CreateRequestAsync(RequestService service, User user)
    {
        var result = await service.CreateAsync(user.Id, Input(), default); Assert.True(result.Succeeded, result.ErrorMessage); return result.Value!;
    }

    private static async Task<AgentWorkflowExecution> PendingWorkflowAsync(LifeLinkDbContext db, Guid requestId)
    {
        var execution = new AgentWorkflowExecution(requestId, 1, "test", Guid.NewGuid().ToString("N"));
        execution.Finish(WorkflowStatus.PendingApproval, "{}", Now); db.AgentWorkflowExecutions.Add(execution);
        await SetStatusAsync(db, requestId, BloodRequestStatus.PendingApproval);
        return execution;
    }

    private static async Task SetStatusAsync(LifeLinkDbContext db, Guid requestId, BloodRequestStatus status)
    {
        (await db.BloodRequests.SingleAsync(x => x.Id == requestId)).TransitionTo(status); await db.SaveChangesAsync();
    }

    private static async Task<(RequestService Service, LifeLinkDbContext Db, User User, RecordingTrigger Trigger)> VerifiedHospitalAsync()
    {
        var context = await CreateAsync();
        var hospital = await context.Service.RegisterHospitalAsync(context.User.Id, new("General Hospital", $"GH-{Guid.NewGuid():N}"[..20], "Colombo", null, null, "Doctor"), default);
        await context.Service.SetHospitalVerificationAsync(hospital.Value!.Id, VerificationStatus.Verified, default);
        return context;
    }

    private static async Task<(RequestService Service, LifeLinkDbContext Db, User User, RecordingTrigger Trigger)> CreateAsync()
    {
        var options = new DbContextOptionsBuilder<LifeLinkDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var db = new LifeLinkDbContext(options); var user = new User("staff@example.com", "hash", UserRole.HospitalRequester); db.Users.Add(user); await db.SaveChangesAsync();
        var trigger = new RecordingTrigger(); return (new RequestService(db, new FixedTimeProvider(Now), trigger), db, user, trigger);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider { public override DateTimeOffset GetUtcNow() => now; }
    private sealed class RecordingTrigger : IRequestWorkflowTrigger
    {
        public List<string> Reasons { get; } = [];
        public Task TriggerAsync(Guid requestId, string reason, CancellationToken ct) { Reasons.Add(reason); return Task.CompletedTask; }
    }
}
