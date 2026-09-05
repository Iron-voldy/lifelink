using LifeLink.Domain.Enums;
using LifeLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace LifeLink.Tests.Authentication;

public sealed class AdminBootstrapperTests
{
    [Fact]
    public async Task EnabledBootstrapCreatesOneAdminAndIsIdempotent()
    {
        var services = new ServiceCollection();
        var database = new LifeLinkDbContext(new DbContextOptionsBuilder<LifeLinkDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        services.AddSingleton(database);
        await using var provider = services.BuildServiceProvider();
        var options = Options.Create(new BootstrapOptions { Enabled = true, AdminEmail = "ADMIN@LifeLink.Test", AdminPassword = "StrongAdmin!42" });
        var bootstrap = new AdminBootstrapper(provider.GetRequiredService<IServiceScopeFactory>(), options, NullLogger<AdminBootstrapper>.Instance);
        await bootstrap.StartAsync(default);
        await bootstrap.StartAsync(default);
        var users = await database.Users.ToListAsync();
        var admin = Assert.Single(users);
        Assert.Equal("admin@lifelink.test", admin.Email);
        Assert.Equal(UserRole.BloodBankAdmin, admin.Role);
    }
}
