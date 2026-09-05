using LifeLink.Domain.Entities;
using LifeLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LifeLink.Tests.Infrastructure;

public sealed class LifeLinkDbContextTests
{
    private static LifeLinkDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<LifeLinkDbContext>()
            .UseNpgsql("Host=localhost;Database=model_only;Username=test;Password=test")
            .Options;
        return new LifeLinkDbContext(options);
    }

    [Fact]
    public void Model_ContainsAllCoreAggregateTables()
    {
        using var context = CreateContext();
        var entityTypes = context.Model.GetEntityTypes().Select(x => x.ClrType).ToHashSet();
        Assert.Contains(typeof(User), entityTypes);
        Assert.Contains(typeof(Donor), entityTypes);
        Assert.Contains(typeof(BloodRequest), entityTypes);
        Assert.Contains(typeof(InventoryLot), entityTypes);
        Assert.Contains(typeof(DonationCamp), entityTypes);
        Assert.Contains(typeof(AgentWorkflowExecution), entityTypes);
        Assert.Contains(typeof(DonationRecord), entityTypes);
        Assert.Equal(20, entityTypes.Count);
    }

    [Fact]
    public void Model_EnforcesUniqueWorkflowAttemptPerRequest()
    {
        using var context = CreateContext();
        var workflow = context.Model.FindEntityType(typeof(AgentWorkflowExecution));
        var index = workflow!.GetIndexes().Single(x => x.Properties.Select(p => p.Name).SequenceEqual(["BloodRequestId", "AttemptNumber"]));
        Assert.True(index.IsUnique);
    }

    [Fact]
    public void Model_EnforcesUniqueInventoryReservationIdempotencyKey()
    {
        using var context = CreateContext();
        var reservation = context.Model.FindEntityType(typeof(InventoryReservation));
        var index = reservation!.GetIndexes().Single(x => x.Properties.Count == 1 && x.Properties[0].Name == "IdempotencyKey");
        Assert.True(index.IsUnique);
    }
}
