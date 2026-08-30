using System.Globalization;
using Game.Sim.Entities;
using Game.Sim.Events;
using Game.Sim.Locations;
using Game.Sim.Logging;
using Game.Sim.Time;

namespace Game.Sim.Tests.Logging;

public sealed class JsonlWorldEventLoggerTests
{
    private static readonly EntityId George = new("george");
    private static readonly LocationId Lobby = new("lobby");
    private static readonly LocationId Basement = new("basement");

    [Fact]
    public void Write_ProducesOneDeterministicJsonObjectPerLine()
    {
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        var logger = new JsonlWorldEventLogger(output);
        var worldEvent = new WorldEvent(
            new EventId(7),
            new SimTime(12),
            George,
            EventType.EnterLocation,
            Basement,
            tags: [EventTag.Visible, EventTag.Movement, EventTag.Visible],
            payload: new LocationTransitionPayload(Lobby, Basement));

        logger.Write(worldEvent);

        const string expected =
            "{\"schemaVersion\":1,\"id\":7,\"tick\":12,\"type\":\"EnterLocation\"," +
            "\"actor\":\"george\",\"target\":null,\"location\":\"basement\"," +
            "\"tags\":[\"Movement\",\"Visible\"],\"payload\":{" +
            "\"type\":\"locationTransition\",\"origin\":\"lobby\"," +
            "\"destination\":\"basement\"}}\n";
        Assert.Equal(expected, output.ToString());
    }

    [Fact]
    public void Write_MultipleEventsRemainOnSeparateLfTerminatedLines()
    {
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        var logger = new JsonlWorldEventLogger(output);
        var factory = new WorldEventFactory(new SimClock(), new SequentialEventIdGenerator());

        logger.Write(factory.Create(George, EventType.LeaveLocation, Lobby));
        logger.Write(factory.Create(George, EventType.EnterLocation, Basement));

        string[] lines = output.ToString().Split('\n');
        Assert.Equal(3, lines.Length);
        Assert.NotEmpty(lines[0]);
        Assert.NotEmpty(lines[1]);
        Assert.Empty(lines[2]);
    }

    [Fact]
    public void Write_SerializesBehaviorPatternEvidenceDeterministically()
    {
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        var logger = new JsonlWorldEventLogger(output);
        var worldEvent = new WorldEvent(
            new EventId(12),
            new SimTime(20),
            George,
            EventType.BehaviorPattern,
            Lobby,
            tags: [EventTag.Pattern],
            payload: new BehaviorPatternPayload(
                BehaviorPatternKind.LootSweep,
                [new EventId(9), new EventId(3)]));

        logger.Write(worldEvent);

        const string expected =
            "{\"schemaVersion\":1,\"id\":12,\"tick\":20,\"type\":\"BehaviorPattern\"," +
            "\"actor\":\"george\",\"target\":null,\"location\":\"lobby\"," +
            "\"tags\":[\"Pattern\"],\"payload\":{\"type\":\"behaviorPattern\"," +
            "\"pattern\":\"LootSweep\",\"evidenceEvents\":[3,9]}}\n";
        Assert.Equal(expected, output.ToString());
    }
}
