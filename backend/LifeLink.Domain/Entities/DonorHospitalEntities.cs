using LifeLink.Domain.Common;
using LifeLink.Domain.Enums;

namespace LifeLink.Domain.Entities;

public sealed class Donor : Entity
{
    public Guid UserId { get; private set; }
    public BloodType BloodType { get; private set; }
    public DateOnly DateOfBirth { get; private set; }
    public DateOnly? LastDonationDate { get; private set; }
    public EligibilityStatus EligibilityStatus { get; private set; } = EligibilityStatus.PendingVerification;
    public string MedicalFlagsJson { get; private set; } = "[]";
    public string Address { get; private set; } = string.Empty;
    public decimal? Latitude { get; private set; }
    public decimal? Longitude { get; private set; }
    public bool IsActive { get; private set; } = true;
    private Donor() { }
    public Donor(Guid userId, BloodType bloodType, DateOnly dateOfBirth, string address, decimal? latitude, decimal? longitude, string medicalFlagsJson)
    {
        UserId = userId; BloodType = bloodType; DateOfBirth = dateOfBirth; Address = address.Trim(); Latitude = latitude; Longitude = longitude; MedicalFlagsJson = medicalFlagsJson;
    }
    public void Update(BloodType bloodType, DateOnly dateOfBirth, string address, decimal? latitude, decimal? longitude, string medicalFlagsJson)
    {
        BloodType = bloodType; DateOfBirth = dateOfBirth; Address = address.Trim(); Latitude = latitude; Longitude = longitude; MedicalFlagsJson = medicalFlagsJson; UpdatedAtUtc = DateTimeOffset.UtcNow;
    }
    public void ApplyEligibility(EligibilityStatus status) { EligibilityStatus = status; UpdatedAtUtc = DateTimeOffset.UtcNow; }
    public void RecordDonation(DateOnly date) { LastDonationDate = date; UpdatedAtUtc = DateTimeOffset.UtcNow; }
    public void Deactivate() { IsActive = false; UpdatedAtUtc = DateTimeOffset.UtcNow; }
}

public sealed class DonorEligibilityHistory : Entity
{
    public Guid DonorId { get; private set; }
    public EligibilityStatus PreviousStatus { get; private set; }
    public EligibilityStatus NewStatus { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public Guid? ChangedByUserId { get; private set; }
    private DonorEligibilityHistory() { }
    public DonorEligibilityHistory(Guid donorId, EligibilityStatus previousStatus, EligibilityStatus newStatus, string reason, Guid? changedByUserId)
    { DonorId = donorId; PreviousStatus = previousStatus; NewStatus = newStatus; Reason = reason; ChangedByUserId = changedByUserId; }
}

public sealed class DonationRecord : Entity
{
    public Guid DonorId { get; private set; }
    public DateOnly DonationDate { get; private set; }
    public int Units { get; private set; }
    public string Location { get; private set; } = string.Empty;
    public string? Notes { get; private set; }
    private DonationRecord() { }
    public DonationRecord(Guid donorId, DateOnly donationDate, int units, string location, string? notes)
    { DonorId = donorId; DonationDate = donationDate; Units = units; Location = location.Trim(); Notes = notes; }
}

public sealed class Hospital : Entity
{
    public string Name { get; private set; } = string.Empty;
    public string RegistrationNumber { get; private set; } = string.Empty;
    public VerificationStatus VerificationStatus { get; private set; } = VerificationStatus.Pending;
    public string Address { get; private set; } = string.Empty;
    public decimal? Latitude { get; private set; }
    public decimal? Longitude { get; private set; }
    private Hospital() { }
    public Hospital(string name, string registrationNumber, string address, decimal? latitude, decimal? longitude)
    { Name = name.Trim(); RegistrationNumber = registrationNumber.Trim().ToUpperInvariant(); Address = address.Trim(); Latitude = latitude; Longitude = longitude; }
    public void SetVerification(VerificationStatus status) { VerificationStatus = status; UpdatedAtUtc = DateTimeOffset.UtcNow; }
    public void Update(string name, string address, decimal? latitude, decimal? longitude) { Name = name.Trim(); Address = address.Trim(); Latitude = latitude; Longitude = longitude; UpdatedAtUtc = DateTimeOffset.UtcNow; }
}

public sealed class HospitalStaff : Entity
{
    public Guid UserId { get; private set; }
    public Guid HospitalId { get; private set; }
    public string Position { get; private set; } = string.Empty;
    private HospitalStaff() { }
    public HospitalStaff(Guid userId, Guid hospitalId, string position) { UserId = userId; HospitalId = hospitalId; Position = position.Trim(); }
}

public sealed class BloodRequest : Entity
{
    public Guid HospitalId { get; private set; }
    public Guid RequestedByUserId { get; private set; }
    public BloodType BloodType { get; private set; }
    public int QuantityUnits { get; private set; }
    public RequestUrgency Urgency { get; private set; }
    public BloodRequestStatus Status { get; private set; } = BloodRequestStatus.Submitted;
    public string? Notes { get; private set; }
    public DateTimeOffset RequiredByUtc { get; private set; }
    private BloodRequest() { }
    public BloodRequest(Guid hospitalId, Guid requestedByUserId, BloodType bloodType, int quantityUnits, RequestUrgency urgency, string? notes, DateTimeOffset requiredByUtc)
    { HospitalId = hospitalId; RequestedByUserId = requestedByUserId; BloodType = bloodType; QuantityUnits = quantityUnits; Urgency = urgency; Notes = notes?.Trim(); RequiredByUtc = requiredByUtc; }
    public void Update(BloodType bloodType, int quantityUnits, RequestUrgency urgency, string? notes, DateTimeOffset requiredByUtc)
    { BloodType = bloodType; QuantityUnits = quantityUnits; Urgency = urgency; Notes = notes?.Trim(); RequiredByUtc = requiredByUtc; UpdatedAtUtc = DateTimeOffset.UtcNow; }
    public void TransitionTo(BloodRequestStatus status) { Status = status; UpdatedAtUtc = DateTimeOffset.UtcNow; }
    public void Escalate(RequestUrgency urgency) { Urgency = urgency; Status = BloodRequestStatus.Escalated; UpdatedAtUtc = DateTimeOffset.UtcNow; }
}

public sealed class RequestStatusHistory : Entity
{
    public Guid BloodRequestId { get; private set; }
    public BloodRequestStatus PreviousStatus { get; private set; }
    public BloodRequestStatus NewStatus { get; private set; }
    public Guid ChangedByUserId { get; private set; }
    public string? Reason { get; private set; }
    private RequestStatusHistory() { }
    public RequestStatusHistory(Guid requestId, BloodRequestStatus previousStatus, BloodRequestStatus newStatus, Guid changedByUserId, string? reason)
    { BloodRequestId = requestId; PreviousStatus = previousStatus; NewStatus = newStatus; ChangedByUserId = changedByUserId; Reason = reason?.Trim(); }
}
