using LifeLink.Application.Requests;
using LifeLink.Domain.Entities;
using LifeLink.Domain.Enums;
using LifeLink.Infrastructure.Persistence;
using LifeLink.Infrastructure.Requests;
using Microsoft.EntityFrameworkCore;

namespace LifeLink.Tests.Requests;

public sealed class RequestServiceTests
{
    [Fact]
    public async Task CreateCalculatesCriticalUrgencyAndTriggersWorkflow()
    {
        var now = new DateTimeOffset(2026, 8, 18, 10, 0, 0, TimeSpan.Zero);
        var (service, db, user, trigger) = await CreateAsync(now);
        var registered = await service.RegisterHospitalAsync(user.Id, new("General Hospital", "GH-001", "Colombo", null, null, "Doctor"), default);
        await service.SetHospitalVerificationAsync(registered.Value!.Id, VerificationStatus.Verified, default);
        var result = await service.CreateAsync(user.Id, new(BloodType.ONegative, 2, RequestUrgency.Routine, null, now.AddHours(1)), default);
        Assert.Equal(RequestUrgency.Critical, result.Value!.Urgency);
        Assert.Equal(result.Value.Id, trigger.RequestIds.Single());
        Assert.Single(await db.RequestStatusHistory.ToListAsync());
    }

    [Fact]
    public async Task UnverifiedHospitalCannotSubmitRequest()
    {
        var now = DateTimeOffset.UtcNow;
        var (service, _, user, _) = await CreateAsync(now);
        await service.RegisterHospitalAsync(user.Id, new("General Hospital", "GH-002", "Colombo", null, null, "Doctor"), default);
        var result = await service.CreateAsync(user.Id, new(BloodType.APositive, 1, RequestUrgency.Routine, null, now.AddDays(1)), default);
        Assert.Equal("hospital_not_verified", result.ErrorCode);
    }

    [Fact]
    public async Task InvalidLifecycleTransitionIsRejected()
    {
        var now = DateTimeOffset.UtcNow;
        var (service, _, user, _) = await CreateAsync(now);
        var hospital = await service.RegisterHospitalAsync(user.Id, new("General Hospital", "GH-003", "Colombo", null, null, "Doctor"), default);
        await service.SetHospitalVerificationAsync(hospital.Value!.Id, VerificationStatus.Verified, default);
        var request = await service.CreateAsync(user.Id, new(BloodType.BPositive, 1, RequestUrgency.Routine, null, now.AddDays(1)), default);
        var transition = await service.TransitionAsync(request.Value!.Id, BloodRequestStatus.Fulfilled, user.Id, null, default);
        Assert.Equal("invalid_transition", transition.ErrorCode);
    }

    [Fact]
    public async Task SummaryCountsOnlyOpenCriticalRequests()
    {
        var now = DateTimeOffset.UtcNow;
        var (service, _, user, _) = await CreateAsync(now);
        var hospital = await service.RegisterHospitalAsync(user.Id, new("General Hospital", "GH-004", "Colombo", null, null, "Doctor"), default);
        await service.SetHospitalVerificationAsync(hospital.Value!.Id, VerificationStatus.Verified, default);
        var open = await service.CreateAsync(user.Id, new(BloodType.OPositive, 6, RequestUrgency.Critical, null, now.AddDays(1)), default);
        var closed = await service.CreateAsync(user.Id, new(BloodType.OPositive, 6, RequestUrgency.Critical, null, now.AddDays(1)), default);
        await service.TransitionAsync(closed.Value!.Id, BloodRequestStatus.ClosedUnfulfilled, user.Id, "Closed in test.", default);
        var summary = await service.SummaryAsync(null, default);
        Assert.Equal(1, summary.Critical);
        Assert.Equal(1, summary.Open);
        Assert.Equal(RequestUrgency.Critical, open.Value!.Urgency);
    }

    private static async Task<(RequestService Service, LifeLinkDbContext Db, User User, CapturingTrigger Trigger)> CreateAsync(DateTimeOffset now)
    {
        var options = new DbContextOptionsBuilder<LifeLinkDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var db = new LifeLinkDbContext(options); var user = new User("staff@example.com", "hash", UserRole.HospitalRequester); db.Users.Add(user); await db.SaveChangesAsync();
        var trigger = new CapturingTrigger(); return (new RequestService(db, new FixedTimeProvider(now), trigger), db, user, trigger);
    }
    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider { public override DateTimeOffset GetUtcNow() => now; }
    private sealed class CapturingTrigger : IRequestWorkflowTrigger
    {
        public List<Guid> RequestIds { get; } = [];
        public Task TriggerAsync(Guid requestId, string reason, CancellationToken ct) { RequestIds.Add(requestId); return Task.CompletedTask; }
    }
}
