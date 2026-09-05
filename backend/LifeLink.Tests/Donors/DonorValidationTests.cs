using LifeLink.Application.Camps;
using LifeLink.Application.Donors;
using LifeLink.Application.Notifications;
using LifeLink.Domain.Entities;
using LifeLink.Domain.Enums;
using LifeLink.Infrastructure.Camps;
using LifeLink.Infrastructure.Donors;
using LifeLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LifeLink.Tests.Donors;

public sealed class DonorValidationTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 4, 6, 0, 0, TimeSpan.Zero);

    private static async Task<(DonorService Service, LifeLinkDbContext Db, User User)> CreateAsync()
    {
        var db = new LifeLinkDbContext(new DbContextOptionsBuilder<LifeLinkDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var user = new User("donor@example.com", "hash", UserRole.Donor); db.Users.Add(user); await db.SaveChangesAsync();
        return (new DonorService(db, new FixedTimeProvider(Now)), db, user);
    }

    private static DonorProfileInput Profile(DateOnly dob, params string[] flags) => new(BloodType.OPositive, dob, "Colombo", null, null, flags);

    [Theory]
    [InlineData("2026-10-05", "Date of birth cannot be in the future.")]
    [InlineData("2016-01-01", "Donors must be at least 18 years old.")]
    [InlineData("2008-10-05", "Donors must be at least 18 years old.")]
    [InlineData("0001-01-01", "Date of birth cannot be more than 120 years ago.")]
    [InlineData("1850-01-01", "Date of birth cannot be more than 120 years ago.")]
    public async Task CreateRejectsImplausibleDatesOfBirth(string dob, string message)
    {
        var (service, _, user) = await CreateAsync();
        var result = await service.CreateAsync(user.Id, Profile(DateOnly.Parse(dob)), default);
        Assert.Equal("invalid_profile", result.ErrorCode);
        Assert.Equal(message, result.ErrorMessage);
    }

    [Fact]
    public async Task CreateAcceptsDonorWhoTurns18Today()
    {
        var (service, _, user) = await CreateAsync();
        Assert.True((await service.CreateAsync(user.Id, Profile(new DateOnly(2008, 10, 4)), default)).Succeeded);
    }

    [Fact]
    public async Task UpdateRejectsUnderageDateOfBirth()
    {
        var (service, _, user) = await CreateAsync();
        var created = await service.CreateAsync(user.Id, Profile(new DateOnly(1990, 1, 1)), default);
        var result = await service.UpdateAsync(created.Value!.Id, Profile(new DateOnly(2016, 1, 1)), default);
        Assert.Equal("invalid_profile", result.ErrorCode);
    }

    [Fact]
    public async Task AddingMedicalNoteToEligibleDonorRequiresRecheck()
    {
        var (service, db, user) = await CreateAsync();
        var created = await service.CreateAsync(user.Id, Profile(new DateOnly(1990, 1, 1)), default);
        await service.EvaluateEligibilityAsync(created.Value!.Id, user.Id, true, default);
        var updated = await service.UpdateAsync(created.Value.Id, Profile(new DateOnly(1990, 1, 1), "On antibiotics"), default);
        Assert.Equal(EligibilityStatus.PendingVerification, updated.Value!.EligibilityStatus);
        Assert.Equal(2, await db.DonorEligibilityHistory.CountAsync());
    }

    [Fact]
    public async Task AddressOnlyChangeKeepsEligibility()
    {
        var (service, _, user) = await CreateAsync();
        var created = await service.CreateAsync(user.Id, Profile(new DateOnly(1990, 1, 1)), default);
        await service.EvaluateEligibilityAsync(created.Value!.Id, user.Id, true, default);
        var updated = await service.UpdateAsync(created.Value.Id, new(BloodType.OPositive, new DateOnly(1990, 1, 1), "Kandy", 7.29m, 80.63m, []), default);
        Assert.Equal(EligibilityStatus.Eligible, updated.Value!.EligibilityStatus);
    }

    [Fact]
    public async Task ChangingBloodTypeOfEligibleDonorRequiresStaffReverification()
    {
        var (service, _, user) = await CreateAsync();
        var created = await service.CreateAsync(user.Id, Profile(new DateOnly(1990, 1, 1)), default);
        await service.EvaluateEligibilityAsync(created.Value!.Id, user.Id, true, default);
        var updated = await service.UpdateAsync(created.Value.Id, new(BloodType.ONegative, new DateOnly(1990, 1, 1), "Colombo", null, null, []), default);
        Assert.Equal(EligibilityStatus.PendingVerification, updated.Value!.EligibilityStatus);
        var selfCheck = await service.EvaluateEligibilityAsync(created.Value.Id, user.Id, false, default);
        Assert.Equal(EligibilityStatus.PendingVerification, selfCheck.Value!.Status);
    }

    [Fact]
    public async Task DonorSelfCheckCanTightenButNeverGrantEligibility()
    {
        var (service, _, user) = await CreateAsync();
        var created = await service.CreateAsync(user.Id, Profile(new DateOnly(1990, 1, 1)), default);
        var selfCheck = await service.EvaluateEligibilityAsync(created.Value!.Id, user.Id, false, default);
        Assert.Equal(EligibilityStatus.PendingVerification, selfCheck.Value!.Status);
        Assert.Contains(selfCheck.Value.Reasons, x => x.Contains("staff must verify"));
        Assert.Equal(EligibilityStatus.Eligible, (await service.EvaluateEligibilityAsync(created.Value.Id, Guid.NewGuid(), true, default)).Value!.Status);
        Assert.Equal(EligibilityStatus.Eligible, (await service.EvaluateEligibilityAsync(created.Value.Id, user.Id, false, default)).Value!.Status);
        await service.UpdateAsync(created.Value.Id, Profile(new DateOnly(1990, 1, 1), "Recent tattoo"), default);
        var stricter = await service.EvaluateEligibilityAsync(created.Value.Id, user.Id, false, default);
        Assert.Equal(EligibilityStatus.TemporarilyIneligible, stricter.Value!.Status);
    }

    [Fact]
    public async Task EvaluatorNeverLiftsPermanentIneligibility()
    {
        var (service, _, user) = await CreateAsync();
        var created = await service.CreateAsync(user.Id, Profile(new DateOnly(1950, 1, 1)), default);
        Assert.Equal(EligibilityStatus.PermanentlyIneligible, (await service.EvaluateEligibilityAsync(created.Value!.Id, user.Id, false, default)).Value!.Status);
        await service.UpdateAsync(created.Value.Id, Profile(new DateOnly(1995, 1, 1)), default);
        Assert.Equal(EligibilityStatus.PermanentlyIneligible, (await service.GetAsync(created.Value.Id, default)).Value!.EligibilityStatus);
        Assert.Equal(EligibilityStatus.PermanentlyIneligible, (await service.EvaluateEligibilityAsync(created.Value.Id, user.Id, false, default)).Value!.Status);
        Assert.Equal(EligibilityStatus.PermanentlyIneligible, (await service.EvaluateEligibilityAsync(created.Value.Id, Guid.NewGuid(), true, default)).Value!.Status);
    }

    [Fact]
    public async Task BackfilledOldDonationDoesNotShortenCooldownOrBlockEligibleDonor()
    {
        var (service, _, user) = await CreateAsync();
        var donor = (await service.CreateAsync(user.Id, Profile(new DateOnly(1990, 1, 1)), default)).Value!;
        await service.RecordDonationAsync(donor.Id, new(new DateOnly(2026, 9, 20), 1, "Kandy", null), default);
        await service.RecordDonationAsync(donor.Id, new(new DateOnly(2025, 1, 10), 1, "Kandy", null), default);
        var view = (await service.GetAsync(donor.Id, default)).Value!;
        Assert.Equal(new DateOnly(2026, 9, 20), view.LastDonationDate);

        var (fresh, _, freshUser) = await CreateAsync();
        var other = (await fresh.CreateAsync(freshUser.Id, Profile(new DateOnly(1990, 1, 1)), default)).Value!;
        await fresh.EvaluateEligibilityAsync(other.Id, freshUser.Id, true, default);
        await fresh.RecordDonationAsync(other.Id, new(new DateOnly(2024, 1, 10), 1, "Kandy", null), default);
        Assert.Equal(EligibilityStatus.Eligible, (await fresh.GetAsync(other.Id, default)).Value!.EligibilityStatus);
    }

    [Fact]
    public async Task RecordDonationRejectsDuplicateDayAndDatesBeforeDonorCouldDonate()
    {
        var (service, _, user) = await CreateAsync();
        var donor = (await service.CreateAsync(user.Id, Profile(new DateOnly(1990, 1, 1)), default)).Value!;
        Assert.True((await service.RecordDonationAsync(donor.Id, new(new DateOnly(2026, 9, 1), 1, "Kandy", null), default)).Succeeded);
        Assert.Equal("donation_exists", (await service.RecordDonationAsync(donor.Id, new(new DateOnly(2026, 9, 1), 1, "Kandy", null), default)).ErrorCode);
        Assert.Equal("invalid_donation", (await service.RecordDonationAsync(donor.Id, new(new DateOnly(1995, 1, 1), 1, "Kandy", null), default)).ErrorCode);
        Assert.Equal("invalid_donation", (await service.RecordDonationAsync(donor.Id, new(new DateOnly(2026, 9, 2), 1, "Kandy", new string('n', 1001)), default)).ErrorCode);
    }

    [Fact]
    public async Task DonorCannotCancelBookingOnceCampHasStarted()
    {
        var (camps, db, campId, donorUser, _) = await CampWithBookingDonorAsync();
        var booking = await camps.BookAsync(campId, donorUser.Id, null, default);
        Assert.True(booking.Succeeded);
        Clock.Now = Now.AddDays(2).AddMinutes(1); // start time passed, camp not yet moved to InProgress
        var cancel = await camps.CancelBookingAsync(campId, booking.Value!.Id, donorUser.Id, default);
        Assert.Equal("invalid_slot_state", cancel.ErrorCode);
        Assert.Equal(CampSlotStatus.Booked, (await db.CampSlots.SingleAsync(x => x.Id == booking.Value.Id)).Status);
        Assert.True((await camps.TransitionAsync(campId, CampStatus.InProgress, default)).Succeeded);
        Assert.Equal("invalid_slot_state", (await camps.CancelBookingAsync(campId, booking.Value.Id, donorUser.Id, default)).ErrorCode);
    }

    [Fact]
    public async Task RebookingAfterCancelQueuesANewConfirmation()
    {
        var (camps, _, campId, donorUser, sent) = await CampWithBookingDonorAsync();
        var first = await camps.BookAsync(campId, donorUser.Id, null, default);
        Assert.True((await camps.CancelBookingAsync(campId, first.Value!.Id, donorUser.Id, default)).Succeeded);
        Assert.Equal("invalid_slot_state", (await camps.CancelBookingAsync(campId, first.Value.Id, donorUser.Id, default)).ErrorCode);
        var second = await camps.BookAsync(campId, donorUser.Id, null, default);
        Assert.True(second.Succeeded);
        Assert.Equal(2, sent.Keys.Distinct().Count());
    }


    private readonly FixedTimeProvider Clock = new(Now);

    private async Task<(CampService Camps, LifeLinkDbContext Db, Guid CampId, User DonorUser, RecordingNotifications Sent)> CampWithBookingDonorAsync()
    {
        var db = new LifeLinkDbContext(new DbContextOptionsBuilder<LifeLinkDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var admin = new User("admin@example.com", "hash", UserRole.BloodBankAdmin); var donorUser = new User("donor@example.com", "hash", UserRole.Donor);
        var donor = new Donor(donorUser.Id, BloodType.OPositive, new DateOnly(1990, 1, 1), "Colombo", null, null, "[]"); donor.ApplyEligibility(EligibilityStatus.Eligible);
        db.Users.AddRange(admin, donorUser); db.Donors.Add(donor); await db.SaveChangesAsync();
        var sent = new RecordingNotifications();
        var camps = new CampService(db, Clock, sent);
        var camp = await camps.CreateAsync(admin.Id, new("Camp", "Kandy", null, null, Now.AddDays(2), Now.AddDays(2).AddHours(2), 2, 30), default);
        await camps.TransitionAsync(camp.Value!.Id, CampStatus.Scheduled, default);
        return (camps, db, camp.Value.Id, donorUser, sent);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider { public DateTimeOffset Now { get; set; } = now; public override DateTimeOffset GetUtcNow() => Now; }

    private sealed class RecordingNotifications : INotificationService
    {
        public List<string> Keys { get; } = [];
        public Task<NotificationResult<Guid>> QueueUserNotificationAsync(Guid userId, string type, IReadOnlyDictionary<string, string> data, string idempotencyKey, CancellationToken ct) { Keys.Add(idempotencyKey); return Task.FromResult(NotificationResult<Guid>.Success(Guid.NewGuid())); }
        public Task<NotificationResult<bool>> RegisterDeviceAsync(Guid userId, DeviceTokenInput input, CancellationToken ct) => throw new NotSupportedException();
        public Task<NotificationResult<bool>> RemoveDeviceAsync(Guid userId, string token, CancellationToken ct) => throw new NotSupportedException();
        public Task<NotificationResult<BroadcastResult>> QueueApprovedBroadcastAsync(BroadcastInput input, CancellationToken ct) => throw new NotSupportedException();
        public Task<int> ProcessOutboxAsync(int batchSize, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<NotificationView>> HistoryAsync(Guid userId, CancellationToken ct) => throw new NotSupportedException();
    }
}
