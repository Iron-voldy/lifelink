using LifeLink.Domain.Entities;
using LifeLink.Domain.Enums;
using LifeLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LifeLink.Tests.Persistence;

/// <summary>Updates applied to an already-seeded demo database: donor20 becomes a bookable donor with upcoming bookings.</summary>
public sealed class DemoDataUpdatesTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 11, 6, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Donor20BecomesEligibleAndIsBookedIntoUpcomingCampsOnce()
    {
        var db = new LifeLinkDbContext(new DbContextOptionsBuilder<LifeLinkDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var user = new User($"donor20@{DemoDataSeeder.EmailDomain}", "hash", UserRole.Donor); var organizer = new User("c@example.com", "hash", UserRole.CampCoordinator);
        var donor = new Donor(user.Id, BloodType.OPositive, new DateOnly(1961, 3, 14), "67, Kamburugamuwa, Matara", 5.967m, 80.529m, "[]");
        donor.ApplyEligibility(EligibilityStatus.PermanentlyIneligible); donor.Deactivate();
        db.Users.AddRange(user, organizer); db.Donors.Add(donor);
        var past = new DonationCamp(organizer.Id, "Past", "Colombo", null, null, Now.AddDays(-3), Now.AddDays(-3).AddHours(2), 2);
        var soon = new DonationCamp(organizer.Id, "Soon", "Badulla", 6.98m, 81.06m, Now.AddDays(2), Now.AddDays(2).AddHours(2), 2);
        var later = new DonationCamp(organizer.Id, "Later", "Batticaloa", 7.71m, 81.70m, Now.AddDays(9), Now.AddDays(9).AddHours(2), 2);
        foreach (var camp in new[] { past, soon, later }) { camp.TransitionTo(CampStatus.Scheduled); db.DonationCamps.Add(camp); db.CampSlots.AddRange(new CampSlot(camp.Id, camp.StartsAtUtc), new CampSlot(camp.Id, camp.StartsAtUtc.AddMinutes(30))); }
        await db.SaveChangesAsync();

        await DemoDataSeeder.ApplyDemoUpdatesAsync(db, Now, default);
        await DemoDataSeeder.ApplyDemoUpdatesAsync(db, Now, default);

        var updated = await db.Donors.SingleAsync();
        Assert.True(updated.IsActive);
        Assert.Equal(EligibilityStatus.Eligible, updated.EligibilityStatus);
        Assert.True(updated.DateOfBirth.Year > 1970);
        var booked = await db.CampSlots.Where(x => x.DonorId == donor.Id && x.Status == CampSlotStatus.Booked).Select(x => x.CampId).ToListAsync();
        Assert.Equal(new[] { soon.Id, later.Id }.OrderBy(x => x), booked.OrderBy(x => x));
        Assert.Single(await db.DonorEligibilityHistory.ToListAsync());
    }
}
