using LifeLink.Domain.Entities;
using LifeLink.Domain.Enums;
using LifeLink.Infrastructure.Inventory;
using LifeLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LifeLink.Tests.Inventory;

/// <summary>Admin-side inventory rules: adjustments that would resurrect or double-count stock, expiry bounds and report numbers.</summary>
public sealed class InventoryAdminValidationTests
{
    private static readonly DateOnly Today = new(2026, 8, 18);

    [Fact]
    public async Task AdjustCannotReleaseAQuarantinedLotBackIntoStock()
    {
        var (service, db, _, locationId, _) = await CreateAsync();
        var lot = (await service.StockInAsync(new(locationId, BloodType.APositive, 4, Today.AddDays(10), "Donation"), default)).Value!;
        var quarantined = (await service.QuarantineAsync(lot.Id, default)).Value!;
        var result = await service.AdjustAsync(lot.Id, 3, quarantined.Version, default);
        Assert.Equal("invalid_lot_state", result.ErrorCode);
        Assert.Equal(InventoryLotStatus.Quarantined, (await db.InventoryLots.SingleAsync()).Status);
    }

    [Fact]
    public async Task AdjustCannotCountReservedOrDispatchedUnitsAgain()
    {
        var (service, db, request, locationId, _) = await CreateAsync();
        var lot = (await service.StockInAsync(new(locationId, BloodType.APositive, 5, Today.AddDays(10), "Donation"), default)).Value!;
        var plan = (await service.ReserveAsync(new(request.Id, 2, "hold-1", 30), default)).Value!;

        var tooMany = await service.AdjustAsync(lot.Id, 5, 0, default);
        Assert.Equal("invalid_adjustment", tooMany.ErrorCode);
        Assert.Contains("At most 3", tooMany.ErrorMessage);

        Assert.True((await service.AdjustAsync(lot.Id, 1, 0, default)).Succeeded);
        Assert.True((await service.ReleaseAsync(plan.Reservations.Single().Id, "cancelled", default)).Succeeded);
        Assert.Equal(3, (await db.InventoryLots.SingleAsync()).UnitsAvailable);
    }

    [Fact]
    public async Task StockInRejectsExpiryMoreThanAYearAhead()
    {
        var (service, _, _, locationId, _) = await CreateAsync();
        var result = await service.StockInAsync(new(locationId, BloodType.APositive, 2, Today.AddYears(10), "Donation"), default);
        Assert.Equal("invalid_stock", result.ErrorCode);
        Assert.Contains("check the year", result.ErrorMessage);
        Assert.True((await service.StockInAsync(new(locationId, BloodType.APositive, 2, Today.AddDays(InventoryService.MaxShelfLifeDays), "Donation"), default)).Succeeded);
    }

    [Fact]
    public async Task DispatchRefusesAHoldWhoseLotExpiredMeanwhile()
    {
        var (service, _, request, locationId, clock) = await CreateAsync();
        await service.StockInAsync(new(locationId, BloodType.APositive, 2, Today.AddDays(1), "Donation"), default);
        clock.Now = new DateTimeOffset(2026, 8, 19, 23, 50, 0, TimeSpan.Zero);
        var plan = (await service.ReserveAsync(new(request.Id, 1, "hold-2", 60), default)).Value!;
        clock.Now = clock.Now.AddMinutes(20);
        var dispatched = await service.DispatchAsync(plan.Reservations.Single().Id, Guid.NewGuid(), default);
        Assert.Equal("invalid_reservation_state", dispatched.ErrorCode);
    }

    [Fact]
    public async Task StockReportIgnoresEmptyLotsInCounts()
    {
        var (service, _, _, locationId, _) = await CreateAsync();
        var empty = (await service.StockInAsync(new(locationId, BloodType.APositive, 2, Today.AddDays(3), "Donation"), default)).Value!;
        await service.AdjustAsync(empty.Id, 0, 0, default);
        await service.StockInAsync(new(locationId, BloodType.APositive, 4, Today.AddDays(30), "Donation"), default);
        var row = Assert.Single(await service.StockReportAsync(default));
        Assert.Equal(4, row.AvailableUnits);
        Assert.Equal(1, row.LotCount);
        Assert.Equal(0, row.ExpiringWithinSevenDays);
    }

    [Fact]
    public async Task NewReservationReclaimsUnitsFromLapsedHolds()
    {
        var (service, db, request, locationId, clock) = await CreateAsync();
        await service.StockInAsync(new(locationId, BloodType.APositive, 2, Today.AddDays(10), "Donation"), default);
        Assert.True((await service.ReserveAsync(new(request.Id, 2, "hold-a", 5), default)).Value!.FullyReserved);
        clock.Now = clock.Now.AddMinutes(10);
        var second = await service.ReserveAsync(new(request.Id, 2, "hold-b", 30), default);
        Assert.True(second.Value!.FullyReserved);
        Assert.Equal(ReservationStatus.Expired, (await db.InventoryReservations.SingleAsync(x => x.IdempotencyKey.StartsWith("hold-a:"))).Status);
    }

    [Fact]
    public async Task ReserveRejectsKeysThatWouldOverflowTheStoredColumn()
    {
        var (service, _, request, locationId, _) = await CreateAsync();
        await service.StockInAsync(new(locationId, BloodType.APositive, 2, Today.AddDays(10), "Donation"), default);
        Assert.Equal("invalid_reservation", (await service.ReserveAsync(new(request.Id, 1, new string('k', InventoryService.MaxIdempotencyKeyLength + 1), 30), default)).ErrorCode);
        Assert.True((await service.ReserveAsync(new(request.Id, 1, new string('k', InventoryService.MaxIdempotencyKeyLength), 30), default)).Succeeded);
    }

    private static async Task<(InventoryService Service, LifeLinkDbContext Db, BloodRequest Request, Guid LocationId, MutableTimeProvider Clock)> CreateAsync()
    {
        var options = new DbContextOptionsBuilder<LifeLinkDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options; var db = new LifeLinkDbContext(options);
        var user = new User("staff@example.com", "hash", UserRole.HospitalRequester); var hospital = new Hospital("Hospital", "REG-1", "Colombo", null, null); hospital.SetVerification(VerificationStatus.Verified);
        var request = new BloodRequest(hospital.Id, user.Id, BloodType.APositive, 5, RequestUrgency.Urgent, null, new DateTimeOffset(2026, 8, 20, 0, 0, 0, TimeSpan.Zero));
        db.Users.Add(user); db.Hospitals.Add(hospital); db.BloodRequests.Add(request); db.SaveChanges();
        var clock = new MutableTimeProvider(new DateTimeOffset(2026, 8, 18, 0, 0, 0, TimeSpan.Zero)); var service = new InventoryService(db, clock);
        var location = await service.CreateLocationAsync(new("Central", "Colombo", 6.9m, 79.8m), default);
        return (service, db, request, location.Value!.Id, clock);
    }
    private sealed class MutableTimeProvider(DateTimeOffset now) : TimeProvider { public DateTimeOffset Now { get; set; } = now; public override DateTimeOffset GetUtcNow() => Now; }
}
