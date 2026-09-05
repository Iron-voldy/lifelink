using System.Text.Json;
using LifeLink.Application.Workflows;
using LifeLink.Domain.Enums;
using LifeLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace LifeLink.Tests.Infrastructure;

public sealed class DemoDataSeederTests
{
    private static (DemoDataSeeder Seeder, LifeLinkDbContext Db, ServiceProvider Provider) Create(string environment = "Development")
    {
        var db = new LifeLinkDbContext(new DbContextOptionsBuilder<LifeLinkDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options);
        var provider = new ServiceCollection().AddSingleton(db).BuildServiceProvider();
        var seeder = new DemoDataSeeder(provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new DemoDataOptions { Enabled = true, Password = "Demo!Pass12345" }),
            new HostingEnvironment { EnvironmentName = environment }, NullLogger<DemoDataSeeder>.Instance);
        return (seeder, db, provider);
    }

    [Fact]
    public async Task SeedsAtLeastTenRowsForEveryTableAndRoleAndIsIdempotent()
    {
        var (seeder, db, provider) = Create();
        await using var _ = provider;
        await seeder.StartAsync(default);
        await seeder.StartAsync(default);

        foreach (var role in Enum.GetValues<UserRole>())
            Assert.True(await db.Users.CountAsync(x => x.Role == role) >= 10, $"{role} users");
        var counts = new Dictionary<string, int>
        {
            ["donors"] = await db.Donors.CountAsync(), ["eligibility history"] = await db.DonorEligibilityHistory.CountAsync(),
            ["donation records"] = await db.DonationRecords.CountAsync(), ["hospitals"] = await db.Hospitals.CountAsync(),
            ["hospital staff"] = await db.HospitalStaff.CountAsync(), ["requests"] = await db.BloodRequests.CountAsync(),
            ["request history"] = await db.RequestStatusHistory.CountAsync(), ["locations"] = await db.BloodBankLocations.CountAsync(),
            ["lots"] = await db.InventoryLots.CountAsync(), ["reservations"] = await db.InventoryReservations.CountAsync(),
            ["dispatches"] = await db.DispatchRecords.CountAsync(), ["camps"] = await db.DonationCamps.CountAsync(),
            ["slots"] = await db.CampSlots.CountAsync(), ["notifications"] = await db.Notifications.CountAsync(),
            ["device tokens"] = await db.DeviceTokens.CountAsync(), ["workflows"] = await db.AgentWorkflowExecutions.CountAsync(),
            ["agent steps"] = await db.AgentSteps.CountAsync(), ["approvals"] = await db.AgentApprovals.CountAsync(),
        };
        Assert.All(counts, x => Assert.True(x.Value >= 10, $"{x.Key}: {x.Value}"));
        // Second start must not duplicate anything.
        Assert.Equal(52, await db.Users.CountAsync());
    }

    [Fact]
    public async Task PendingApprovalWorkflowsCarryAnOutcomeTheApprovalFlowCanApply()
    {
        var (seeder, db, provider) = Create();
        await using var _ = provider;
        await seeder.StartAsync(default);
        var pending = await db.AgentWorkflowExecutions.Where(x => x.Status == WorkflowStatus.PendingApproval).ToListAsync();
        Assert.NotEmpty(pending);
        foreach (var workflow in pending)
        {
            var request = await db.BloodRequests.SingleAsync(x => x.Id == workflow.BloodRequestId);
            var outcome = JsonSerializer.Deserialize<AgentRunResponse>(workflow.FinalOutcomeJson!, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            Assert.NotNull(outcome);
            Assert.Equal(BloodRequestStatus.PendingApproval, request.Status);
            Assert.InRange(outcome!.ProposedReservationUnits, 1, request.QuantityUnits);
        }
    }

    [Fact]
    public async Task RefusesToSeedInProduction()
    {
        var (seeder, _, provider) = Create("Production");
        await using var __ = provider;
        await Assert.ThrowsAsync<InvalidOperationException>(() => seeder.StartAsync(default));
    }
}
