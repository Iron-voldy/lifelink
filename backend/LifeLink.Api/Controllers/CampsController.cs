using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using LifeLink.Application.Camps;
using LifeLink.Application.Donors;
using LifeLink.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LifeLink.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/camps")]
public sealed class CampsController(ICampService camps) : ControllerBase
{
    [HttpPost]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<CampView>> Create(CampRequest input, CancellationToken ct) { var result = await camps.CreateAsync(CurrentUserId(), input.ToContract(), ct); return result.Succeeded ? CreatedAtAction(nameof(Get), new { id = result.Value!.Id }, result.Value) : Error(result); }
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CampView>> Get(Guid id, CancellationToken ct) { var result = await camps.GetAsync(id, ct); if (result.Succeeded && result.Value!.Status == CampStatus.Draft && !IsAdmin()) return Problem(statusCode: 404, title: "camp_not_found", detail: "Donation camp was not found."); return result.Succeeded ? Ok(result.Value) : Error(result); }
    [HttpGet]
    public async Task<ActionResult<PagedResult<CampView>>> List([FromQuery] string? search, [FromQuery] CampStatus? status, [FromQuery] DateTimeOffset? fromUtc, [FromQuery] DateTimeOffset? toUtc, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) => Ok(await camps.ListAsync(new(search, status, fromUtc, toUtc, page, pageSize, PublicOnly: !IsAdmin()), ct));
    [HttpPut("{id:guid}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<CampView>> Update(Guid id, CampRequest input, CancellationToken ct) { var result = await camps.UpdateAsync(id, input.ToContract(), ct); return result.Succeeded ? Ok(result.Value) : Error(result); }
    [HttpPut("{id:guid}/status")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<CampView>> Transition(Guid id, CampTransitionRequest input, CancellationToken ct) { var result = await camps.TransitionAsync(id, input.Status, ct); return result.Succeeded ? Ok(result.Value) : Error(result); }
    [HttpGet("{id:guid}/slots")]
    public async Task<ActionResult<IReadOnlyList<CampSlotView>>> Slots(Guid id, [FromServices] IDonorService donors, CancellationToken ct)
    {
        var result = await camps.SlotsAsync(id, ct); if (!result.Succeeded) return Error(result);
        if (IsAdmin()) return Ok(result.Value);
        // Which donor holds which slot is personal data: non-admins only see their own booking.
        var mine = await donors.GetByUserIdAsync(CurrentUserId(), ct); var ownDonorId = mine.Succeeded ? mine.Value!.Id : (Guid?)null;
        return Ok(result.Value!.Select(x => x.DonorId is not null && x.DonorId != ownDonorId ? x with { DonorId = null } : x).ToList());
    }
    [HttpPost("{id:guid}/slots/book")]
    [Authorize(Policy = "DonorOnly")]
    public async Task<ActionResult<CampSlotView>> Book(Guid id, BookSlotRequest input, CancellationToken ct) { var result = await camps.BookAsync(id, CurrentUserId(), input.PreferredSlotId, ct); return result.Succeeded ? Ok(result.Value) : Error(result); }
    [HttpDelete("{id:guid}/slots/{slotId:guid}/booking")]
    [Authorize(Policy = "DonorOnly")]
    public async Task<IActionResult> Cancel(Guid id, Guid slotId, CancellationToken ct) { var result = await camps.CancelBookingAsync(id, slotId, CurrentUserId(), ct); return result.Succeeded ? NoContent() : Error(result); }
    [HttpPut("{id:guid}/slots/{slotId:guid}/check-in")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<CampSlotView>> CheckIn(Guid id, Guid slotId, CancellationToken ct) { var result = await camps.CheckInAsync(id, slotId, ct); return result.Succeeded ? Ok(result.Value) : Error(result); }
    [HttpGet("{id:guid}/attendance-report")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<AttendanceReport>> Attendance(Guid id, CancellationToken ct) { var result = await camps.AttendanceAsync(id, ct); return result.Succeeded ? Ok(result.Value) : Error(result); }
    [HttpGet("{id:guid}/roster")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<IReadOnlyList<CampRosterEntry>>> Roster(Guid id, CancellationToken ct) { var result = await camps.RosterAsync(id, ct); return result.Succeeded ? Ok(result.Value) : Error(result); }
    private bool IsAdmin() => User.IsInRole(UserRole.BloodBankAdmin.ToString());
    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private ObjectResult Error<T>(CampResult<T> result) => result.ErrorCode switch { "camp_not_found" or "slot_not_found" => Problem(statusCode: 404, title: result.ErrorCode, detail: result.ErrorMessage), "already_booked" or "slot_unavailable" or "camp_has_bookings" or "invalid_transition" or "invalid_camp_state" or "invalid_slot_state" => Problem(statusCode: 409, title: result.ErrorCode, detail: result.ErrorMessage), "not_booking_owner" => Problem(statusCode: 403, title: result.ErrorCode, detail: result.ErrorMessage), _ => Problem(statusCode: 400, title: result.ErrorCode, detail: result.ErrorMessage) };
}

public sealed record CampRequest([Required(ErrorMessage = "Name is required."), MaxLength(250, ErrorMessage = "Name must be at most 250 characters.")] string Name, [Required(ErrorMessage = "Location is required."), MaxLength(500, ErrorMessage = "Location must be at most 500 characters.")] string Location, [Range(-90, 90, ErrorMessage = "Latitude must be between -90 and 90.")] decimal? Latitude, [Range(-180, 180, ErrorMessage = "Longitude must be between -180 and 180.")] decimal? Longitude, DateTimeOffset StartsAtUtc, DateTimeOffset EndsAtUtc, [Range(1, 1000, ErrorMessage = "Capacity must be between 1 and 1000.")] int Capacity, [Range(5, 240, ErrorMessage = "Slot minutes must be between 5 and 240.")] int SlotMinutes) { public CampInput ToContract() => new(Name, Location, Latitude, Longitude, StartsAtUtc, EndsAtUtc, Capacity, SlotMinutes); }
public sealed record CampTransitionRequest([EnumDataType(typeof(CampStatus), ErrorMessage = "Status is not one of the allowed values.")] CampStatus Status);
public sealed record BookSlotRequest(Guid? PreferredSlotId);
