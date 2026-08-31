using System.Globalization;
using Game.Sim.Entities;
using Game.Sim.Events;
using Game.Sim.Locations;
using Game.Sim.Logging;
using Game.Sim.Time;

namespace Game.Sim.Tests.Logging;

public sealed class WorldEventTraceTests
{
    [Fact]
    public void ComputeSha256_UsesTheCanonicalJsonlRepresentation()
    {
        var worldEvent = new WorldEvent(
            new EventId(1),
            new SimTime(2),
            new EntityId("george"),
            EventType.EnterLocation,
            new LocationId("basement"));
        using var output = new StringWriter(CultureInfo.InvariantCulture);

        WorldEventTrace.WriteJsonl([worldEvent], output);

        string first = WorldEventTrace.ComputeSha256([worldEvent]);
        string second = WorldEventTrace.ComputeSha256([worldEvent]);
        Assert.NotEmpty(output.ToString());
        Assert.Equal(64, first.Length);
        Assert.Equal(first, second);
    }
}
