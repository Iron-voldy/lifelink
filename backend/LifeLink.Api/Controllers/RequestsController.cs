using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using LifeLink.Application.Donors;
using LifeLink.Application.Inventory;
using LifeLink.Application.Requests;
using LifeLink.Domain.Enums;
using LifeLink.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LifeLink.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/requests")]
public sealed class RequestsController(IRequestService requests) : ControllerBase
{
    [HttpPost]
    [Authorize(Policy = "HospitalOnly")]
    public async Task<ActionResult<BloodRequestView>> Create(BloodRequestInput input, CancellationToken ct)
    {
        var result = await requests.CreateAsync(CurrentUserId(), input.ToContract(), ct);
        return result.Succeeded ? CreatedAtAction(nameof(Get), new { id = result.Value!.Id }, result.Value) : Error(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<BloodRequestView>> Get(Guid id, CancellationToken ct)
    {
        var result = await requests.GetAsync(id, ct); if (!result.Succeeded) return Error(result);
        return await CanAccess(result.Value!.HospitalId, ct) ? Ok(result.Value) : Forbid();
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<BloodRequestView>>> List([FromQuery] Guid? hospitalId, [FromQuery] BloodRequestStatus? status, [FromQuery] RequestUrgency? urgency, [FromQuery] BloodType? bloodType, [FromQuery] string sortBy = "created", [FromQuery] bool descending = true, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        if (!IsAdmin() && !IsHospital()) return Forbid();
        if (!IsAdmin()) { var own = await requests.GetHospitalIdForStaffAsync(CurrentUserId(), ct); if (!own.Succeeded) return Error(own); hospitalId = own.Value; }
        return Ok(await requests.ListAsync(new(hospitalId, status, urgency, bloodType, sortBy, descending, page, pageSize), ct));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "HospitalOnly")]
    public async Task<ActionResult<BloodRequestView>> Update(Guid id, BloodRequestInput input, CancellationToken ct)
    {
        var existing = await requests.GetAsync(id, ct); if (!existing.Succeeded) return Error(existing); if (!await CanAccess(existing.Value!.HospitalId, ct)) return Forbid();
        var result = await requests.UpdateAsync(id, input.ToContract(), CurrentUserId(), ct); return result.Succeeded ? Ok(result.Value) : Error(result);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "HospitalOnly")]
    public async Task<IActionResult> Close(Guid id, CancellationToken ct)
    {
        var existing = await requests.GetAsync(id, ct); if (!existing.Succeeded) return Error(existing); if (!await CanAccess(existing.Value!.HospitalId, ct)) return Forbid();
        var result = await requests.CancelAsync(id, CurrentUserId(), ct); return result.Succeeded ? NoContent() : Error(result);
    }

    [HttpPut("{id:guid}/status")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<BloodRequestView>> Transition(Guid id, RequestTransitionRequest input, [FromServices] IInventoryService inventory, [FromServices] LifeLinkDbContext db, CancellationToken ct)
    {
        // Stock removal and the status change commit together; the row lock stops two admins dispatching the same request twice.
        await using var transaction = input.Status == BloodRequestStatus.Dispatched && db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(ct) : null;
        if (transaction is not null) await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM lifelink.blood_requests WHERE \"Id\" = {id} FOR UPDATE", ct);
        if (input.Status == BloodRequestStatus.Dispatched)
        {
            // A dispatch must take the units out of stock, otherwise inventory and dashboards drift from reality.
            var existing = await requests.GetAsync(id, ct); if (!existing.Succeeded) return Error(existing);
            if (existing.Value!.Status != BloodRequestStatus.Approved) return Problem(statusCode: 409, title: "invalid_transition", detail: $"Cannot transition from {existing.Value.Status} to Dispatched.");
            var stock = await inventory.DispatchRequestAsync(id, CurrentUserId(), ct);
            if (!stock.Succeeded) return Problem(statusCode: stock.ErrorCode == "request_not_found" ? 404 : 409, title: stock.ErrorCode, detail: stock.ErrorMessage);
        }
        var result = await requests.TransitionAsync(id, input.Status, CurrentUserId(), input.Reason, ct); if (!result.Succeeded) return Error(result);
        if (transaction is not null) await transaction.CommitAsync(ct);
        return Ok(result.Value);
    }

    [HttpPost("{id:guid}/escalate")]
    [Authorize(Policy = "HospitalOnly")]
    public async Task<ActionResult<BloodRequestView>> Escalate(Guid id, EscalationRequest input, CancellationToken ct)
    {
        var existing = await requests.GetAsync(id, ct); if (!existing.Succeeded) return Error(existing); if (!await CanAccess(existing.Value!.HospitalId, ct)) return Forbid();
        var result = await requests.EscalateAsync(id, CurrentUserId(), input.Reason, ct); return result.Succeeded ? Ok(result.Value) : Error(result);
    }

    [HttpGet("{id:guid}/history")]
    public async Task<ActionResult<IReadOnlyList<RequestHistoryView>>> History(Guid id, CancellationToken ct)
    {
        var existing = await requests.GetAsync(id, ct); if (!existing.Succeeded) return Error(existing); if (!await CanAccess(existing.Value!.HospitalId, ct)) return Forbid();
        var result = await requests.HistoryAsync(id, ct); return Ok(result.Value);
    }

    [HttpGet("reports/summary")]
    public async Task<ActionResult<RequestSummary>> Summary([FromQuery] Guid? hospitalId, CancellationToken ct)
    {
        if (!IsAdmin() && !IsHospital()) return Forbid();
        if (!IsAdmin()) { var own = await requests.GetHospitalIdForStaffAsync(CurrentUserId(), ct); if (!own.Succeeded) return Error(own); hospitalId = own.Value; }
        return Ok(await requests.SummaryAsync(hospitalId, ct));
    }

    private bool IsAdmin() => User.IsInRole(UserRole.BloodBankAdmin.ToString());
    private bool IsHospital() => User.IsInRole(UserRole.HospitalRequester.ToString());
    private async Task<bool> CanAccess(Guid hospitalId, CancellationToken ct) { if (IsAdmin()) return true; var own = await requests.GetHospitalIdForStaffAsync(CurrentUserId(), ct); return own.Succeeded && own.Value == hospitalId; }
    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private ObjectResult Error<T>(RequestResult<T> result) => result.ErrorCode switch { "request_not_found" or "hospital_not_found" => Problem(statusCode: 404, title: result.ErrorCode, detail: result.ErrorMessage), "invalid_transition" or "request_not_editable" or "request_closed" or "request_not_escalatable" or "request_not_cancellable" or "workflow_pending" => Problem(statusCode: 409, title: result.ErrorCode, detail: result.ErrorMessage), _ => Problem(statusCode: 400, title: result.ErrorCode, detail: result.ErrorMessage) };
}

public sealed record BloodRequestInput([EnumDataType(typeof(BloodType), ErrorMessage = "Blood type is not one of the allowed values.")] BloodType BloodType, [Range(1, 100, ErrorMessage = "Quantity units must be between 1 and 100.")] int QuantityUnits, [EnumDataType(typeof(RequestUrgency), ErrorMessage = "Requested urgency is not one of the allowed values.")] RequestUrgency RequestedUrgency, [MaxLength(2000, ErrorMessage = "Notes must be at most 2000 characters.")] string? Notes, DateTimeOffset RequiredByUtc)
{ public RequestInput ToContract() => new(BloodType, QuantityUnits, RequestedUrgency, Notes, RequiredByUtc); }
public sealed record RequestTransitionRequest([EnumDataType(typeof(BloodRequestStatus), ErrorMessage = "Status is not one of the allowed values.")] BloodRequestStatus Status, [MaxLength(1000, ErrorMessage = "Reason must be at most 1000 characters.")] string? Reason);
public sealed record EscalationRequest([MaxLength(1000, ErrorMessage = "Reason must be at most 1000 characters.")] string? Reason);
