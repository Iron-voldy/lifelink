using LifeLink.Application.Donors;
using LifeLink.Domain.Enums;

namespace LifeLink.Application.Inventory;

public sealed record LocationInput(string Name, string Address, decimal Latitude, decimal Longitude);
public sealed record LocationView(Guid Id, string Name, string Address, decimal Latitude, decimal Longitude, DateTimeOffset CreatedAtUtc);
public sealed record StockInInput(Guid LocationId, BloodType BloodType, int Units, DateOnly ExpiryDate, string Source);
public sealed record InventoryLotView(Guid Id, Guid LocationId, string LocationName, BloodType BloodType, int UnitsReceived, int UnitsAvailable, DateOnly ExpiryDate, string Source, InventoryLotStatus Status, uint Version, DateTimeOffset UpdatedAtUtc);
public sealed record InventoryQuery(Guid? LocationId, BloodType? BloodType, InventoryLotStatus? Status, bool IncludeExpired, string SortBy, bool Descending, int Page, int PageSize);
public sealed record ReserveInventoryInput(Guid BloodRequestId, int Units, string IdempotencyKey, int HoldMinutes = 30);
public sealed record ReservationView(Guid Id, Guid BloodRequestId, Guid InventoryLotId, BloodType BloodType, int Units, ReservationStatus Status, DateTimeOffset ExpiresAtUtc, string IdempotencyKey);
public sealed record ReservationPlan(Guid BloodRequestId, BloodType RequestedBloodType, int RequestedUnits, int ReservedUnits, IReadOnlyList<ReservationView> Reservations, bool FullyReserved);
public sealed record StockLevelView(Guid LocationId, string LocationName, BloodType BloodType, int AvailableUnits, int LotCount, int ExpiringWithinSevenDays);
public sealed record CompatibilityView(BloodType RecipientType, IReadOnlyList<BloodType> CompatibleDonorTypes);
public sealed record InventoryResult<T>(T? Value, string? ErrorCode, string? ErrorMessage)
{
    public bool Succeeded => ErrorCode is null;
    public static InventoryResult<T> Success(T value) => new(value, null, null);
    public static InventoryResult<T> Failure(string code, string message) => new(default, code, message);
}

public interface IInventoryService
{
    Task<InventoryResult<LocationView>> CreateLocationAsync(LocationInput input, CancellationToken ct);
    Task<InventoryResult<LocationView>> UpdateLocationAsync(Guid id, LocationInput input, CancellationToken ct);
    Task<IReadOnlyList<LocationView>> ListLocationsAsync(CancellationToken ct);
    Task<InventoryResult<InventoryLotView>> StockInAsync(StockInInput input, CancellationToken ct);
    Task<InventoryResult<InventoryLotView>> AdjustAsync(Guid lotId, int unitsAvailable, uint expectedVersion, CancellationToken ct);
    Task<InventoryResult<InventoryLotView>> QuarantineAsync(Guid lotId, CancellationToken ct);
    Task<PagedResult<InventoryLotView>> ListAsync(InventoryQuery query, CancellationToken ct);
    Task<IReadOnlyList<InventoryLotView>> ExpiringSoonAsync(int days, CancellationToken ct);
    Task<InventoryResult<ReservationPlan>> ReserveAsync(ReserveInventoryInput input, CancellationToken ct);
    Task<InventoryResult<ReservationView>> ReleaseAsync(Guid reservationId, string reason, CancellationToken ct);
    Task<InventoryResult<ReservationView>> DispatchAsync(Guid reservationId, Guid approvedByUserId, CancellationToken ct);
    Task<int> ExpireReservationsAsync(CancellationToken ct);
    /// <summary>Takes a request's outstanding units out of stock (active holds first, then FEFO) so a manual dispatch moves real inventory.</summary>
    Task<InventoryResult<int>> DispatchRequestAsync(Guid bloodRequestId, Guid approvedByUserId, CancellationToken ct);
    Task<IReadOnlyList<StockLevelView>> StockReportAsync(CancellationToken ct);
    CompatibilityView Compatibility(BloodType recipientType);
}
