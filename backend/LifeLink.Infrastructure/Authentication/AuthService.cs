using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using LifeLink.Application.Auth;
using LifeLink.Domain.Entities;
using LifeLink.Domain.Enums;
using LifeLink.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace LifeLink.Infrastructure.Authentication;

public sealed class AuthService(LifeLinkDbContext db, IOptions<JwtOptions> options) : IAuthService
{
    private readonly JwtOptions _options = options.Value;
    private readonly PasswordHasher<User> _hasher = new();
    /// <summary>Concurrent refreshes from one client (several 401s at once) are a race, not theft; only later reuse revokes every session.</summary>
    public static readonly TimeSpan ReuseGracePeriod = TimeSpan.FromSeconds(30);
    private static readonly User TimingDummyUser = new("timing@invalid.local", string.Empty, UserRole.Donor);
    private static readonly string TimingDummyHash = new PasswordHasher<User>().HashPassword(TimingDummyUser, Convert.ToBase64String(RandomNumberGenerator.GetBytes(24)));

    public async Task<AuthResult<TokenPair>> RegisterAsync(RegisterCommand command, CancellationToken cancellationToken)
    {
        if (command.Role is not (UserRole.Donor or UserRole.HospitalRequester)) return AuthResult<TokenPair>.Failure("role_not_self_registerable", "Only donor and hospital requester accounts can self-register.");
        var email = command.Email.Trim().ToLowerInvariant();
        if (!IsValidEmail(email)) return AuthResult<TokenPair>.Failure("invalid_email", "A valid email address is required.");
        if (!IsStrongPassword(command.Password)) return AuthResult<TokenPair>.Failure("weak_password", "Password must be at least 12 characters and contain upper, lower, digit, and symbol characters.");
        if (await db.Users.AnyAsync(x => x.Email == email, cancellationToken)) return AuthResult<TokenPair>.Failure("email_exists", "An account already exists for this email address.");
        var user = new User(email, string.Empty, command.Role);
        user.SetPasswordHash(_hasher.HashPassword(user, command.Password));
        db.Users.Add(user);
        var pair = IssueTokenPair(user, command.DeviceName, out var refreshEntity);
        db.RefreshTokens.Add(refreshEntity);
        await db.SaveChangesAsync(cancellationToken);
        return AuthResult<TokenPair>.Success(pair);
    }

    public async Task<AuthResult<TokenPair>> LoginAsync(LoginCommand command, CancellationToken cancellationToken)
    {
        var email = command.Email.Trim().ToLowerInvariant();
        var user = await db.Users.SingleOrDefaultAsync(x => x.Email == email, cancellationToken);
        // Always run one hash verification so the response time does not reveal whether the email is registered.
        var verification = _hasher.VerifyHashedPassword(user ?? TimingDummyUser, user?.PasswordHash ?? TimingDummyHash, command.Password);
        if (user is null || !user.IsActive || verification == PasswordVerificationResult.Failed) return AuthResult<TokenPair>.Failure("invalid_credentials", "Email or password is incorrect.");
        var pair = IssueTokenPair(user, command.DeviceName, out var refreshEntity);
        db.RefreshTokens.Add(refreshEntity);
        await db.SaveChangesAsync(cancellationToken);
        return AuthResult<TokenPair>.Success(pair);
    }

    public async Task<AuthResult<TokenPair>> RefreshAsync(RefreshCommand command, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var current = await db.RefreshTokens.SingleOrDefaultAsync(x => x.TokenHash == HashToken(command.RefreshToken), cancellationToken);
        if (current is null) return InvalidRefresh();
        if (!current.IsUsable(now))
        {
            // A rotated token presented again after the grace window was copied: end every session of that user.
            if (current.ReplacedByTokenHash is not null && current.RevokedAtUtc is { } rotatedAt && now - rotatedAt > ReuseGracePeriod) await RevokeAllForUserAsync(current.UserId, now, cancellationToken);
            return InvalidRefresh();
        }
        var user = await db.Users.SingleOrDefaultAsync(x => x.Id == current.UserId && x.IsActive, cancellationToken);
        if (user is null) return InvalidRefresh();
        var pair = IssueTokenPair(user, command.DeviceName ?? current.DeviceName, out var replacement);
        if (db.Database.IsRelational())
        {
            // Claim the token atomically so two concurrent refreshes with the same token cannot both mint sessions.
            var claimed = await db.RefreshTokens.Where(x => x.Id == current.Id && x.RevokedAtUtc == null)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAtUtc, now).SetProperty(x => x.ReplacedByTokenHash, replacement.TokenHash).SetProperty(x => x.UpdatedAtUtc, now), cancellationToken);
            if (claimed != 1) return InvalidRefresh();
            db.Entry(current).State = EntityState.Detached;
        }
        else current.Revoke(now, replacement.TokenHash);
        db.RefreshTokens.Add(replacement);
        await db.SaveChangesAsync(cancellationToken);
        return AuthResult<TokenPair>.Success(pair);
    }

    public async Task<bool> RevokeAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var current = await db.RefreshTokens.SingleOrDefaultAsync(x => x.TokenHash == HashToken(refreshToken), cancellationToken);
        if (current is null || current.RevokedAtUtc is not null) return false;
        current.Revoke(DateTimeOffset.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task RevokeAllForUserAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var active = await db.RefreshTokens.Where(x => x.UserId == userId && x.RevokedAtUtc == null).ToListAsync(cancellationToken);
        foreach (var token in active) token.Revoke(now);
        if (active.Count > 0) await db.SaveChangesAsync(cancellationToken);
    }

    private static AuthResult<TokenPair> InvalidRefresh() => AuthResult<TokenPair>.Failure("invalid_refresh_token", "Refresh token is invalid or expired.");

    private TokenPair IssueTokenPair(User user, string? deviceName, out RefreshToken refreshEntity)
    {
        if (_options.SigningKey.Length < 32) throw new InvalidOperationException("JWT signing key must be at least 32 characters.");
        var now = DateTimeOffset.UtcNow;
        var accessExpiry = now.AddMinutes(_options.AccessTokenMinutes);
        var credentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey)), SecurityAlgorithms.HmacSha256);
        var claims = new[] { new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()), new Claim(JwtRegisteredClaimNames.Email, user.Email), new Claim(ClaimTypes.Role, user.Role.ToString()), new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")) };
        var jwt = new JwtSecurityToken(_options.Issuer, _options.Audience, claims, now.UtcDateTime, accessExpiry.UtcDateTime, credentials);
        var rawRefresh = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        var refreshExpiry = now.AddDays(_options.RefreshTokenDays);
        refreshEntity = new RefreshToken(user.Id, HashToken(rawRefresh), refreshExpiry, deviceName);
        return new TokenPair(new JwtSecurityTokenHandler().WriteToken(jwt), accessExpiry, rawRefresh, refreshExpiry);
    }

    private static string HashToken(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    private static bool IsValidEmail(string email) => email.Length <= 320 && System.Net.Mail.MailAddress.TryCreate(email, out var parsed) && parsed.Address == email;
    private static bool IsStrongPassword(string value) => value.Length >= 12 && value.Any(char.IsUpper) && value.Any(char.IsLower) && value.Any(char.IsDigit) && value.Any(ch => !char.IsLetterOrDigit(ch));
}
