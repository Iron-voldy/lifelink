using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using LifeLink.Application.Donors;
using LifeLink.Application.Requests;
using LifeLink.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LifeLink.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/hospitals")]
public sealed class HospitalsController(IRequestService requests) : ControllerBase
{
    [HttpPost("register")]
    [Authorize(Policy = "HospitalOnly")]
    public async Task<ActionResult<HospitalView>> Register(HospitalRegistrationRequest input, CancellationToken ct)
    {
        var result = await requests.RegisterHospitalAsync(CurrentUserId(), new(input.Name, input.RegistrationNumber, input.Address, input.Latitude, input.Longitude, input.Position), ct);
        return result.Succeeded ? StatusCode(201, result.Value) : Error(result);
    }

    [HttpGet("me")]
    [Authorize(Policy = "HospitalOnly")]
    public async Task<ActionResult<HospitalView>> Mine(CancellationToken ct)
    {
        var result = await requests.GetHospitalForStaffAsync(CurrentUserId(), ct);
        return result.Succeeded ? Ok(result.Value) : Problem(statusCode: 404, title: result.ErrorCode, detail: result.ErrorMessage);
    }

    [HttpGet]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<PagedResult<HospitalView>>> List([FromQuery] VerificationStatus? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => Ok(await requests.ListHospitalsAsync(status, page, pageSize, ct));

    [HttpPut("{id:guid}/verification")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<HospitalView>> Verify(Guid id, HospitalVerificationRequest input, CancellationToken ct)
    {
        var result = await requests.SetHospitalVerificationAsync(id, input.Status, ct);
        return result.Succeeded ? Ok(result.Value) : Error(result);
    }

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private ObjectResult Error<T>(RequestResult<T> result) => result.ErrorCode switch { "hospital_not_found" => Problem(statusCode: 404, title: result.ErrorCode, detail: result.ErrorMessage), "registration_exists" or "staff_profile_exists" => Problem(statusCode: 409, title: result.ErrorCode, detail: result.ErrorMessage), _ => Problem(statusCode: 400, title: result.ErrorCode, detail: result.ErrorMessage) };
}

public sealed record HospitalRegistrationRequest([Required(ErrorMessage = "Name is required."), MaxLength(250, ErrorMessage = "Name must be at most 250 characters.")] string Name, [Required(ErrorMessage = "Registration number is required."), MaxLength(100, ErrorMessage = "Registration number must be at most 100 characters.")] string RegistrationNumber, [Required(ErrorMessage = "Address is required."), MaxLength(500, ErrorMessage = "Address must be at most 500 characters.")] string Address, [Range(-90, 90, ErrorMessage = "Latitude must be between -90 and 90.")] decimal? Latitude, [Range(-180, 180, ErrorMessage = "Longitude must be between -180 and 180.")] decimal? Longitude, [Required(ErrorMessage = "Position is required."), MaxLength(150, ErrorMessage = "Position must be at most 150 characters.")] string Position);
public sealed record HospitalVerificationRequest([EnumDataType(typeof(VerificationStatus), ErrorMessage = "Status is not one of the allowed values.")] VerificationStatus Status);
