using LifeLink.Application.Donors;
using LifeLink.Domain.Enums;

namespace LifeLink.Application.Camps;

public sealed record CampInput(string Name, string Location, decimal? Latitude, decimal? Longitude, DateTimeOffset StartsAtUtc, DateTimeOffset EndsAtUtc, int Capacity, int SlotMinutes);
public sealed record CampView(Guid Id, Guid OrganizerUserId, string Name, string Location, decimal? Latitude, decimal? Longitude, DateTimeOffset StartsAtUtc, DateTimeOffset EndsAtUtc, int Capacity, CampStatus Status, int AvailableSlots, int BookedSlots, DateTimeOffset CreatedAtUtc);
/// <param name="PublicOnly">Hide Draft camps, which are not announced yet, from non-administrators.</param>
public sealed record CampQuery(string? Search, CampStatus? Status, DateTimeOffset? FromUtc, DateTimeOffset? ToUtc, int Page, int PageSize, bool PublicOnly = false);
public sealed record CampSlotView(Guid Id, Guid CampId, Guid? DonorId, DateTimeOffset SlotTimeUtc, CampSlotStatus Status, uint Version);
/// <summary>Admin-only view of a booked slot with enough donor detail to check the person in.</summary>
public sealed record CampRosterEntry(Guid SlotId, DateTimeOffset SlotTimeUtc, CampSlotStatus Status, Guid DonorId, string DonorEmail, BloodType BloodType);
public sealed record AttendanceReport(Guid CampId, int Capacity, int Booked, int CheckedIn, int NoShows, int Cancelled, double AttendanceRate);
public sealed record CampResult<T>(T? Value, string? ErrorCode, string? ErrorMessage)
{
    public bool Succeeded => ErrorCode is null;
    public static CampResult<T> Success(T value) => new(value, null, null);
    public static CampResult<T> Failure(string code, string message) => new(default, code, message);
}
public interface ICampService
{
    Task<CampResult<CampView>> CreateAsync(Guid organizerUserId, CampInput input, CancellationToken ct);
    Task<CampResult<CampView>> GetAsync(Guid id, CancellationToken ct);
    Task<PagedResult<CampView>> ListAsync(CampQuery query, CancellationToken ct);
    Task<CampResult<CampView>> UpdateAsync(Guid id, CampInput input, CancellationToken ct);
    Task<CampResult<CampView>> TransitionAsync(Guid id, CampStatus status, CancellationToken ct);
    Task<CampResult<CampSlotView>> BookAsync(Guid campId, Guid donorUserId, Guid? preferredSlotId, CancellationToken ct);
    Task<CampResult<CampSlotView>> CancelBookingAsync(Guid campId, Guid slotId, Guid donorUserId, CancellationToken ct);
    Task<CampResult<CampSlotView>> CheckInAsync(Guid campId, Guid slotId, CancellationToken ct);
    Task<CampResult<IReadOnlyList<CampSlotView>>> SlotsAsync(Guid campId, CancellationToken ct);
    Task<CampResult<AttendanceReport>> AttendanceAsync(Guid campId, CancellationToken ct);
    Task<CampResult<IReadOnlyList<CampRosterEntry>>> RosterAsync(Guid campId, CancellationToken ct);
}
