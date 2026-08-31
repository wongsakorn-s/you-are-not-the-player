using Game.Sim.Actions;
using Game.Sim.Entities;
using Game.Sim.Events;
using Game.Sim.Locations;
using Game.Sim.Time;
using Game.Sim.World;

namespace Game.Sim.Tests.Actions;

public sealed class MoveEntityActionHandlerTests
{
    private static readonly EntityId George = new("george");
    private static readonly EntityId UnknownActor = new("unknown");
    private static readonly LocationId Lobby = new("lobby");
    private static readonly LocationId Basement = new("basement");
    private static readonly LocationId UnknownLocation = new("unknown");

    [Fact]
    public void Execute_MovesEntityAndPublishesOrderedTransitionEvents()
    {
        (WorldState world, SimClock clock, WorldEventBuffer buffer, MoveEntityActionHandler handler) =
            CreateSystem();
        clock.Advance(new SimDelta(12));

        bool moved = handler.Execute(new MoveEntityCommand(George, Basement));
        IReadOnlyList<WorldEvent> events = buffer.Drain();

        Assert.True(moved);
        Assert.Equal(Basement, world.GetEntity(George).LogicalLocation);
        Assert.Equal([EventType.LeaveLocation, EventType.EnterLocation], events.Select(evt => evt.Type));
        Assert.Equal([Lobby, Basement], events.Select(evt => evt.Location));
        Assert.Equal([new EventId(1), new EventId(2)], events.Select(evt => evt.Id));
        Assert.All(events, evt => Assert.Equal(new SimTime(12), evt.Time));
        Assert.All(events, AssertTransitionPayload);
    }

    [Fact]
    public void Execute_SameLocationIsNoOpAndDoesNotConsumeEventIds()
    {
        (_, _, WorldEventBuffer buffer, MoveEntityActionHandler handler) = CreateSystem();

        bool moved = handler.Execute(new MoveEntityCommand(George, Lobby));
        bool movedAfterNoOp = handler.Execute(new MoveEntityCommand(George, Basement));
        IReadOnlyList<WorldEvent> events = buffer.Drain();

        Assert.False(moved);
        Assert.True(movedAfterNoOp);
        Assert.Equal([new EventId(1), new EventId(2)], events.Select(evt => evt.Id));
    }

    [Fact]
    public void Execute_UnknownDestinationLeavesWorldAndBufferUnchanged()
    {
        (WorldState world, _, WorldEventBuffer buffer, MoveEntityActionHandler handler) = CreateSystem();

        Assert.Throws<KeyNotFoundException>(
            () => handler.Execute(new MoveEntityCommand(George, UnknownLocation)));

        Assert.Equal(Lobby, world.GetEntity(George).LogicalLocation);
        Assert.Equal(0, buffer.Count);
    }

    [Fact]
    public void Execute_UnknownActorLeavesBufferUnchanged()
    {
        (_, _, WorldEventBuffer buffer, MoveEntityActionHandler handler) = CreateSystem();

        Assert.Throws<KeyNotFoundException>(
            () => handler.Execute(new MoveEntityCommand(UnknownActor, Basement)));

        Assert.Equal(0, buffer.Count);
    }

    [Fact]
    public void Execute_EnteringRestrictedLocationTagsOnlyDestinationEvent()
    {
        var world = new WorldState();
        world.AddLocation(new LocationState(Lobby));
        world.AddLocation(new LocationState(Basement, isRestricted: true));
        world.AddEntity(new EntityState(George, Lobby));
        var clock = new SimClock();
        var buffer = new WorldEventBuffer();
        var factory = new WorldEventFactory(clock, new SequentialEventIdGenerator());
        var handler = new MoveEntityActionHandler(world, factory, buffer);

        _ = handler.Execute(new MoveEntityCommand(George, Basement));
        IReadOnlyList<WorldEvent> events = buffer.Drain();

        Assert.DoesNotContain(EventTag.Restricted, events[0].Tags);
        Assert.Contains(EventTag.Restricted, events[1].Tags);
    }

    private static (
        WorldState World,
        SimClock Clock,
        WorldEventBuffer Buffer,
        MoveEntityActionHandler Handler) CreateSystem()
    {
        var world = new WorldState();
        world.AddLocation(new LocationState(Lobby));
        world.AddLocation(new LocationState(Basement));
        world.AddEntity(new EntityState(George, Lobby));

        var clock = new SimClock();
        var buffer = new WorldEventBuffer();
        var factory = new WorldEventFactory(clock, new SequentialEventIdGenerator());
        var handler = new MoveEntityActionHandler(world, factory, buffer);
        return (world, clock, buffer, handler);
    }

    private static void AssertTransitionPayload(WorldEvent worldEvent)
    {
        LocationTransitionPayload payload = Assert.IsType<LocationTransitionPayload>(worldEvent.Payload);
        Assert.Equal(Lobby, payload.Origin);
        Assert.Equal(Basement, payload.Destination);
        Assert.Equal([EventTag.Movement, EventTag.Visible], worldEvent.Tags);
    }
}
