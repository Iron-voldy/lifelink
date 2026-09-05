using LifeLink.Domain.Common;
using LifeLink.Domain.Enums;

namespace LifeLink.Domain.Entities;

public sealed class User : Entity
{
    public string Email { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public UserRole Role { get; private set; }
    public bool IsActive { get; private set; } = true;
    public Donor? DonorProfile { get; private set; }
    public HospitalStaff? HospitalStaffProfile { get; private set; }
    private User() { }
    public User(string email, string passwordHash, UserRole role) { Email = email.Trim().ToLowerInvariant(); PasswordHash = passwordHash; Role = role; }
    public void SetPasswordHash(string passwordHash) { if (string.IsNullOrWhiteSpace(passwordHash)) throw new ArgumentException("Password hash is required.", nameof(passwordHash)); PasswordHash = passwordHash; UpdatedAtUtc = DateTimeOffset.UtcNow; }
}

public sealed class RefreshToken : Entity
{
    public Guid UserId { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public DateTimeOffset ExpiresAtUtc { get; private set; }
    public DateTimeOffset? RevokedAtUtc { get; private set; }
    public string? ReplacedByTokenHash { get; private set; }
    public string? DeviceName { get; private set; }
    private RefreshToken() { }
    public RefreshToken(Guid userId, string tokenHash, DateTimeOffset expiresAtUtc, string? deviceName) { UserId = userId; TokenHash = tokenHash; ExpiresAtUtc = expiresAtUtc; DeviceName = deviceName; }
    public bool IsUsable(DateTimeOffset now) => RevokedAtUtc is null && ExpiresAtUtc > now;
    public void Revoke(DateTimeOffset now, string? replacedByTokenHash = null) { RevokedAtUtc = now; ReplacedByTokenHash = replacedByTokenHash; UpdatedAtUtc = now; }
}

public sealed class DeviceToken : Entity
{
    public Guid UserId { get; private set; }
    public string Token { get; private set; } = string.Empty;
    public string Platform { get; private set; } = string.Empty;
    public DateTimeOffset LastSeenAtUtc { get; private set; } = DateTimeOffset.UtcNow;
    public bool IsActive { get; private set; } = true;
    private DeviceToken() { }
    public DeviceToken(Guid userId, string token, string platform) { UserId = userId; Token = token; Platform = platform.Trim(); }
    public void Touch(string platform, DateTimeOffset now) { Platform = platform.Trim(); LastSeenAtUtc = now; IsActive = true; UpdatedAtUtc = now; }
    public void Deactivate() { IsActive = false; UpdatedAtUtc = DateTimeOffset.UtcNow; }
}
