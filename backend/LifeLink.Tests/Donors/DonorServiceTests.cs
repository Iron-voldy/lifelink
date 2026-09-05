using LifeLink.Application.Donors;
using LifeLink.Domain.Entities;
using LifeLink.Domain.Enums;
using LifeLink.Infrastructure.Donors;
using LifeLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LifeLink.Tests.Donors;

public sealed class DonorServiceTests
{
    private static async Task<(DonorService Service, LifeLinkDbContext Db, User User)> CreateAsync(DateTimeOffset now)
    {
        var options = new DbContextOptionsBuilder<LifeLinkDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var db = new LifeLinkDbContext(options);
        var user = new User("donor@example.com", "hash", UserRole.Donor); db.Users.Add(user); await db.SaveChangesAsync();
        return (new DonorService(db, new FixedTimeProvider(now)), db, user);
    }

    [Fact]
    public async Task EvaluateEligibility_EligibleAdultWithoutFlagsBecomesEligible()
    {
        var (service, db, user) = await CreateAsync(new DateTimeOffset(2026, 8, 18, 0, 0, 0, TimeSpan.Zero));
        var created = await service.CreateAsync(user.Id, new(BloodType.OPositive, new DateOnly(1995, 1, 1), "Colombo", 6.9271m, 79.8612m, []), default);
        var result = await service.EvaluateEligibilityAsync(created.Value!.Id, user.Id, true, default);
        Assert.Equal(EligibilityStatus.Eligible, result.Value!.Status);
        Assert.Empty(result.Value.Reasons);
        Assert.Single(await db.DonorEligibilityHistory.ToListAsync());
    }

    [Fact]
    public async Task RecordDonationEnforcesCooldownOnNextEvaluation()
    {
        var (service, _, user) = await CreateAsync(new DateTimeOffset(2026, 8, 18, 0, 0, 0, TimeSpan.Zero));
        var donor = await service.CreateAsync(user.Id, new(BloodType.APositive, new DateOnly(1990, 1, 1), "Kandy", null, null, []), default);
        await service.RecordDonationAsync(donor.Value!.Id, new(new DateOnly(2026, 8, 1), 1, "Kandy Blood Bank", null), default);
        var evaluation = await service.EvaluateEligibilityAsync(donor.Value.Id, user.Id, true, default);
        Assert.Equal(EligibilityStatus.TemporarilyIneligible, evaluation.Value!.Status);
        Assert.Contains(evaluation.Value.Reasons, x => x.Contains("90-day"));
    }

    [Fact]
    public async Task CreateRejectsSecondProfileForSameUser()
    {
        var (service, _, user) = await CreateAsync(DateTimeOffset.UtcNow);
        var input = new DonorProfileInput(BloodType.BNegative, new DateOnly(1999, 1, 1), "Galle", null, null, []);
        await service.CreateAsync(user.Id, input, default);
        var duplicate = await service.CreateAsync(user.Id, input, default);
        Assert.Equal("profile_exists", duplicate.ErrorCode);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
