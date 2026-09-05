using LifeLink.Application.Camps;
using LifeLink.Application.Notifications;
using LifeLink.Domain.Entities;
using LifeLink.Domain.Enums;
using LifeLink.Infrastructure.Camps;
using LifeLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LifeLink.Tests.Camps;

/// <summary>Admin-side camp rules: time zones, transition timing, check-in state and attendance counts.</summary>
public sealed class CampAdminValidationTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 18, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task CreateStoresLocalOffsetTimesAsUtc()
    {
        var (service, db, admin, _, _) = Create();
        var colombo = TimeSpan.FromHours(5.5);
        var start = new DateTimeOffset(2026, 9, 1, 10, 0, 0, colombo);
        var result = await service.CreateAsync(admin.Id, new("Camp", "Kandy", null, null, start, start.AddHours(2), 2, 30), default);
        Assert.True(result.Succeeded);
        Assert.Equal(TimeSpan.Zero, result.Value!.StartsAtUtc.Offset);
        Assert.Equal(start.UtcDateTime, result.Value.StartsAtUtc.UtcDateTime);
        Assert.All(await db.CampSlots.ToListAsync(), slot => Assert.Equal(TimeSpan.Zero, slot.SlotTimeUtc.Offset));
    }

    [Fact]
    public async Task CreateRejectsStartYearsAhead()
    {
        var (service, _, admin, _, _) = Create();
        var result = await service.CreateAsync(admin.Id, new("Camp", "Kandy", null, null, Now.AddYears(3), Now.AddYears(3).AddHours(2), 2, 30), default);
        Assert.Equal("invalid_camp", result.ErrorCode);
        Assert.Contains("two years", result.ErrorMessage);
    }

    [Fact]
    public async Task CampCannotStartDaysEarlyOrBeScheduledAfterItsStartPassed()
    {
        var (service, _, admin, _, clock) = Create();
        var camp = (await service.CreateAsync(admin.Id, new("Camp", "Kandy", null, null, Now.AddDays(2), Now.AddDays(2).AddHours(2), 2, 30), default)).Value!;
        var late = (await service.CreateAsync(admin.Id, new("Late", "Kandy", null, null, Now.AddHours(1), Now.AddHours(3), 2, 30), default)).Value!;
        Assert.True((await service.TransitionAsync(camp.Id, CampStatus.Scheduled, default)).Succeeded);

        var early = await service.TransitionAsync(camp.Id, CampStatus.InProgress, default);
        Assert.Equal("invalid_transition", early.ErrorCode);

        clock.Now = Now.AddDays(2).AddMinutes(-30);
        Assert.True((await service.TransitionAsync(camp.Id, CampStatus.InProgress, default)).Succeeded);
        var scheduleLate = await service.TransitionAsync(late.Id, CampStatus.Scheduled, default);
        Assert.Equal("invalid_transition", scheduleLate.ErrorCode);
        Assert.True((await service.TransitionAsync(late.Id, CampStatus.Cancelled, default)).Succeeded);
    }

    [Fact]
    public async Task CheckInRequiresCampInProgressAndBookedCountKeepsCheckedInDonors()
    {
        var (service, _, admin, donorUser, clock) = Create();
        var camp = (await service.CreateAsync(admin.Id, new("Camp", "Kandy", null, null, Now.AddHours(5), Now.AddHours(7), 3, 30), default)).Value!;
        await service.TransitionAsync(camp.Id, CampStatus.Scheduled, default);
        var booking = (await service.BookAsync(camp.Id, donorUser.Id, null, default)).Value!;

        var tooEarly = await service.CheckInAsync(camp.Id, booking.Id, default);
        Assert.Equal("invalid_slot_state", tooEarly.ErrorCode);

        clock.Now = Now.AddHours(5);
        await service.TransitionAsync(camp.Id, CampStatus.InProgress, default);
        Assert.True((await service.CheckInAsync(camp.Id, booking.Id, default)).Succeeded);
        var view = (await service.GetAsync(camp.Id, default)).Value!;
        Assert.Equal(1, view.BookedSlots);
        Assert.Equal(2, view.AvailableSlots);
    }

    [Fact]
    public async Task AttendanceOfCancelledCampCountsOnlyBookedSlotsAsCancelled()
    {
        var (service, _, admin, donorUser, _) = Create();
        var camp = (await service.CreateAsync(admin.Id, new("Camp", "Kandy", null, null, Now.AddDays(1), Now.AddDays(1).AddHours(2), 4, 30), default)).Value!;
        await service.TransitionAsync(camp.Id, CampStatus.Scheduled, default);
        await service.BookAsync(camp.Id, donorUser.Id, null, default);
        await service.TransitionAsync(camp.Id, CampStatus.Cancelled, default);
        var report = (await service.AttendanceAsync(camp.Id, default)).Value!;
        Assert.Equal(1, report.Cancelled);
        Assert.Equal(0, report.Booked);
    }

    private static (CampService Service, LifeLinkDbContext Db, User Admin, User DonorUser, MutableTimeProvider Clock) Create()
    {
        var options = new DbContextOptionsBuilder<LifeLinkDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options; var db = new LifeLinkDbContext(options);
        var admin = new User("admin@example.com", "hash", UserRole.BloodBankAdmin); var donorUser = new User("donor@example.com", "hash", UserRole.Donor);
        var donor = new Donor(donorUser.Id, BloodType.OPositive, new DateOnly(1990, 1, 1), "Colombo", null, null, "[]"); donor.ApplyEligibility(EligibilityStatus.Eligible);
        db.Users.AddRange(admin, donorUser); db.Donors.Add(donor); db.SaveChanges();
        var clock = new MutableTimeProvider(Now); return (new CampService(db, clock, new FakeNotifications()), db, admin, donorUser, clock);
    }
    private sealed class MutableTimeProvider(DateTimeOffset now) : TimeProvider { public DateTimeOffset Now { get; set; } = now; public override DateTimeOffset GetUtcNow() => Now; }
    private sealed class FakeNotifications : INotificationService
    {
        public Task<NotificationResult<Guid>> QueueUserNotificationAsync(Guid userId, string type, IReadOnlyDictionary<string, string> data, string idempotencyKey, CancellationToken ct) => Task.FromResult(NotificationResult<Guid>.Success(Guid.NewGuid()));
        public Task<NotificationResult<bool>> RegisterDeviceAsync(Guid userId, DeviceTokenInput input, CancellationToken ct) => throw new NotSupportedException();
        public Task<NotificationResult<bool>> RemoveDeviceAsync(Guid userId, string token, CancellationToken ct) => throw new NotSupportedException();
        public Task<NotificationResult<BroadcastResult>> QueueApprovedBroadcastAsync(BroadcastInput input, CancellationToken ct) => throw new NotSupportedException();
        public Task<int> ProcessOutboxAsync(int batchSize, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<NotificationView>> HistoryAsync(Guid userId, CancellationToken ct) => throw new NotSupportedException();
    }
}
