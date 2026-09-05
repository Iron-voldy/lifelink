using LifeLink.Domain.Enums;
namespace LifeLink.Application.Auth;
public sealed record RegisterCommand(string Email, string Password, UserRole Role, string? DeviceName);
public sealed record LoginCommand(string Email, string Password, string? DeviceName);
public sealed record RefreshCommand(string RefreshToken, string? DeviceName);
public sealed record TokenPair(string AccessToken, DateTimeOffset AccessTokenExpiresAtUtc, string RefreshToken, DateTimeOffset RefreshTokenExpiresAtUtc);
public sealed record AuthenticatedUser(Guid Id, string Email, UserRole Role);
public sealed record AuthResult<T>(T? Value, string? ErrorCode, string? ErrorMessage)
{
    public bool Succeeded => ErrorCode is null;
    public static AuthResult<T> Success(T value) => new(value, null, null);
    public static AuthResult<T> Failure(string code, string message) => new(default, code, message);
}
public interface IAuthService
{
    Task<AuthResult<TokenPair>> RegisterAsync(RegisterCommand command, CancellationToken cancellationToken);
    Task<AuthResult<TokenPair>> LoginAsync(LoginCommand command, CancellationToken cancellationToken);
    Task<AuthResult<TokenPair>> RefreshAsync(RefreshCommand command, CancellationToken cancellationToken);
    Task<bool> RevokeAsync(string refreshToken, CancellationToken cancellationToken);
}
