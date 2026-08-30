using Game.Sim.Entities;
using Game.Sim.Events;
using Game.Sim.Locations;

namespace Game.Sim.Tests.Entities;

public sealed class IdentifierTests
{
    [Fact]
    public void StringIdentifiers_TrimValues()
    {
        Assert.Equal("anna", new EntityId("  anna  ").Value);
        Assert.Equal("basement", new LocationId("  basement  ").Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void EntityId_RejectsBlankValues(string value)
    {
        Assert.Throws<ArgumentException>(() => new EntityId(value));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void LocationId_RejectsBlankValues(string value)
    {
        Assert.Throws<ArgumentException>(() => new LocationId(value));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void EventId_RejectsNonPositiveValues(long value)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new EventId(value));
    }
}
