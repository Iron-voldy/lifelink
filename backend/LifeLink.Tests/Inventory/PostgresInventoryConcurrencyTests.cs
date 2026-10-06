using System.Data.Common;
using LifeLink.Domain.Entities;
using LifeLink.Domain.Enums;
using LifeLink.Infrastructure.Inventory;
using LifeLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace LifeLink.Tests.Inventory;

public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("LIFELINK_TEST_POSTGRES")))
            Skip = "Set LIFELINK_TEST_POSTGRES to a disposable PostgreSQL test database connection string.";
    }
}

public sealed class PostgresInventoryConcurrencyTests
{
    [PostgresFact]
    public async Task ConcurrentReservationsForSameLotCannotOverAllocate()
    {
        var connectionString = GetConnectionString();
        var fixture = await SeedAsync(connectionString, requestCount: 2);
        var barrier = new InventoryLotReadBarrier();
        var options = Options(connectionString, barrier);
        await using var firstDb = new LifeLinkDbContext(options);
        await using var secondDb = new LifeLinkDbContext(options);
        var firstService = new InventoryService(firstDb, TimeProvider.System);
        var secondService = new InventoryService(secondDb, TimeProvider.System);

        var results = await Task.WhenAll(
            firstService.ReserveAsync(new(fixture.RequestIds[0], 1, $"reserve-a-{Guid.NewGuid():N}", 30), default),
            secondService.ReserveAsync(new(fixture.RequestIds[1], 1, $"reserve-b-{Guid.NewGuid():N}", 30), default));

        Assert.Single(results.Where(x => x.Succeeded));
        Assert.Single(results.Where(x => x.ErrorCode == "concurrency_conflict"));

        await using var verifyDb = new LifeLinkDbContext(Options(connectionString));
        var lot = await verifyDb.InventoryLots.SingleAsync(x => x.Id == fixture.LotId);
        var reservations = await verifyDb.InventoryReservations.Where(x => x.InventoryLotId == fixture.LotId).ToListAsync();
        Assert.Equal(1, lot.UnitsReceived);
        Assert.Equal(0, lot.UnitsAvailable);
        Assert.InRange(lot.UnitsAvailable, 0, lot.UnitsReceived);
        Assert.Single(reservations);
        Assert.Equal(1, reservations.Sum(x => x.Units));
    }

    [PostgresFact]
    public async Task ConcurrentDispatchesForSameLotCannotOverAllocateOrDuplicateRecords()
    {
        var connectionString = GetConnectionString();
        var fixture = await SeedAsync(connectionString, requestCount: 2);
        var barrier = new InventoryLotReadBarrier();
        var options = Options(connectionString, barrier);
        await using var firstDb = new LifeLinkDbContext(options);
        await using var secondDb = new LifeLinkDbContext(options);
        var firstService = new InventoryService(firstDb, TimeProvider.System);
        var secondService = new InventoryService(secondDb, TimeProvider.System);

        var results = await Task.WhenAll(
            firstService.DispatchRequestAsync(fixture.RequestIds[0], fixture.AdminId, default),
            secondService.DispatchRequestAsync(fixture.RequestIds[1], fixture.AdminId, default));

        Assert.Single(results.Where(x => x.Succeeded));
        Assert.Single(results.Where(x => x.ErrorCode == "concurrency_conflict"));

        await using var verifyDb = new LifeLinkDbContext(Options(connectionString));
        var lot = await verifyDb.InventoryLots.SingleAsync(x => x.Id == fixture.LotId);
        var reservations = await verifyDb.InventoryReservations.Where(x => x.InventoryLotId == fixture.LotId).ToListAsync();
        var reservationIds = reservations.Select(x => x.Id).ToArray();
        var dispatches = await verifyDb.DispatchRecords.Where(x => reservationIds.Contains(x.InventoryReservationId)).ToListAsync();
        Assert.Equal(1, lot.UnitsReceived);
        Assert.Equal(0, lot.UnitsAvailable);
        Assert.InRange(lot.UnitsAvailable, 0, lot.UnitsReceived);
        Assert.Single(reservations);
        Assert.All(reservations, x => Assert.Equal(ReservationStatus.Dispatched, x.Status));
        Assert.Single(dispatches);
        Assert.Equal(1, dispatches.Sum(x => x.UnitsDispatched));
        Assert.Equal(reservations.Single().Id, dispatches.Single().InventoryReservationId);
    }

    private static string GetConnectionString() =>
        Environment.GetEnvironmentVariable("LIFELINK_TEST_POSTGRES")
        ?? throw new InvalidOperationException("LIFELINK_TEST_POSTGRES is required.");

    private static DbContextOptions<LifeLinkDbContext> Options(
        string connectionString,
        DbCommandInterceptor? interceptor = null)
    {
        var builder = new DbContextOptionsBuilder<LifeLinkDbContext>().UseNpgsql(connectionString);
        if (interceptor is not null) builder.AddInterceptors(interceptor);
        return builder.Options;
    }

    private static async Task<SeededInventory> SeedAsync(string connectionString, int requestCount)
    {
        await using var db = new LifeLinkDbContext(Options(connectionString));
        await db.Database.MigrateAsync();

        var suffix = Guid.NewGuid().ToString("N");
        var admin = new User($"admin-{suffix}@example.test", "test-hash", UserRole.BloodBankAdmin);
        var requester = new User($"requester-{suffix}@example.test", "test-hash", UserRole.HospitalRequester);
        var hospital = new Hospital($"Hospital {suffix}", $"PG-{suffix}", "Colombo", null, null);
        hospital.SetVerification(VerificationStatus.Verified);
        var location = new BloodBankLocation($"Location {suffix}", "Colombo", 6.9m, 79.8m);
        var now = DateTimeOffset.UtcNow;
        var requests = Enumerable.Range(0, requestCount)
            .Select(_ => new BloodRequest(
                hospital.Id,
                requester.Id,
                BloodType.APositive,
                1,
                RequestUrgency.Urgent,
                null,
                now.AddHours(2)))
            .ToList();
        var lot = new InventoryLot(
            location.Id,
            BloodType.APositive,
            1,
            DateOnly.FromDateTime(now.UtcDateTime).AddDays(30),
            "PostgreSQL concurrency test");

        db.Users.AddRange(admin, requester);
        db.Hospitals.Add(hospital);
        db.BloodRequests.AddRange(requests);
        db.BloodBankLocations.Add(location);
        db.InventoryLots.Add(lot);
        await db.SaveChangesAsync();
        return new SeededInventory(admin.Id, lot.Id, requests.Select(x => x.Id).ToArray());
    }

    private sealed record SeededInventory(Guid AdminId, Guid LotId, Guid[] RequestIds);

    private sealed class InventoryLotReadBarrier : DbCommandInterceptor
    {
        private readonly TaskCompletionSource _bothReadsCompleted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _lotReads;

        public override async ValueTask<DbDataReader> ReaderExecutedAsync(
            DbCommand command,
            CommandExecutedEventData eventData,
            DbDataReader result,
            CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("inventory_lots", StringComparison.OrdinalIgnoreCase))
            {
                var readNumber = Interlocked.Increment(ref _lotReads);
                if (readNumber <= 2)
                {
                    if (readNumber == 2) _bothReadsCompleted.TrySetResult();
                    await _bothReadsCompleted.Task.WaitAsync(cancellationToken);
                }
            }

            return result;
        }
    }
}
