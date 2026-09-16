using LifeLink.Application.Donors;
using LifeLink.Domain.Enums;

namespace LifeLink.Application.Requests;

public sealed record HospitalRegistrationInput(string Name, string RegistrationNumber, string Address, decimal? Latitude, decimal? Longitude, string Position);
public sealed record HospitalView(Guid Id, string Name, string RegistrationNumber, VerificationStatus VerificationStatus, string Address, decimal? Latitude, decimal? Longitude, DateTimeOffset CreatedAtUtc);
public sealed record RequestInput(BloodType BloodType, int QuantityUnits, RequestUrgency RequestedUrgency, string? Notes, DateTimeOffset RequiredByUtc);
public sealed record BloodRequestView(Guid Id, Guid HospitalId, string HospitalName, Guid RequestedByUserId, BloodType BloodType, int QuantityUnits, RequestUrgency Urgency, BloodRequestStatus Status, string? Notes, DateTimeOffset RequiredByUtc, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc);
public sealed record RequestQuery(Guid? HospitalId, BloodRequestStatus? Status, RequestUrgency? Urgency, BloodType? BloodType, string SortBy, bool Descending, int Page, int PageSize);
public sealed record RequestHistoryView(Guid Id, BloodRequestStatus PreviousStatus, BloodRequestStatus NewStatus, Guid ChangedByUserId, string? Reason, DateTimeOffset ChangedAtUtc);
public sealed record RequestSummary(int Total, int Open, int Critical, int Fulfilled, int ClosedUnfulfilled, IReadOnlyDictionary<BloodType, int> UnitsByBloodType);
public sealed record RequestResult<T>(T? Value, string? ErrorCode, string? ErrorMessage)
{
    public bool Succeeded => ErrorCode is null;
    public static RequestResult<T> Success(T value) => new(value, null, null);
    public static RequestResult<T> Failure(string code, string message) => new(default, code, message);
}

public interface IRequestWorkflowTrigger
{
    Task TriggerAsync(Guid requestId, string reason, CancellationToken ct);
}

public interface IRequestService
{
    Task<RequestResult<HospitalView>> RegisterHospitalAsync(Guid staffUserId, HospitalRegistrationInput input, CancellationToken ct);
    Task<RequestResult<HospitalView>> SetHospitalVerificationAsync(Guid hospitalId, VerificationStatus status, CancellationToken ct);
    Task<PagedResult<HospitalView>> ListHospitalsAsync(VerificationStatus? status, int page, int pageSize, CancellationToken ct);
    Task<RequestResult<BloodRequestView>> CreateAsync(Guid staffUserId, RequestInput input, CancellationToken ct);
    Task<RequestResult<BloodRequestView>> GetAsync(Guid requestId, CancellationToken ct);
    Task<PagedResult<BloodRequestView>> ListAsync(RequestQuery query, CancellationToken ct);
    Task<RequestResult<BloodRequestView>> UpdateAsync(Guid requestId, RequestInput input, Guid actorUserId, CancellationToken ct);
    Task<RequestResult<BloodRequestView>> TransitionAsync(Guid requestId, BloodRequestStatus status, Guid actorUserId, string? reason, CancellationToken ct);
    Task<RequestResult<BloodRequestView>> EscalateAsync(Guid requestId, Guid actorUserId, string? reason, CancellationToken ct);
    Task<RequestResult<BloodRequestView>> CancelAsync(Guid requestId, Guid actorUserId, CancellationToken ct);
    Task<RequestResult<IReadOnlyList<RequestHistoryView>>> HistoryAsync(Guid requestId, CancellationToken ct);
    Task<RequestSummary> SummaryAsync(Guid? hospitalId, CancellationToken ct);
    Task<RequestResult<Guid>> GetHospitalIdForStaffAsync(Guid userId, CancellationToken ct);
    Task<RequestResult<HospitalView>> GetHospitalForStaffAsync(Guid userId, CancellationToken ct);
}
