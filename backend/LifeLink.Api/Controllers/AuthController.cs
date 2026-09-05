using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using LifeLink.Application.Auth;
using LifeLink.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LifeLink.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(IAuthService authService) : ControllerBase
{
    [AllowAnonymous]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("auth")]
    [HttpPost("register")]
    public async Task<ActionResult<TokenPair>> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        // Only accept the role name: Enum.TryParse would also take "0" or "Donor,BloodBankAdmin".
        if (!Enum.GetNames<UserRole>().Contains(request.Role.Trim(), StringComparer.OrdinalIgnoreCase) || !Enum.TryParse<UserRole>(request.Role.Trim(), true, out var role)) return BadRequestProblem("invalid_role", "Role must be Donor or HospitalRequester.");
        var result = await authService.RegisterAsync(new RegisterCommand(request.Email, request.Password, role, request.DeviceName), cancellationToken);
        return ToActionResult(result, StatusCodes.Status201Created);
    }

    [AllowAnonymous]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("auth")]
    [HttpPost("login")]
    public async Task<ActionResult<TokenPair>> Login(LoginRequest request, CancellationToken cancellationToken) => ToActionResult(await authService.LoginAsync(new LoginCommand(request.Email, request.Password, request.DeviceName), cancellationToken));

    [AllowAnonymous]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("refresh")]
    [HttpPost("refresh")]
    public async Task<ActionResult<TokenPair>> Refresh(RefreshRequest request, CancellationToken cancellationToken) => ToActionResult(await authService.RefreshAsync(new RefreshCommand(request.RefreshToken, request.DeviceName), cancellationToken));

    [AllowAnonymous]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("refresh")]
    [HttpPost("revoke")]
    public async Task<IActionResult> Revoke(RevokeRequest request, CancellationToken cancellationToken) => await authService.RevokeAsync(request.RefreshToken, cancellationToken) ? NoContent() : BadRequestProblem("invalid_refresh_token", "Refresh token is invalid or already revoked.");

    [Authorize]
    [HttpGet("me")]
    public ActionResult<AuthenticatedUser> Me()
    {
        var idText = User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        var email = User.FindFirstValue(JwtRegisteredClaimNames.Email) ?? User.FindFirstValue(ClaimTypes.Email);
        var roleText = User.FindFirstValue(ClaimTypes.Role);
        if (!Guid.TryParse(idText, out var id) || email is null || !Enum.TryParse<UserRole>(roleText, out var role)) return Unauthorized();
        return Ok(new AuthenticatedUser(id, email, role));
    }

    private ActionResult<TokenPair> ToActionResult(AuthResult<TokenPair> result, int successStatus = StatusCodes.Status200OK)
    {
        if (result.Succeeded) return StatusCode(successStatus, result.Value);
        return result.ErrorCode switch
        {
            "email_exists" => ConflictProblem(result.ErrorCode, result.ErrorMessage!),
            "invalid_credentials" or "invalid_refresh_token" => UnauthorizedProblem(result.ErrorCode, result.ErrorMessage!),
            _ => BadRequestProblem(result.ErrorCode!, result.ErrorMessage!)
        };
    }

    private ObjectResult BadRequestProblem(string code, string detail) => Problem(statusCode: 400, title: code, detail: detail);
    private ObjectResult UnauthorizedProblem(string code, string detail) => Problem(statusCode: 401, title: code, detail: detail);
    private ObjectResult ConflictProblem(string code, string detail) => Problem(statusCode: 409, title: code, detail: detail);
}

public sealed record RegisterRequest([Required(ErrorMessage = "Email is required."), EmailAddress(ErrorMessage = "Enter a valid email address, for example name@example.com."), MaxLength(320, ErrorMessage = "Email must be at most 320 characters.")] string Email, [Required(ErrorMessage = "Password is required."), MaxLength(256, ErrorMessage = "Password must be at most 256 characters.")] string Password, [Required(ErrorMessage = "Role is required."), MaxLength(40, ErrorMessage = "Role must be Donor or HospitalRequester.")] string Role, [MaxLength(200, ErrorMessage = "Device name must be at most 200 characters.")] string? DeviceName);
public sealed record LoginRequest([Required(ErrorMessage = "Email is required."), EmailAddress(ErrorMessage = "Enter a valid email address, for example name@example.com."), MaxLength(320, ErrorMessage = "Email must be at most 320 characters.")] string Email, [Required(ErrorMessage = "Password is required."), MaxLength(256, ErrorMessage = "Password must be at most 256 characters.")] string Password, [MaxLength(200, ErrorMessage = "Device name must be at most 200 characters.")] string? DeviceName);
public sealed record RefreshRequest([Required(ErrorMessage = "Refresh token is required."), MaxLength(200, ErrorMessage = "Refresh token is invalid.")] string RefreshToken, [MaxLength(200, ErrorMessage = "Device name must be at most 200 characters.")] string? DeviceName);
public sealed record RevokeRequest([Required(ErrorMessage = "Refresh token is required."), MaxLength(200, ErrorMessage = "Refresh token is invalid.")] string RefreshToken);
