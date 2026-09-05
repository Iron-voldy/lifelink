using LifeLink.Domain.Entities;
using LifeLink.Domain.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LifeLink.Infrastructure.Persistence;

public sealed class BootstrapOptions
{
    public bool Enabled { get; set; }
    public string AdminEmail { get; set; } = string.Empty;
    public string AdminPassword { get; set; } = string.Empty;
}

public sealed class AdminBootstrapper(IServiceScopeFactory scopes, IOptions<BootstrapOptions> options, ILogger<AdminBootstrapper> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var value = options.Value;
        if (!value.Enabled) return;
        if (string.IsNullOrWhiteSpace(value.AdminEmail) || !IsStrong(value.AdminPassword)) throw new InvalidOperationException("Bootstrap admin email and a strong 12+ character password are required when bootstrap is enabled.");
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LifeLinkDbContext>();
        var email = value.AdminEmail.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(x => x.Email == email, cancellationToken)) return;
        var user = new User(email, string.Empty, UserRole.BloodBankAdmin);
        user.SetPasswordHash(new PasswordHasher<User>().HashPassword(user, value.AdminPassword));
        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Bootstrapped the configured blood-bank administrator account {AdminEmail}", email);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    private static bool IsStrong(string value) => value.Length >= 12 && value.Any(char.IsUpper) && value.Any(char.IsLower) && value.Any(char.IsDigit) && value.Any(x => !char.IsLetterOrDigit(x));
}
