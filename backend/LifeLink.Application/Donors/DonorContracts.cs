using LifeLink.Domain.Enums;

namespace LifeLink.Application.Donors;

public sealed record DonorProfileInput(BloodType BloodType, DateOnly DateOfBirth, string Address, decimal? Latitude, decimal? Longitude, IReadOnlyList<string> MedicalFlags);
public sealed record DonorView(Guid Id, Guid UserId, string Email, BloodType BloodType, DateOnly DateOfBirth, DateOnly? LastDonationDate, EligibilityStatus EligibilityStatus, string Address, decimal? Latitude, decimal? Longitude, IReadOnlyList<string> MedicalFlags, bool IsActive, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc);
public sealed record DonorQuery(string? Search, BloodType? BloodType, EligibilityStatus? EligibilityStatus, bool IncludeInactive, string SortBy, bool Descending, int Page, int PageSize);
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
}
public sealed record EligibilityEvaluation(Guid DonorId, EligibilityStatus PreviousStatus, EligibilityStatus Status, IReadOnlyList<string> Reasons, DateTimeOffset EvaluatedAtUtc);
public sealed record EligibilityHistoryView(Guid Id, EligibilityStatus PreviousStatus, EligibilityStatus NewStatus, string Reason, Guid? ChangedByUserId, DateTimeOffset ChangedAtUtc);
public sealed record DonationRecordInput(DateOnly DonationDate, int Units, string Location, string? Notes);
public sealed record DonationRecordView(Guid Id, DateOnly DonationDate, int Units, string Location, string? Notes, DateTimeOffset CreatedAtUtc);
public sealed record DonorResult<T>(T? Value, string? ErrorCode, string? ErrorMessage)
{
    public bool Succeeded => ErrorCode is null;
    public static DonorResult<T> Success(T value) => new(value, null, null);
    public static DonorResult<T> Failure(string code, string message) => new(default, code, message);
}

public interface IDonorService
{
    Task<DonorResult<DonorView>> CreateAsync(Guid userId, DonorProfileInput input, CancellationToken ct);
    Task<DonorResult<DonorView>> GetAsync(Guid donorId, CancellationToken ct);
    Task<DonorResult<DonorView>> GetByUserIdAsync(Guid userId, CancellationToken ct);
    Task<PagedResult<DonorView>> ListAsync(DonorQuery query, CancellationToken ct);
    Task<DonorResult<DonorView>> UpdateAsync(Guid donorId, DonorProfileInput input, CancellationToken ct);
    Task<DonorResult<bool>> DeactivateAsync(Guid donorId, CancellationToken ct);
    Task<DonorResult<EligibilityEvaluation>> EvaluateEligibilityAsync(Guid donorId, Guid changedByUserId, bool verifiedByStaff, CancellationToken ct);
    Task<DonorResult<IReadOnlyList<EligibilityHistoryView>>> GetEligibilityHistoryAsync(Guid donorId, CancellationToken ct);
    Task<DonorResult<DonationRecordView>> RecordDonationAsync(Guid donorId, DonationRecordInput input, CancellationToken ct);
    Task<DonorResult<IReadOnlyList<DonationRecordView>>> GetDonationHistoryAsync(Guid donorId, CancellationToken ct);
}
