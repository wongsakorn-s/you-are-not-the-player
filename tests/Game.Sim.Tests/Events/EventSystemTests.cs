using Game.Sim.Entities;
using Game.Sim.Events;
using Game.Sim.Locations;
using Game.Sim.Time;

namespace Game.Sim.Tests.Events;

public sealed class EventSystemTests
{
    private static readonly EntityId George = new("george");
    private static readonly LocationId Basement = new("basement");

    [Fact]
    public void SequentialEventIdGenerator_ProducesStableMonotonicIds()
    {
        var ids = new SequentialEventIdGenerator(firstValue: 10);

        EventId[] actual = [ids.NextId(), ids.NextId(), ids.NextId()];

        Assert.Equal([new EventId(10), new EventId(11), new EventId(12)], actual);
    }

    [Fact]
    public void WorldEventFactory_CapturesCurrentTimeAndDefensivelyCopiesTags()
    {
        var clock = new SimClock();
        clock.Advance(new SimDelta(8));
        var factory = new WorldEventFactory(clock, new SequentialEventIdGenerator());
        var tags = new List<EventTag> { EventTag.Movement, EventTag.Visible };

        WorldEvent worldEvent = factory.Create(
            George,
            EventType.EnterLocation,
            Basement,
            tags: tags);
        tags.Clear();

        Assert.Equal(new EventId(1), worldEvent.Id);
        Assert.Equal(new SimTime(8), worldEvent.Time);
        Assert.Equal([EventTag.Movement, EventTag.Visible], worldEvent.Tags);
        Assert.Equal(2, worldEvent.Tags.Count);
    }

    [Fact]
    public void EventStream_DrainsEventsInPublishOrder()
    {
        var factory = new WorldEventFactory(new SimClock(), new SequentialEventIdGenerator());
        var buffer = new WorldEventBuffer();
        WorldEvent first = factory.Create(George, EventType.LeaveLocation, Basement);
        WorldEvent second = factory.Create(George, EventType.EnterLocation, Basement);

        buffer.Publish(first);
        buffer.Publish(second);
        IReadOnlyList<WorldEvent> drained = buffer.Drain();

        Assert.Equal([first, second], drained);
        Assert.Equal(0, buffer.Count);
        Assert.Empty(buffer.Drain());
    }

    [Fact]
    public void WorldEvent_UsesEmptyPayloadWhenNoneIsProvided()
    {
        var factory = new WorldEventFactory(new SimClock(), new SequentialEventIdGenerator());

        WorldEvent worldEvent = factory.Create(George, EventType.EnterLocation, Basement);

        Assert.Same(EmptyEventPayload.Instance, worldEvent.Payload);
    }

    [Fact]
    public void LocationTransitionPayload_PreservesWorldTruth()
    {
        var lobby = new LocationId("lobby");
        var payload = new LocationTransitionPayload(lobby, Basement);

        Assert.Equal(lobby, payload.Origin);
        Assert.Equal(Basement, payload.Destination);
    }
}
