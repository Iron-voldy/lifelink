using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using LifeLink.Application.Donors;
using LifeLink.Application.Inventory;
using LifeLink.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LifeLink.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/inventory")]
public sealed class InventoryController(IInventoryService inventory) : ControllerBase
{
    [HttpPost("locations")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<LocationView>> CreateLocation(LocationRequest input, CancellationToken ct) { var result = await inventory.CreateLocationAsync(input.ToContract(), ct); return result.Succeeded ? StatusCode(201, result.Value) : Error(result); }

    [HttpPut("locations/{id:guid}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<LocationView>> UpdateLocation(Guid id, LocationRequest input, CancellationToken ct) { var result = await inventory.UpdateLocationAsync(id, input.ToContract(), ct); return result.Succeeded ? Ok(result.Value) : Error(result); }

    [HttpGet("locations")]
    public async Task<ActionResult<IReadOnlyList<LocationView>>> Locations(CancellationToken ct) => Ok(await inventory.ListLocationsAsync(ct));

    [HttpPost("stock-in")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<InventoryLotView>> StockIn(StockInRequest input, CancellationToken ct) { var result = await inventory.StockInAsync(new(input.LocationId, input.BloodType, input.Units, input.ExpiryDate, input.Source), ct); return result.Succeeded ? StatusCode(201, result.Value) : Error(result); }

    [HttpPut("{id:guid}/adjust")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<InventoryLotView>> Adjust(Guid id, AdjustStockRequest input, CancellationToken ct) { var result = await inventory.AdjustAsync(id, input.UnitsAvailable, input.ExpectedVersion, ct); return result.Succeeded ? Ok(result.Value) : Error(result); }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<InventoryLotView>> Quarantine(Guid id, CancellationToken ct) { var result = await inventory.QuarantineAsync(id, ct); return result.Succeeded ? Ok(result.Value) : Error(result); }

    [HttpGet]
    public async Task<ActionResult<PagedResult<InventoryLotView>>> List([FromQuery] Guid? locationId, [FromQuery] BloodType? bloodType, [FromQuery] InventoryLotStatus? status, [FromQuery] bool includeExpired = false, [FromQuery] string sortBy = "expiry", [FromQuery] bool descending = false, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => Ok(await inventory.ListAsync(new(locationId, bloodType, status, includeExpired, sortBy, descending, page, pageSize), ct));

    [HttpGet("expiring-soon")]
    public async Task<ActionResult<IReadOnlyList<InventoryLotView>>> ExpiringSoon([FromQuery] int days = 7, CancellationToken ct = default) => Ok(await inventory.ExpiringSoonAsync(days, ct));

    [HttpGet("compatibility/{recipientType}")]
    public ActionResult<CompatibilityView> Compatibility(BloodType recipientType) => Ok(inventory.Compatibility(recipientType));

    [HttpPost("reserve")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<ReservationPlan>> Reserve(ReserveRequest input, CancellationToken ct) { var result = await inventory.ReserveAsync(new(input.BloodRequestId, input.Units, input.IdempotencyKey, input.HoldMinutes), ct); return result.Succeeded ? Ok(result.Value) : Error(result); }

    [HttpPost("reservations/{id:guid}/release")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<ReservationView>> Release(Guid id, ReleaseReservationRequest input, CancellationToken ct) { var result = await inventory.ReleaseAsync(id, input.Reason, ct); return result.Succeeded ? Ok(result.Value) : Error(result); }

    [HttpPost("reservations/{id:guid}/dispatch")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<ReservationView>> Dispatch(Guid id, CancellationToken ct) { var result = await inventory.DispatchAsync(id, CurrentUserId(), ct); return result.Succeeded ? Ok(result.Value) : Error(result); }

    [HttpPost("reservations/expire")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<object>> Expire(CancellationToken ct) => Ok(new { expiredCount = await inventory.ExpireReservationsAsync(ct) });

    [HttpGet("reports/stock-levels")]
    public async Task<ActionResult<IReadOnlyList<StockLevelView>>> Report(CancellationToken ct) => Ok(await inventory.StockReportAsync(ct));

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private ObjectResult Error<T>(InventoryResult<T> result) => result.ErrorCode switch { "location_not_found" or "lot_not_found" or "request_not_found" or "reservation_not_found" => Problem(statusCode: 404, title: result.ErrorCode, detail: result.ErrorMessage), "concurrency_conflict" or "invalid_lot_state" or "active_reservations" or "invalid_reservation_state" or "reservation_expired" or "request_closed" => Problem(statusCode: 409, title: result.ErrorCode, detail: result.ErrorMessage), _ => Problem(statusCode: 400, title: result.ErrorCode, detail: result.ErrorMessage) };
}

public sealed record LocationRequest([Required(ErrorMessage = "Name is required."), MaxLength(250, ErrorMessage = "Name must be at most 250 characters.")] string Name, [Required(ErrorMessage = "Address is required."), MaxLength(500, ErrorMessage = "Address must be at most 500 characters.")] string Address, [Range(-90, 90, ErrorMessage = "Latitude must be between -90 and 90.")] decimal Latitude, [Range(-180, 180, ErrorMessage = "Longitude must be between -180 and 180.")] decimal Longitude) { public LocationInput ToContract() => new(Name, Address, Latitude, Longitude); }
public sealed record StockInRequest(Guid LocationId, [EnumDataType(typeof(BloodType), ErrorMessage = "Blood type is not one of the allowed values.")] BloodType BloodType, [Range(1, 10000, ErrorMessage = "Units must be between 1 and 10000.")] int Units, DateOnly ExpiryDate, [Required(ErrorMessage = "Source is required."), MaxLength(200, ErrorMessage = "Source must be at most 200 characters.")] string Source);
public sealed record AdjustStockRequest([Range(0, 10000, ErrorMessage = "Units available must be between 0 and 10000.")] int UnitsAvailable, uint ExpectedVersion);
public sealed record ReserveRequest(Guid BloodRequestId, [Range(1, 100, ErrorMessage = "Units must be between 1 and 100.")] int Units, [Required(ErrorMessage = "Idempotency key is required."), MaxLength(64, ErrorMessage = "Idempotency key must be at most 64 characters.")] string IdempotencyKey, [Range(5, 1440, ErrorMessage = "Hold minutes must be between 5 and 1440.")] int HoldMinutes = 30);
public sealed record ReleaseReservationRequest([Required(ErrorMessage = "Reason is required."), MaxLength(500, ErrorMessage = "Reason must be at most 500 characters.")] string Reason);
