using LifeLink.Application.Inventory;
using LifeLink.Domain.Entities;
using LifeLink.Domain.Enums;
using LifeLink.Infrastructure.Inventory;
using LifeLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LifeLink.Tests.Inventory;

public sealed class InventoryServiceTests
{
    [Fact]
    public void CompatibilityMatrixProtectsONegativeAndAllowsUniversalABPositive()
    {
        var (service, _, _, _) = Create();
        Assert.Equal([BloodType.ONegative], service.Compatibility(BloodType.ONegative).CompatibleDonorTypes);
        Assert.Equal(8, service.Compatibility(BloodType.ABPositive).CompatibleDonorTypes.Count);
    }

    [Fact]
    public async Task ReserveUsesFefoAcrossCompatibleLotsAndIsIdempotent()
    {
        var (service, db, request, _) = Create();
        var location = await service.CreateLocationAsync(new("Central", "Colombo", 6.9m, 79.8m), default);
        var early = await service.StockInAsync(new(location.Value!.Id, BloodType.OPositive, 2, new DateOnly(2026, 9, 1), "Donation"), default);
        var later = await service.StockInAsync(new(location.Value.Id, BloodType.APositive, 5, new DateOnly(2026, 10, 1), "Donation"), default);
        var first = await service.ReserveAsync(new(request.Id, 5, "reserve-1", 30), default);
        var replay = await service.ReserveAsync(new(request.Id, 5, "reserve-1", 30), default);
        Assert.True(first.Value!.FullyReserved);
        Assert.Equal(2, first.Value.Reservations.Count);
        Assert.Equal(early.Value!.Id, first.Value.Reservations[0].InventoryLotId);
        Assert.Equal(first.Value.Reservations.Select(x => x.Id), replay.Value!.Reservations.Select(x => x.Id));
        Assert.Equal(2, await db.InventoryReservations.CountAsync());
        Assert.Equal(2, (await db.InventoryLots.SingleAsync(x => x.Id == later.Value!.Id)).UnitsAvailable);
    }

    [Fact]
    public async Task ReleaseRestoresReservedUnits()
    {
        var (service, db, request, _) = Create();
        var location = await service.CreateLocationAsync(new("Central", "Colombo", 6.9m, 79.8m), default);
        var lot = await service.StockInAsync(new(location.Value!.Id, BloodType.ONegative, 3, new DateOnly(2026, 9, 1), "Donation"), default);
        var plan = await service.ReserveAsync(new(request.Id, 2, "reserve-2", 30), default);
        await service.ReleaseAsync(plan.Value!.Reservations.Single().Id, "Request cancelled", default);
        var storedLot = await db.InventoryLots.SingleAsync(x => x.Id == lot.Value!.Id);
        Assert.Equal(3, storedLot.UnitsAvailable);
        Assert.Equal(ReservationStatus.Released, (await db.InventoryReservations.SingleAsync()).Status);
    }

    [Fact]
    public async Task ManualDispatchTakesOutstandingUnitsOutOfStockOnce()
    {
        var (service, db, request, _) = Create();
        var admin = new User("admin@example.com", "hash", UserRole.BloodBankAdmin); db.Users.Add(admin); await db.SaveChangesAsync();
        var location = await service.CreateLocationAsync(new("Central", "Colombo", 6.9m, 79.8m), default);
        var early = await service.StockInAsync(new(location.Value!.Id, BloodType.ONegative, 3, new DateOnly(2026, 9, 1), "Donation"), default);
        var later = await service.StockInAsync(new(location.Value.Id, BloodType.APositive, 4, new DateOnly(2026, 10, 1), "Donation"), default);
        var first = await service.DispatchRequestAsync(request.Id, admin.Id, default);
        var replay = await service.DispatchRequestAsync(request.Id, admin.Id, default);
        Assert.Equal(5, first.Value);
        Assert.Equal(0, replay.Value);
        Assert.Equal(0, (await db.InventoryLots.SingleAsync(x => x.Id == early.Value!.Id)).UnitsAvailable);
        Assert.Equal(2, (await db.InventoryLots.SingleAsync(x => x.Id == later.Value!.Id)).UnitsAvailable);
        Assert.Equal(5, await db.DispatchRecords.SumAsync(x => x.UnitsDispatched));
    }

    [Fact]
    public async Task ManualDispatchRefusesWithoutEnoughStockAndChangesNothing()
    {
        var (service, db, request, _) = Create();
        var location = await service.CreateLocationAsync(new("Central", "Colombo", 6.9m, 79.8m), default);
        var lot = await service.StockInAsync(new(location.Value!.Id, BloodType.APositive, 2, new DateOnly(2026, 9, 1), "Donation"), default);
        var result = await service.DispatchRequestAsync(request.Id, Guid.NewGuid(), default);
        Assert.Equal("stock_unavailable", result.ErrorCode);
        Assert.Equal(2, (await db.InventoryLots.SingleAsync(x => x.Id == lot.Value!.Id)).UnitsAvailable);
        Assert.Empty(db.InventoryReservations);
    }

    [Fact]
    public async Task StockInRejectsExpiredBloodWithASpecificMessage()
    {
        var (service, _, _, _) = Create();
        var location = await service.CreateLocationAsync(new("Central", "Colombo", 6.9m, 79.8m), default);
        var result = await service.StockInAsync(new(location.Value!.Id, BloodType.APositive, 2, new DateOnly(2026, 8, 18), "Donation"), default);
        Assert.Equal("invalid_stock", result.ErrorCode);
        Assert.Contains("Expiry date", result.ErrorMessage);
    }

    private static (InventoryService Service, LifeLinkDbContext Db, BloodRequest Request, MutableTimeProvider Clock) Create()
    {
        var options = new DbContextOptionsBuilder<LifeLinkDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options; var db = new LifeLinkDbContext(options);
        var user = new User("staff@example.com", "hash", UserRole.HospitalRequester); var hospital = new Hospital("Hospital", "REG-1", "Colombo", null, null); hospital.SetVerification(VerificationStatus.Verified);
        var request = new BloodRequest(hospital.Id, user.Id, BloodType.APositive, 5, RequestUrgency.Urgent, null, new DateTimeOffset(2026, 8, 20, 0, 0, 0, TimeSpan.Zero));
        db.Users.Add(user); db.Hospitals.Add(hospital); db.BloodRequests.Add(request); db.SaveChanges(); var clock = new MutableTimeProvider(new DateTimeOffset(2026, 8, 18, 0, 0, 0, TimeSpan.Zero)); return (new InventoryService(db, clock), db, request, clock);
    }
    private sealed class MutableTimeProvider(DateTimeOffset now) : TimeProvider { public DateTimeOffset Now { get; set; } = now; public override DateTimeOffset GetUtcNow() => Now; }
}
