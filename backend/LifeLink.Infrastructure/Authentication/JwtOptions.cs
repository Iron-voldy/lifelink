namespace LifeLink.Infrastructure.Authentication;
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";
    public string Issuer { get; init; } = "LifeLink";
    public string Audience { get; init; } = "LifeLink.Clients";
    public string SigningKey { get; init; } = string.Empty;
    public int AccessTokenMinutes { get; init; } = 15;
    public int RefreshTokenDays { get; init; } = 14;
}
