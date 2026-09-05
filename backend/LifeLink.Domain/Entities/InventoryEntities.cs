using LifeLink.Domain.Common;
using LifeLink.Domain.Enums;

namespace LifeLink.Domain.Entities;

public sealed class BloodBankLocation : Entity
{
    public string Name { get; private set; } = string.Empty;
    public string Address { get; private set; } = string.Empty;
    public decimal Latitude { get; private set; }
    public decimal Longitude { get; private set; }
    private BloodBankLocation() { }
    public BloodBankLocation(string name, string address, decimal latitude, decimal longitude) { Name = name.Trim(); Address = address.Trim(); Latitude = latitude; Longitude = longitude; }
    public void Update(string name, string address, decimal latitude, decimal longitude) { Name = name.Trim(); Address = address.Trim(); Latitude = latitude; Longitude = longitude; UpdatedAtUtc = DateTimeOffset.UtcNow; }
}

public sealed class InventoryLot : Entity
{
    public Guid LocationId { get; private set; }
    public BloodType BloodType { get; private set; }
    public int UnitsReceived { get; private set; }
    public int UnitsAvailable { get; private set; }
    public DateOnly ExpiryDate { get; private set; }
    public string Source { get; private set; } = string.Empty;
    public InventoryLotStatus Status { get; private set; } = InventoryLotStatus.Available;
    public uint Version { get; private set; }
    private InventoryLot() { }
    public InventoryLot(Guid locationId, BloodType bloodType, int units, DateOnly expiryDate, string source)
    { LocationId = locationId; BloodType = bloodType; UnitsReceived = units; UnitsAvailable = units; ExpiryDate = expiryDate; Source = source.Trim(); }
    public void AdjustAvailable(int unitsAvailable) { if (unitsAvailable < 0 || unitsAvailable > UnitsReceived) throw new InvalidOperationException("Available units are outside the lot bounds."); UnitsAvailable = unitsAvailable; Status = unitsAvailable == 0 ? InventoryLotStatus.Reserved : InventoryLotStatus.Available; UpdatedAtUtc = DateTimeOffset.UtcNow; }
    public void Reserve(int units) { if (units < 1 || units > UnitsAvailable || Status is InventoryLotStatus.Expired or InventoryLotStatus.Quarantined) throw new InvalidOperationException("Lot cannot satisfy reservation."); UnitsAvailable -= units; Status = UnitsAvailable == 0 ? InventoryLotStatus.Reserved : InventoryLotStatus.Available; UpdatedAtUtc = DateTimeOffset.UtcNow; }
    public void Release(int units) { if (units < 1 || UnitsAvailable + units > UnitsReceived) throw new InvalidOperationException("Release exceeds received units."); UnitsAvailable += units; Status = InventoryLotStatus.Available; UpdatedAtUtc = DateTimeOffset.UtcNow; }
    public void MarkExpired() { UnitsAvailable = 0; Status = InventoryLotStatus.Expired; UpdatedAtUtc = DateTimeOffset.UtcNow; }
    public void Quarantine() { Status = InventoryLotStatus.Quarantined; UpdatedAtUtc = DateTimeOffset.UtcNow; }
}

public sealed class InventoryReservation : Entity
{
    public Guid BloodRequestId { get; private set; }
    public Guid InventoryLotId { get; private set; }
    public int Units { get; private set; }
    public ReservationStatus Status { get; private set; } = ReservationStatus.Active;
    public DateTimeOffset ExpiresAtUtc { get; private set; }
    public string IdempotencyKey { get; private set; } = string.Empty;
    public string? ReleaseReason { get; private set; }
    private InventoryReservation() { }
    public InventoryReservation(Guid requestId, Guid lotId, int units, DateTimeOffset expiresAtUtc, string idempotencyKey)
    { BloodRequestId = requestId; InventoryLotId = lotId; Units = units; ExpiresAtUtc = expiresAtUtc; IdempotencyKey = idempotencyKey; }
    public void Release(string reason) { if (Status != ReservationStatus.Active) throw new InvalidOperationException("Only active reservations can be released."); Status = ReservationStatus.Released; ReleaseReason = reason.Trim(); UpdatedAtUtc = DateTimeOffset.UtcNow; }
    public void MarkDispatched() { if (Status != ReservationStatus.Active) throw new InvalidOperationException("Only active reservations can be dispatched."); Status = ReservationStatus.Dispatched; UpdatedAtUtc = DateTimeOffset.UtcNow; }
    public void MarkExpired() { if (Status != ReservationStatus.Active) return; Status = ReservationStatus.Expired; ReleaseReason = "Reservation expired."; UpdatedAtUtc = DateTimeOffset.UtcNow; }
}

public sealed class DispatchRecord : Entity
{
    public Guid BloodRequestId { get; private set; }
    public Guid InventoryReservationId { get; private set; }
    public int UnitsDispatched { get; private set; }
    public DateTimeOffset DispatchedAtUtc { get; private set; }
    public Guid ApprovedByUserId { get; private set; }
    private DispatchRecord() { }
    public DispatchRecord(Guid requestId, Guid reservationId, int units, DateTimeOffset dispatchedAtUtc, Guid approvedByUserId)
    { BloodRequestId = requestId; InventoryReservationId = reservationId; UnitsDispatched = units; DispatchedAtUtc = dispatchedAtUtc; ApprovedByUserId = approvedByUserId; }
}
