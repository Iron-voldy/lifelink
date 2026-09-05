using LifeLink.Api.Infrastructure;
using LifeLink.Application.Auth;
using LifeLink.Domain.Enums;
using LifeLink.Infrastructure.Authentication;
using LifeLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace LifeLink.Tests.Authentication;

public sealed class AuthSecurityHardeningTests
{
    private static (AuthService Service, LifeLinkDbContext Db) CreateService()
    {
        var db = new LifeLinkDbContext(new DbContextOptionsBuilder<LifeLinkDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var jwt = Options.Create(new JwtOptions { SigningKey = "test-signing-key-at-least-thirty-two-characters", AccessTokenMinutes = 5, RefreshTokenDays = 1 });
        return (new AuthService(db, jwt), db);
    }

    [Fact]
    public async Task Refresh_ReplayAfterGraceWindowRevokesEverySessionOfTheUser()
    {
        var (service, db) = CreateService();
        var first = (await service.RegisterAsync(new RegisterCommand("donor@example.com", "StrongPassword!42", UserRole.Donor, null), default)).Value!;
        var second = (await service.LoginAsync(new LoginCommand("donor@example.com", "StrongPassword!42", "other device"), default)).Value!;
        var rotated = (await service.RefreshAsync(new RefreshCommand(first.RefreshToken, null), default)).Value!;

        // Pretend the rotation happened long ago, so a new presentation of the old token is theft, not a client race.
        var old = await db.RefreshTokens.SingleAsync(x => x.ReplacedByTokenHash != null);
        db.Entry(old).Property(x => x.RevokedAtUtc).CurrentValue = DateTimeOffset.UtcNow - AuthService.ReuseGracePeriod - TimeSpan.FromMinutes(1);
        await db.SaveChangesAsync();

        var replay = await service.RefreshAsync(new RefreshCommand(first.RefreshToken, null), default);
        Assert.Equal("invalid_refresh_token", replay.ErrorCode);
        Assert.False((await service.RefreshAsync(new RefreshCommand(rotated.RefreshToken, null), default)).Succeeded);
        Assert.False((await service.RefreshAsync(new RefreshCommand(second.RefreshToken, null), default)).Succeeded);
        Assert.Empty(await db.RefreshTokens.Where(x => x.RevokedAtUtc == null).ToListAsync());
    }

    [Fact]
    public async Task Refresh_ReplayInsideGraceWindowOnlyFailsAndKeepsTheNewSession()
    {
        var (service, _) = CreateService();
        var first = (await service.RegisterAsync(new RegisterCommand("donor@example.com", "StrongPassword!42", UserRole.Donor, null), default)).Value!;
        var rotated = (await service.RefreshAsync(new RefreshCommand(first.RefreshToken, null), default)).Value!;

        Assert.False((await service.RefreshAsync(new RefreshCommand(first.RefreshToken, null), default)).Succeeded);
        Assert.True((await service.RefreshAsync(new RefreshCommand(rotated.RefreshToken, null), default)).Succeeded);
    }

    [Fact]
    public async Task Login_UnknownEmailFailsWithTheSameErrorAsWrongPassword()
    {
        var (service, _) = CreateService();
        await service.RegisterAsync(new RegisterCommand("donor@example.com", "StrongPassword!42", UserRole.Donor, null), default);
        var unknown = await service.LoginAsync(new LoginCommand("nobody@example.com", "StrongPassword!42", null), default);
        var wrong = await service.LoginAsync(new LoginCommand("donor@example.com", "WrongPassword!42", null), default);
        Assert.Equal("invalid_credentials", unknown.ErrorCode);
        Assert.Equal(wrong.ErrorCode, unknown.ErrorCode);
    }

    [Theory]
    [InlineData("local-only-signing-key-change-before-deployment-2026", "a-random-agent-key-0123456789", "Host=db;Password=Xk9random")]
    [InlineData("a-real-random-signing-key-with-plenty-of-length-1234", "local-agent-key-change-me", "Host=db;Password=Xk9random")]
    [InlineData("a-real-random-signing-key-with-plenty-of-length-1234", "short-key", "Host=db;Password=Xk9random")]
    [InlineData("a-real-random-signing-key-with-plenty-of-length-1234", "a-random-agent-key-0123456789", "Host=db;Password=lifelink_local_only")]
    public void StartupChecks_RejectSecretsShippedAsRepositoryDefaults(string signingKey, string agentKey, string connectionString)
    {
        var configuration = Configuration(agentKey, connectionString);
        Assert.Throws<InvalidOperationException>(() => StartupSecurityChecks.Validate(configuration, new TestEnvironment("Production"), new JwtOptions { SigningKey = signingKey }));
    }

    [Fact]
    public void StartupChecks_AcceptRandomSecretsAndAllowDefaultsInDevelopment()
    {
        StartupSecurityChecks.Validate(Configuration("a-random-agent-key-0123456789", "Host=db;Password=Xk9random"), new TestEnvironment("Staging"), new JwtOptions { SigningKey = "a-real-random-signing-key-with-plenty-of-length-1234" });
        StartupSecurityChecks.Validate(Configuration("local-agent-key-change-me", "Host=db;Password=lifelink_local_only"), new TestEnvironment("Development"), new JwtOptions { SigningKey = "local-only-signing-key-change-before-deployment-2026" });
    }

    private static IConfiguration Configuration(string agentKey, string connectionString) => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?> { ["AgentService:InternalApiKey"] = agentKey, ["ConnectionStrings:LifeLink"] = connectionString }).Build();

    private sealed class TestEnvironment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "test";
        public string ContentRootPath { get; set; } = ".";
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
