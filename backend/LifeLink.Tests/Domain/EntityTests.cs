using LifeLink.Domain.Common;
namespace LifeLink.Tests.Domain;
public sealed class EntityTests
{
    [Fact]
    public void NewEntity_HasIdentityAndUtcTimestamps()
    {
        var entity = new TestEntity();
        Assert.NotEqual(Guid.Empty, entity.Id);
        Assert.NotEqual(default, entity.CreatedAtUtc);
        Assert.Equal(TimeSpan.Zero, entity.CreatedAtUtc.Offset);
    }
    private sealed class TestEntity : Entity;
}
