using LifeLink.Application.Donors;
using LifeLink.Application.Inventory;
using LifeLink.Domain.Entities;
using LifeLink.Domain.Enums;
using LifeLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LifeLink.Infrastructure.Inventory;

public sealed class InventoryService(LifeLinkDbContext db, TimeProvider timeProvider) : IInventoryService
{
    private static readonly IReadOnlyDictionary<BloodType, BloodType[]> CompatibilityMatrix = new Dictionary<BloodType, BloodType[]>
    {
        [BloodType.ONegative] = [BloodType.ONegative],
        [BloodType.OPositive] = [BloodType.ONegative, BloodType.OPositive],
        [BloodType.ANegative] = [BloodType.ONegative, BloodType.ANegative],
        [BloodType.APositive] = [BloodType.ONegative, BloodType.OPositive, BloodType.ANegative, BloodType.APositive],
        [BloodType.BNegative] = [BloodType.ONegative, BloodType.BNegative],
        [BloodType.BPositive] = [BloodType.ONegative, BloodType.OPositive, BloodType.BNegative, BloodType.BPositive],
        [BloodType.ABNegative] = [BloodType.ONegative, BloodType.ANegative, BloodType.BNegative, BloodType.ABNegative],
        [BloodType.ABPositive] = Enum.GetValues<BloodType>()
    };

    /// <summary>Longest storage life accepted at stock-in (frozen plasma, one year); anything later is almost certainly a mistyped year.</summary>
    public const int MaxShelfLifeDays = 366;
    /// <summary>The stored key gets a ':' plus a 32-character lot id appended and the column holds 100 characters.</summary>
    public const int MaxIdempotencyKeyLength = 64;

    public async Task<InventoryResult<LocationView>> CreateLocationAsync(LocationInput input, CancellationToken ct)
    {
        var validation = ValidateLocation(input); if (validation is not null) return InventoryResult<LocationView>.Failure("invalid_location", validation);
        var location = new BloodBankLocation(input.Name, input.Address, input.Latitude, input.Longitude); db.BloodBankLocations.Add(location); await db.SaveChangesAsync(ct); return InventoryResult<LocationView>.Success(Map(location));
    }
    public async Task<InventoryResult<LocationView>> UpdateLocationAsync(Guid id, LocationInput input, CancellationToken ct)
    {
        var location = await db.BloodBankLocations.SingleOrDefaultAsync(x => x.Id == id, ct); if (location is null) return InventoryResult<LocationView>.Failure("location_not_found", "Blood-bank location was not found.");
        var validation = ValidateLocation(input); if (validation is not null) return InventoryResult<LocationView>.Failure("invalid_location", validation);
        location.Update(input.Name, input.Address, input.Latitude, input.Longitude); await db.SaveChangesAsync(ct); return InventoryResult<LocationView>.Success(Map(location));
    }
    public async Task<IReadOnlyList<LocationView>> ListLocationsAsync(CancellationToken ct) => (await db.BloodBankLocations.AsNoTracking().OrderBy(x => x.Name).ToListAsync(ct)).Select(Map).ToList();

    public async Task<InventoryResult<InventoryLotView>> StockInAsync(StockInInput input, CancellationToken ct)
    {
        if (!await db.BloodBankLocations.AnyAsync(x => x.Id == input.LocationId, ct)) return InventoryResult<InventoryLotView>.Failure("location_not_found", "Blood-bank location was not found.");
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        if (input.Units is < 1 or > 10000) return InventoryResult<InventoryLotView>.Failure("invalid_stock", "Units must be between 1 and 10000.");
        if (input.ExpiryDate <= today) return InventoryResult<InventoryLotView>.Failure("invalid_stock", "Expiry date must be after today; expired blood cannot be received.");
        if (input.ExpiryDate > today.AddDays(MaxShelfLifeDays)) return InventoryResult<InventoryLotView>.Failure("invalid_stock", $"Expiry date must be within {MaxShelfLifeDays} days of today; check the year.");
        if (string.IsNullOrWhiteSpace(input.Source) || input.Source.Length > 200) return InventoryResult<InventoryLotView>.Failure("invalid_stock", "Source is required and must be at most 200 characters.");
        var lot = new InventoryLot(input.LocationId, input.BloodType, input.Units, input.ExpiryDate, input.Source); db.InventoryLots.Add(lot); await db.SaveChangesAsync(ct);
        var location = await db.BloodBankLocations.FindAsync([input.LocationId], ct); return InventoryResult<InventoryLotView>.Success(Map(lot, location!.Name));
    }

    public async Task<InventoryResult<InventoryLotView>> AdjustAsync(Guid lotId, int unitsAvailable, uint expectedVersion, CancellationToken ct)
    {
        var lot = await db.InventoryLots.SingleOrDefaultAsync(x => x.Id == lotId, ct); if (lot is null) return InventoryResult<InventoryLotView>.Failure("lot_not_found", "Inventory lot was not found.");
        if (lot.Version != expectedVersion) return InventoryResult<InventoryLotView>.Failure("concurrency_conflict", "Inventory lot changed; reload before adjusting.");
        // Adjusting would silently put quarantined or expired blood back into available stock.
        if (lot.Status is InventoryLotStatus.Quarantined or InventoryLotStatus.Expired) return InventoryResult<InventoryLotView>.Failure("invalid_lot_state", $"A {lot.Status.ToString().ToLowerInvariant()} lot cannot be adjusted.");
        // Units already held or dispatched from this lot are physically gone from the shelf; counting them again would over-state stock and break later releases.
        var committed = await db.InventoryReservations.Where(x => x.InventoryLotId == lotId && (x.Status == ReservationStatus.Active || x.Status == ReservationStatus.Dispatched)).SumAsync(x => x.Units, ct);
        if (unitsAvailable > lot.UnitsReceived - committed) return InventoryResult<InventoryLotView>.Failure("invalid_adjustment", $"At most {Math.Max(0, lot.UnitsReceived - committed)} unit(s) can be available: {committed} of {lot.UnitsReceived} received are reserved or dispatched.");
        try { lot.AdjustAvailable(unitsAvailable); await db.SaveChangesAsync(ct); }
        catch (InvalidOperationException ex) { return InventoryResult<InventoryLotView>.Failure("invalid_adjustment", ex.Message); }
        catch (DbUpdateConcurrencyException) { return InventoryResult<InventoryLotView>.Failure("concurrency_conflict", "Inventory lot changed; reload before adjusting."); }
        var name = await LocationName(lot.LocationId, ct); return InventoryResult<InventoryLotView>.Success(Map(lot, name));
    }

    public async Task<InventoryResult<InventoryLotView>> QuarantineAsync(Guid lotId, CancellationToken ct)
    {
        var lot = await db.InventoryLots.SingleOrDefaultAsync(x => x.Id == lotId, ct); if (lot is null) return InventoryResult<InventoryLotView>.Failure("lot_not_found", "Inventory lot was not found.");
        if (await db.InventoryReservations.AnyAsync(x => x.InventoryLotId == lotId && x.Status == ReservationStatus.Active, ct)) return InventoryResult<InventoryLotView>.Failure("active_reservations", "Release active reservations before quarantining this lot.");
        lot.Quarantine(); await db.SaveChangesAsync(ct); return InventoryResult<InventoryLotView>.Success(Map(lot, await LocationName(lot.LocationId, ct)));
    }

    public async Task<PagedResult<InventoryLotView>> ListAsync(InventoryQuery input, CancellationToken ct)
    {
        var page = Math.Max(1, input.Page); var size = Math.Clamp(input.PageSize, 1, 100); var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime); var query = db.InventoryLots.AsNoTracking();
        if (input.LocationId is not null) query = query.Where(x => x.LocationId == input.LocationId); if (input.BloodType is not null) query = query.Where(x => x.BloodType == input.BloodType); if (input.Status is not null) query = query.Where(x => x.Status == input.Status); if (!input.IncludeExpired) query = query.Where(x => x.ExpiryDate >= today && x.Status != InventoryLotStatus.Expired);
        query = (input.SortBy.ToLowerInvariant(), input.Descending) switch { ("units", false) => query.OrderBy(x => x.UnitsAvailable), ("units", true) => query.OrderByDescending(x => x.UnitsAvailable), ("bloodtype", false) => query.OrderBy(x => x.BloodType), ("bloodtype", true) => query.OrderByDescending(x => x.BloodType), (_, true) => query.OrderByDescending(x => x.ExpiryDate), _ => query.OrderBy(x => x.ExpiryDate) };
        var count = await query.CountAsync(ct); var lots = await query.Skip((page - 1) * size).Take(size).ToListAsync(ct); var names = await LocationNames(lots.Select(x => x.LocationId), ct);
        return new(lots.Select(x => Map(x, names[x.LocationId])).ToList(), page, size, count);
    }

    public async Task<IReadOnlyList<InventoryLotView>> ExpiringSoonAsync(int days, CancellationToken ct)
    {
        days = Math.Clamp(days, 1, 90); var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime); var end = today.AddDays(days);
        var lots = await db.InventoryLots.AsNoTracking().Where(x => x.UnitsAvailable > 0 && x.ExpiryDate >= today && x.ExpiryDate <= end && x.Status != InventoryLotStatus.Quarantined).OrderBy(x => x.ExpiryDate).ToListAsync(ct); var names = await LocationNames(lots.Select(x => x.LocationId), ct);
        return lots.Select(x => Map(x, names[x.LocationId])).ToList();
    }

    public async Task<InventoryResult<ReservationPlan>> ReserveAsync(ReserveInventoryInput input, CancellationToken ct)
    {
        if (input.Units is < 1 or > 100 || string.IsNullOrWhiteSpace(input.IdempotencyKey) || input.IdempotencyKey.Length > MaxIdempotencyKeyLength || input.HoldMinutes is < 5 or > 1440) return InventoryResult<ReservationPlan>.Failure("invalid_reservation", "Reservation units, key, or hold duration is invalid.");
        var request = await db.BloodRequests.SingleOrDefaultAsync(x => x.Id == input.BloodRequestId, ct); if (request is null) return InventoryResult<ReservationPlan>.Failure("request_not_found", "Blood request was not found.");
        if (request.Status is BloodRequestStatus.Fulfilled or BloodRequestStatus.ClosedUnfulfilled) return InventoryResult<ReservationPlan>.Failure("request_closed", "Cannot reserve stock for a closed request.");
        var prefix = input.IdempotencyKey + ":"; var existing = await db.InventoryReservations.Where(x => x.BloodRequestId == input.BloodRequestId && x.IdempotencyKey.StartsWith(prefix)).ToListAsync(ct);
        if (existing.Count > 0) return InventoryResult<ReservationPlan>.Success(await BuildPlan(request, input.Units, existing, ct));
        // Lapsed holds still lock units until expired; free them so new reservations see the real shelf.
        await ExpireReservationsAsync(ct);
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime); var compatible = CompatibilityMatrix[request.BloodType];
        var lots = await db.InventoryLots.Where(x => compatible.Contains(x.BloodType) && x.UnitsAvailable > 0 && x.ExpiryDate >= today && x.Status == InventoryLotStatus.Available).OrderBy(x => x.ExpiryDate).ThenBy(x => x.BloodType == request.BloodType ? 0 : 1).ToListAsync(ct);
        var remaining = input.Units; var reservations = new List<InventoryReservation>();
        foreach (var lot in lots) { if (remaining == 0) break; var units = Math.Min(remaining, lot.UnitsAvailable); lot.Reserve(units); var reservation = new InventoryReservation(request.Id, lot.Id, units, timeProvider.GetUtcNow().AddMinutes(input.HoldMinutes), prefix + lot.Id.ToString("N")); db.InventoryReservations.Add(reservation); reservations.Add(reservation); remaining -= units; }
        if (reservations.Count == 0) return InventoryResult<ReservationPlan>.Failure("stock_unavailable", "No compatible unexpired stock is available.");
        try { await db.SaveChangesAsync(ct); } catch (DbUpdateConcurrencyException) { return InventoryResult<ReservationPlan>.Failure("concurrency_conflict", "Stock changed while reserving; retry with the same idempotency key."); }
        return InventoryResult<ReservationPlan>.Success(await BuildPlan(request, input.Units, reservations, ct));
    }

    public async Task<InventoryResult<ReservationView>> ReleaseAsync(Guid reservationId, string reason, CancellationToken ct)
    {
        var reservation = await db.InventoryReservations.SingleOrDefaultAsync(x => x.Id == reservationId, ct); if (reservation is null) return InventoryResult<ReservationView>.Failure("reservation_not_found", "Reservation was not found.");
        var lot = await db.InventoryLots.SingleAsync(x => x.Id == reservation.InventoryLotId, ct); try { reservation.Release(string.IsNullOrWhiteSpace(reason) ? "Released by administrator." : reason); lot.Release(reservation.Units); await db.SaveChangesAsync(ct); } catch (InvalidOperationException ex) { return InventoryResult<ReservationView>.Failure("invalid_reservation_state", ex.Message); }
        return InventoryResult<ReservationView>.Success(Map(reservation, lot.BloodType));
    }

    public async Task<InventoryResult<ReservationView>> DispatchAsync(Guid reservationId, Guid approvedByUserId, CancellationToken ct)
    {
        var reservation = await db.InventoryReservations.SingleOrDefaultAsync(x => x.Id == reservationId, ct); if (reservation is null) return InventoryResult<ReservationView>.Failure("reservation_not_found", "Reservation was not found.");
        if (reservation.ExpiresAtUtc <= timeProvider.GetUtcNow()) return InventoryResult<ReservationView>.Failure("reservation_expired", "Reservation has expired.");
        var lot = await db.InventoryLots.SingleAsync(x => x.Id == reservation.InventoryLotId, ct);
        if (lot.ExpiryDate < DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime)) return InventoryResult<ReservationView>.Failure("invalid_reservation_state", "The reserved lot has expired and cannot be dispatched; release this reservation.");
        try { reservation.MarkDispatched(); db.DispatchRecords.Add(new DispatchRecord(reservation.BloodRequestId, reservation.Id, reservation.Units, timeProvider.GetUtcNow(), approvedByUserId)); await db.SaveChangesAsync(ct); } catch (InvalidOperationException ex) { return InventoryResult<ReservationView>.Failure("invalid_reservation_state", ex.Message); }
        return InventoryResult<ReservationView>.Success(Map(reservation, lot.BloodType));
    }

    public async Task<int> ExpireReservationsAsync(CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow(); var reservations = await db.InventoryReservations.Where(x => x.Status == ReservationStatus.Active && x.ExpiresAtUtc <= now).ToListAsync(ct);
        foreach (var reservation in reservations) { var lot = await db.InventoryLots.SingleAsync(x => x.Id == reservation.InventoryLotId, ct); reservation.MarkExpired(); lot.Release(reservation.Units); }
        await db.SaveChangesAsync(ct); return reservations.Count;
    }

    public async Task<InventoryResult<int>> DispatchRequestAsync(Guid bloodRequestId, Guid approvedByUserId, CancellationToken ct)
    {
        var request = await db.BloodRequests.SingleOrDefaultAsync(x => x.Id == bloodRequestId, ct); if (request is null) return InventoryResult<int>.Failure("request_not_found", "Blood request was not found.");
        if (request.Status is BloodRequestStatus.Fulfilled or BloodRequestStatus.ClosedUnfulfilled) return InventoryResult<int>.Failure("request_closed", "Cannot dispatch stock for a closed request.");
        await ExpireReservationsAsync(ct);
        var now = timeProvider.GetUtcNow(); var today = DateOnly.FromDateTime(now.UtcDateTime);
        var reservations = await db.InventoryReservations.Where(x => x.BloodRequestId == bloodRequestId && (x.Status == ReservationStatus.Dispatched || x.Status == ReservationStatus.Active)).ToListAsync(ct);
        var needed = request.QuantityUnits - reservations.Where(x => x.Status == ReservationStatus.Dispatched).Sum(x => x.Units);
        if (needed <= 0) return InventoryResult<int>.Success(0);
        var holds = reservations.Where(x => x.Status == ReservationStatus.Active && x.ExpiresAtUtc > now).OrderBy(x => x.ExpiresAtUtc).ToList();
        var compatible = CompatibilityMatrix[request.BloodType];
        var lots = await db.InventoryLots.Where(x => compatible.Contains(x.BloodType) && x.UnitsAvailable > 0 && x.ExpiryDate >= today && x.Status == InventoryLotStatus.Available).OrderBy(x => x.ExpiryDate).ThenBy(x => x.BloodType == request.BloodType ? 0 : 1).ToListAsync(ct);
        var available = holds.Sum(x => x.Units) + lots.Sum(x => x.UnitsAvailable);
        if (available < needed) return InventoryResult<int>.Failure("stock_unavailable", $"Only {available} compatible unit(s) are in stock but {needed} are needed. Receive more stock before dispatching.");
        var remaining = needed;
        foreach (var hold in holds) { if (remaining <= 0) break; hold.MarkDispatched(); db.DispatchRecords.Add(new DispatchRecord(request.Id, hold.Id, hold.Units, now, approvedByUserId)); remaining -= hold.Units; }
        foreach (var lot in lots)
        {
            if (remaining <= 0) break;
            var units = Math.Min(remaining, lot.UnitsAvailable); lot.Reserve(units);
            var reservation = new InventoryReservation(request.Id, lot.Id, units, now.AddMinutes(5), $"manual-dispatch:{request.Id:N}:{Guid.NewGuid():N}");
            reservation.MarkDispatched(); db.InventoryReservations.Add(reservation); db.DispatchRecords.Add(new DispatchRecord(request.Id, reservation.Id, units, now, approvedByUserId)); remaining -= units;
        }
        try { await db.SaveChangesAsync(ct); } catch (DbUpdateConcurrencyException) { return InventoryResult<int>.Failure("concurrency_conflict", "Stock changed while dispatching; try again."); }
        return InventoryResult<int>.Success(needed);
    }

    public async Task<IReadOnlyList<StockLevelView>> StockReportAsync(CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime); var cutoff = today.AddDays(7);
        var rows = await db.InventoryLots.AsNoTracking().Join(db.BloodBankLocations, l => l.LocationId, p => p.Id, (l, p) => new { l, p }).Where(x => x.l.UnitsAvailable > 0 && x.l.ExpiryDate >= today && x.l.Status != InventoryLotStatus.Quarantined && x.l.Status != InventoryLotStatus.Expired).ToListAsync(ct);
        return rows.GroupBy(x => new { x.p.Id, x.p.Name, x.l.BloodType }).Select(g => new StockLevelView(g.Key.Id, g.Key.Name, g.Key.BloodType, g.Sum(x => x.l.UnitsAvailable), g.Count(), g.Count(x => x.l.ExpiryDate <= cutoff))).OrderBy(x => x.LocationName).ThenBy(x => x.BloodType).ToList();
    }

    public CompatibilityView Compatibility(BloodType recipientType) => new(recipientType, CompatibilityMatrix[recipientType]);
    private async Task<ReservationPlan> BuildPlan(BloodRequest request, int requestedUnits, IReadOnlyList<InventoryReservation> reservations, CancellationToken ct) { var lotIds = reservations.Select(x => x.InventoryLotId).ToList(); var lots = await db.InventoryLots.Where(x => lotIds.Contains(x.Id)).Select(x => new { x.Id, x.BloodType, x.ExpiryDate }).ToDictionaryAsync(x => x.Id, ct); var views = reservations.OrderBy(x => lots[x.InventoryLotId].ExpiryDate).ThenBy(x => x.InventoryLotId).Select(x => Map(x, lots[x.InventoryLotId].BloodType)).ToList(); var total = views.Sum(x => x.Units); return new(request.Id, request.BloodType, requestedUnits, total, views, total >= requestedUnits); }
    private async Task<string> LocationName(Guid id, CancellationToken ct) => await db.BloodBankLocations.Where(x => x.Id == id).Select(x => x.Name).SingleAsync(ct);
    private async Task<Dictionary<Guid, string>> LocationNames(IEnumerable<Guid> ids, CancellationToken ct) { var list = ids.Distinct().ToList(); return await db.BloodBankLocations.Where(x => list.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Name, ct); }
    private static string? ValidateLocation(LocationInput x) => string.IsNullOrWhiteSpace(x.Name) || x.Name.Length > 250 ? "Name is required and must be at most 250 characters." : string.IsNullOrWhiteSpace(x.Address) || x.Address.Length > 500 ? "Address is required and must be at most 500 characters." : x.Latitude is < -90 or > 90 || x.Longitude is < -180 or > 180 ? "Coordinates are outside valid ranges." : null;
    private static LocationView Map(BloodBankLocation x) => new(x.Id, x.Name, x.Address, x.Latitude, x.Longitude, x.CreatedAtUtc);
    private static InventoryLotView Map(InventoryLot x, string locationName) => new(x.Id, x.LocationId, locationName, x.BloodType, x.UnitsReceived, x.UnitsAvailable, x.ExpiryDate, x.Source, x.Status, x.Version, x.UpdatedAtUtc);
    private static ReservationView Map(InventoryReservation x, BloodType type) => new(x.Id, x.BloodRequestId, x.InventoryLotId, type, x.Units, x.Status, x.ExpiresAtUtc, x.IdempotencyKey);
}
