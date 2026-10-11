using LifeLink.Domain.Entities;
using LifeLink.Domain.Enums;
using LifeLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LifeLink.Tests.Persistence;

/// <summary>The island-wide demo data: every district has donors, hospitals, stock and an upcoming camp, added once.</summary>
public sealed class SriLankaDemoDataTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 11, 6, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task EveryDistrictGetsAtLeastFiveBookableDonorsAndTheRestOfItsDataOnce()
    {
        var db = new LifeLinkDbContext(new DbContextOptionsBuilder<LifeLinkDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.BloodBankLocations.Add(new BloodBankLocation("Regional Blood Centre Kandy", "William Gopallawa Mawatha, Kandy", 7.29m, 80.63m)); await db.SaveChangesAsync();

        Assert.True(await SriLankaDemoData.ApplyAsync(db, "hash", Now, default));
        Assert.False(await SriLankaDemoData.ApplyAsync(db, "hash", Now, default)); // second run is a no-op

        var districts = SriLankaDemoData.Districts;
        Assert.Equal(25, districts.Count);
        var donors = await db.Donors.ToListAsync();
        var users = await db.Users.ToDictionaryAsync(x => x.Id, x => x.Email);
        foreach (var district in districts)
        {
            var local = donors.Where(x => users[x.UserId].StartsWith($"donor.{district.Key}.")).ToList();
            Assert.True(local.Count >= 8, district.Name);
            Assert.True(local.Count(x => x.IsActive && x.EligibilityStatus == EligibilityStatus.Eligible) >= 5, district.Name);
            Assert.All(local, d => Assert.True(Math.Abs(d.Latitude!.Value - district.Lat) < 0.1m && Math.Abs(d.Longitude!.Value - district.Lon) < 0.1m, district.Name));
        }
        Assert.Equal(50, await db.Hospitals.CountAsync());
        Assert.Equal(50, await db.HospitalStaff.CountAsync());
        // Kandy already had a centre: it is not duplicated, every other district gets one.
        var locations = await db.BloodBankLocations.Select(x => x.Name.ToLower()).ToListAsync();
        Assert.Equal(locations.Count, locations.Distinct().Count());
        Assert.Equal(25, locations.Count);
        Assert.True(await db.InventoryLots.CountAsync() >= 24 * 6);

        var camps = await db.DonationCamps.ToListAsync();
        Assert.Equal(25, camps.Count);
        var lk = TimeSpan.FromHours(5.5);
        Assert.All(camps, c => { Assert.Equal(CampStatus.Scheduled, c.Status); Assert.True(c.StartsAtUtc > Now); Assert.Equal(9, c.StartsAtUtc.ToOffset(lk).Hour); Assert.Equal(15, c.EndsAtUtc.ToOffset(lk).Hour); });
        Assert.True(await db.CampSlots.CountAsync(x => x.Status == CampSlotStatus.Booked) >= 25 * 3);
        Assert.Equal(25, await db.BloodRequests.CountAsync());
    }
}
