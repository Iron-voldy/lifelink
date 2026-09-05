using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using LifeLink.Application.Donors;
using LifeLink.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LifeLink.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/donors")]
public sealed class DonorsController(IDonorService donors) : ControllerBase
{
    [HttpPost]
    [Authorize(Policy = "DonorOnly")]
    public async Task<ActionResult<DonorView>> Create(DonorProfileRequest request, CancellationToken ct)
    {
        var result = await donors.CreateAsync(CurrentUserId(), request.ToInput(), ct);
        return result.Succeeded ? CreatedAtAction(nameof(Get), new { id = result.Value!.Id }, result.Value) : DonorError(result);
    }

    [HttpGet("me")]
    [Authorize(Policy = "DonorOnly")]
    public async Task<ActionResult<DonorView>> GetMine(CancellationToken ct)
    {
        var result = await donors.GetByUserIdAsync(CurrentUserId(), ct);
        return result.Succeeded ? Ok(result.Value) : DonorError(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<DonorView>> Get(Guid id, CancellationToken ct)
    {
        var result = await donors.GetAsync(id, ct);
        if (!result.Succeeded) return DonorError(result);
        return CanAccess(result.Value!) ? Ok(result.Value) : Forbid();
    }

    [HttpGet]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<PagedResult<DonorView>>> List([FromQuery] string? search, [FromQuery] BloodType? bloodType, [FromQuery] EligibilityStatus? eligibilityStatus, [FromQuery] bool includeInactive = false, [FromQuery] string sortBy = "created", [FromQuery] bool descending = false, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => Ok(await donors.ListAsync(new DonorQuery(search, bloodType, eligibilityStatus, includeInactive, sortBy, descending, page, pageSize), ct));

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<DonorView>> Update(Guid id, DonorProfileRequest request, CancellationToken ct)
    {
        var existing = await donors.GetAsync(id, ct);
        if (!existing.Succeeded) return DonorError(existing);
        if (!CanAccess(existing.Value!)) return Forbid();
        var result = await donors.UpdateAsync(id, request.ToInput(), ct);
        return result.Succeeded ? Ok(result.Value) : DonorError(result);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
    {
        var result = await donors.DeactivateAsync(id, ct);
        return result.Succeeded ? NoContent() : DonorError(result);
    }

    [HttpPost("{id:guid}/check-eligibility")]
    public async Task<ActionResult<EligibilityEvaluation>> CheckEligibility(Guid id, CancellationToken ct)
    {
        var existing = await donors.GetAsync(id, ct);
        if (!existing.Succeeded) return DonorError(existing);
        if (!CanAccess(existing.Value!)) return Forbid();
        var result = await donors.EvaluateEligibilityAsync(id, CurrentUserId(), User.IsInRole(UserRole.BloodBankAdmin.ToString()), ct);
        return result.Succeeded ? Ok(result.Value) : DonorError(result);
    }

    [HttpGet("{id:guid}/eligibility-history")]
    public async Task<ActionResult<IReadOnlyList<EligibilityHistoryView>>> EligibilityHistory(Guid id, CancellationToken ct)
    {
        var existing = await donors.GetAsync(id, ct);
        if (!existing.Succeeded) return DonorError(existing);
        if (!CanAccess(existing.Value!)) return Forbid();
        var result = await donors.GetEligibilityHistoryAsync(id, ct);
        return Ok(result.Value);
    }

    [HttpPost("{id:guid}/donations")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<DonationRecordView>> RecordDonation(Guid id, DonationRecordRequest request, CancellationToken ct)
    {
        var result = await donors.RecordDonationAsync(id, new(request.DonationDate!.Value, request.Units, request.Location, request.Notes), ct);
        return result.Succeeded ? CreatedAtAction(nameof(DonationHistory), new { id }, result.Value) : DonorError(result);
    }

    [HttpGet("{id:guid}/donation-history")]
    public async Task<ActionResult<IReadOnlyList<DonationRecordView>>> DonationHistory(Guid id, CancellationToken ct)
    {
        var existing = await donors.GetAsync(id, ct);
        if (!existing.Succeeded) return DonorError(existing);
        if (!CanAccess(existing.Value!)) return Forbid();
        var result = await donors.GetDonationHistoryAsync(id, ct);
        return Ok(result.Value);
    }

    private bool CanAccess(DonorView donor) => User.IsInRole(UserRole.BloodBankAdmin.ToString()) || donor.UserId == CurrentUserId();
    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new InvalidOperationException("User identifier claim is missing."));
    private ObjectResult DonorError<T>(DonorResult<T> result) => result.ErrorCode switch { "donor_not_found" => NotFoundProblem(result.ErrorCode, result.ErrorMessage!), "profile_exists" or "donation_exists" => Problem(statusCode: 409, title: result.ErrorCode, detail: result.ErrorMessage), _ => Problem(statusCode: 400, title: result.ErrorCode, detail: result.ErrorMessage) };
    private ObjectResult NotFoundProblem(string code, string detail) => Problem(statusCode: 404, title: code, detail: detail);
}

public sealed record DonorProfileRequest([Required(ErrorMessage = "Blood type is required."), EnumDataType(typeof(BloodType), ErrorMessage = "Blood type is not one of the allowed values.")] BloodType? BloodType, [Required(ErrorMessage = "Date of birth is required.")] DateOnly? DateOfBirth,[Required(ErrorMessage = "Address is required."), MaxLength(500, ErrorMessage = "Address must be at most 500 characters.")] string Address, [Range(-90, 90, ErrorMessage = "Latitude must be between -90 and 90.")] decimal? Latitude, [Range(-180, 180, ErrorMessage = "Longitude must be between -180 and 180.")] decimal? Longitude, IReadOnlyList<string>? MedicalFlags)
{
    public DonorProfileInput ToInput() => new(BloodType!.Value, DateOfBirth!.Value, Address, Latitude, Longitude, MedicalFlags ?? []);
}
public sealed record DonationRecordRequest([Required(ErrorMessage = "Donation date is required.")] DateOnly? DonationDate, [Range(1, 4, ErrorMessage = "Units must be between 1 and 4.")] int Units, [Required(ErrorMessage = "Location is required."), MaxLength(500, ErrorMessage = "Location must be at most 500 characters.")] string Location, [MaxLength(1000, ErrorMessage = "Notes must be at most 1000 characters.")] string? Notes);
