using LifeLink.Application.Auth;
using LifeLink.Domain.Enums;
using LifeLink.Infrastructure.Authentication;
using LifeLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LifeLink.Tests.Authentication;

public sealed class AuthServiceTests
{
    private static (AuthService Service, LifeLinkDbContext Db) CreateService()
    {
        var dbOptions = new DbContextOptionsBuilder<LifeLinkDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var db = new LifeLinkDbContext(dbOptions);
        var jwt = Options.Create(new JwtOptions { SigningKey = "test-signing-key-at-least-thirty-two-characters", AccessTokenMinutes = 5, RefreshTokenDays = 1 });
        return (new AuthService(db, jwt), db);
    }

    [Fact]
    public async Task Register_DonorHashesPasswordAndIssuesTokens()
    {
        var (service, db) = CreateService();
        var result = await service.RegisterAsync(new RegisterCommand("DONOR@example.com", "StrongPassword!42", UserRole.Donor, "test"), default);
        Assert.True(result.Succeeded);
        Assert.NotEmpty(result.Value!.AccessToken);
        Assert.NotEmpty(result.Value.RefreshToken);
        var user = await db.Users.SingleAsync();
        Assert.Equal("donor@example.com", user.Email);
        Assert.NotEqual("StrongPassword!42", user.PasswordHash);
    }

    [Fact]
    public async Task Register_AdminIsRejected()
    {
        var (service, _) = CreateService();
        var result = await service.RegisterAsync(new RegisterCommand("admin@example.com", "StrongPassword!42", UserRole.BloodBankAdmin, null), default);
        Assert.False(result.Succeeded);
        Assert.Equal("role_not_self_registerable", result.ErrorCode);
    }

    [Fact]
    public async Task Login_WithWrongPasswordDoesNotIssueTokens()
    {
        var (service, _) = CreateService();
        await service.RegisterAsync(new RegisterCommand("donor@example.com", "StrongPassword!42", UserRole.Donor, null), default);
        var result = await service.LoginAsync(new LoginCommand("donor@example.com", "WrongPassword!42", null), default);
        Assert.False(result.Succeeded);
        Assert.Equal("invalid_credentials", result.ErrorCode);
    }

    [Fact]
    public async Task Refresh_RotatesTokenAndRejectsReplay()
    {
        var (service, db) = CreateService();
        var registration = await service.RegisterAsync(new RegisterCommand("donor@example.com", "StrongPassword!42", UserRole.Donor, null), default);
        var refreshed = await service.RefreshAsync(new RefreshCommand(registration.Value!.RefreshToken, null), default);
        var replay = await service.RefreshAsync(new RefreshCommand(registration.Value.RefreshToken, null), default);
        Assert.True(refreshed.Succeeded);
        Assert.NotEqual(registration.Value.RefreshToken, refreshed.Value!.RefreshToken);
        Assert.False(replay.Succeeded);
        Assert.Equal(2, await db.RefreshTokens.CountAsync());
        Assert.Single(await db.RefreshTokens.Where(x => x.RevokedAtUtc != null).ToListAsync());
    }
}
