using LifeLink.Domain.Common;
using LifeLink.Domain.Enums;

namespace LifeLink.Domain.Entities;

public sealed class DonationCamp : Entity
{
    public Guid OrganizerUserId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Location { get; private set; } = string.Empty;
    public decimal? Latitude { get; private set; }
    public decimal? Longitude { get; private set; }
    public DateTimeOffset StartsAtUtc { get; private set; }
    public DateTimeOffset EndsAtUtc { get; private set; }
    public int Capacity { get; private set; }
    public CampStatus Status { get; private set; } = CampStatus.Draft;
    private DonationCamp() { }
    public DonationCamp(Guid organizerUserId, string name, string location, decimal? latitude, decimal? longitude, DateTimeOffset startsAtUtc, DateTimeOffset endsAtUtc, int capacity)
    { OrganizerUserId = organizerUserId; Name = name.Trim(); Location = location.Trim(); Latitude = latitude; Longitude = longitude; StartsAtUtc = startsAtUtc; EndsAtUtc = endsAtUtc; Capacity = capacity; }
    public void Update(string name, string location, decimal? latitude, decimal? longitude, DateTimeOffset startsAtUtc, DateTimeOffset endsAtUtc, int capacity)
    { if (Status is not (CampStatus.Draft or CampStatus.Scheduled)) throw new InvalidOperationException("Camp cannot be edited in its current state."); Name = name.Trim(); Location = location.Trim(); Latitude = latitude; Longitude = longitude; StartsAtUtc = startsAtUtc; EndsAtUtc = endsAtUtc; Capacity = capacity; UpdatedAtUtc = DateTimeOffset.UtcNow; }
    public void TransitionTo(CampStatus status) { Status = status; UpdatedAtUtc = DateTimeOffset.UtcNow; }
}

public sealed class CampSlot : Entity
{
    public Guid CampId { get; private set; }
    public Guid? DonorId { get; private set; }
    public DateTimeOffset SlotTimeUtc { get; private set; }
    public CampSlotStatus Status { get; private set; } = CampSlotStatus.Available;
    public uint Version { get; private set; }
    private CampSlot() { }
    public CampSlot(Guid campId, DateTimeOffset slotTimeUtc) { CampId = campId; SlotTimeUtc = slotTimeUtc; }
    public void Book(Guid donorId) { if (Status != CampSlotStatus.Available) throw new InvalidOperationException("Slot is unavailable."); DonorId = donorId; Status = CampSlotStatus.Booked; UpdatedAtUtc = DateTimeOffset.UtcNow; }
    public void Cancel() { if (Status != CampSlotStatus.Booked) throw new InvalidOperationException("Only booked slots can be cancelled."); DonorId = null; Status = CampSlotStatus.Available; UpdatedAtUtc = DateTimeOffset.UtcNow; }
    public void CheckIn() { if (Status != CampSlotStatus.Booked) throw new InvalidOperationException("Only booked slots can be checked in."); Status = CampSlotStatus.CheckedIn; UpdatedAtUtc = DateTimeOffset.UtcNow; }
    public void MarkNoShow() { if (Status == CampSlotStatus.Booked) { Status = CampSlotStatus.NoShow; UpdatedAtUtc = DateTimeOffset.UtcNow; } }
    public void CancelByCamp() { if (Status is CampSlotStatus.Available or CampSlotStatus.Booked) { Status = CampSlotStatus.Cancelled; UpdatedAtUtc = DateTimeOffset.UtcNow; } }
}

public sealed class Notification : Entity
{
    public Guid RecipientUserId { get; private set; }
    public Guid? WorkflowExecutionId { get; private set; }
    public string Type { get; private set; } = string.Empty;
    public string Channel { get; private set; } = "Push";
    public string PayloadJson { get; private set; } = "{}";
    public NotificationStatus Status { get; private set; } = NotificationStatus.Pending;
    public int AttemptCount { get; private set; }
    public DateTimeOffset? SentAtUtc { get; private set; }
    public string IdempotencyKey { get; private set; } = string.Empty;
    public string? LastError { get; private set; }
    private Notification() { }
    public Notification(Guid recipientUserId, Guid? workflowExecutionId, string type, string channel, string payloadJson, string idempotencyKey)
    { RecipientUserId = recipientUserId; WorkflowExecutionId = workflowExecutionId; Type = type; Channel = channel; PayloadJson = payloadJson; IdempotencyKey = idempotencyKey; }
    public void MarkSent(DateTimeOffset sentAtUtc) { Status = NotificationStatus.Sent; SentAtUtc = sentAtUtc; AttemptCount++; LastError = null; UpdatedAtUtc = sentAtUtc; }
    public void MarkFailed(string error) { Status = NotificationStatus.Failed; AttemptCount++; LastError = error.Length > 2000 ? error[..2000] : error; UpdatedAtUtc = DateTimeOffset.UtcNow; }
}
