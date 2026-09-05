using LifeLink.Application.Camps;
using LifeLink.Application.Notifications;
using LifeLink.Domain.Entities;
using LifeLink.Domain.Enums;
using LifeLink.Infrastructure.Camps;
using LifeLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LifeLink.Tests.Camps;

public sealed class CampServiceTests
{
    [Fact]
    public async Task EligibleDonorCanBookOnceCheckInAndAppearInAttendance()
    {
        var now = new DateTimeOffset(2026, 8, 18, 0, 0, 0, TimeSpan.Zero); var (service, db, admin, donorUser) = Create(now);
        var camp = await service.CreateAsync(admin.Id, new("Community Camp", "Colombo", null, null, now.AddMinutes(30), now.AddMinutes(30).AddHours(2), 2, 30), default);
        await service.TransitionAsync(camp.Value!.Id, CampStatus.Scheduled, default);
        var booking = await service.BookAsync(camp.Value.Id, donorUser.Id, null, default);
        var duplicate = await service.BookAsync(camp.Value.Id, donorUser.Id, null, default);
        Assert.True(booking.Succeeded); Assert.Equal("already_booked", duplicate.ErrorCode);
        await service.TransitionAsync(camp.Value.Id, CampStatus.InProgress, default); await service.CheckInAsync(camp.Value.Id, booking.Value!.Id, default);
        var attendance = await service.AttendanceAsync(camp.Value.Id, default); Assert.Equal(1, attendance.Value!.CheckedIn); Assert.Equal(100, attendance.Value.AttendanceRate);
        Assert.Single(await db.CampSlots.Where(x => x.Status == CampSlotStatus.CheckedIn).ToListAsync());
    }

    [Fact]
    public async Task IneligibleDonorCannotBook()
    {
        var now = DateTimeOffset.UtcNow; var (service, db, admin, donorUser) = Create(now); var donor = await db.Donors.SingleAsync(); donor.ApplyEligibility(EligibilityStatus.TemporarilyIneligible); await db.SaveChangesAsync();
        var camp = await service.CreateAsync(admin.Id, new("Camp", "Kandy", null, null, now.AddDays(2), now.AddDays(2).AddHours(2), 2, 30), default); await service.TransitionAsync(camp.Value!.Id, CampStatus.Scheduled, default);
        var booking = await service.BookAsync(camp.Value.Id, donorUser.Id, null, default); Assert.Equal("donor_not_eligible", booking.ErrorCode);
    }

    [Theory]
    [InlineData(2, 1, "End time must be after the start time.")]
    [InlineData(-1, 2, "Start time must be in the future.")]
    public async Task CreateExplainsInvalidScheduleTimes(int startHours, int endHours, string message)
    {
        var now = DateTimeOffset.UtcNow; var (service, _, admin, _) = Create(now);
        var result = await service.CreateAsync(admin.Id, new("Camp", "Kandy", null, null, now.AddHours(startHours), now.AddHours(endHours), 2, 15), default);
        Assert.Equal("invalid_camp", result.ErrorCode);
        Assert.Equal(message, result.ErrorMessage);
    }

    private static (CampService Service, LifeLinkDbContext Db, User Admin, User DonorUser) Create(DateTimeOffset now)
    {
        var options = new DbContextOptionsBuilder<LifeLinkDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options; var db = new LifeLinkDbContext(options);
        var admin = new User("admin@example.com", "hash", UserRole.BloodBankAdmin); var donorUser = new User("donor@example.com", "hash", UserRole.Donor); var donor = new Donor(donorUser.Id, BloodType.OPositive, new DateOnly(1990, 1, 1), "Colombo", null, null, "[]"); donor.ApplyEligibility(EligibilityStatus.Eligible);
        db.Users.AddRange(admin, donorUser); db.Donors.Add(donor); db.SaveChanges(); return (new CampService(db, new FixedTimeProvider(now), new FakeNotifications()), db, admin, donorUser);
    }
    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider { public override DateTimeOffset GetUtcNow() => now; }
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
