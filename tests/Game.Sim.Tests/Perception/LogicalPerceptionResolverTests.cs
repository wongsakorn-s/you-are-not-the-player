using Game.Sim.Entities;
using Game.Sim.Events;
using Game.Sim.Locations;
using Game.Sim.Perception;
using Game.Sim.Time;
using Game.Sim.World;

namespace Game.Sim.Tests.Perception;

public sealed class LogicalPerceptionResolverTests
{
    private static readonly LocationId Lobby = new("lobby");
    private static readonly LocationId Hallway = new("hallway");
    private static readonly LocationId Basement = new("basement");
    private static readonly EntityId George = new("george");
    private static readonly EntityId Anna = new("anna");
    private static readonly EntityId Bob = new("bob");

    [Fact]
    public void SameLocationVisibleEvent_CreatesHighConfidenceVisualObservation()
    {
        (WorldState world, WorldEvent worldEvent) = CreateWorldAndEvent(
            observerLocation: Basement,
            eventTags: [EventTag.Movement, EventTag.Visible]);
        var resolver = new LogicalPerceptionResolver(new SequentialObservationIdGenerator());

        Observation observation = Assert.Single(
            resolver.Observe(world.GetEntity(Anna), worldEvent, world));

        Assert.Equal(new ObservationId(1), observation.Id);
        Assert.Equal(worldEvent.Id, observation.SourceEvent);
        Assert.Equal(Anna, observation.Observer);
        Assert.Equal(George, observation.PerceivedActor);
        Assert.Equal(Basement, observation.Location);
        Assert.Equal(0.95f, observation.Confidence);
        Assert.Equal(PerceptionChannel.Visual, observation.Channel);
    }

    [Fact]
    public void AdjacentAudibleEvent_CreatesLowerConfidenceAnonymousObservation()
    {
        (WorldState world, WorldEvent worldEvent) = CreateWorldAndEvent(
            observerLocation: Hallway,
            eventTags: [EventTag.Audible]);
        world.ConnectLocations(Hallway, Basement, audioTransmission: 0.5f);
        var resolver = new LogicalPerceptionResolver(new SequentialObservationIdGenerator());

        Observation observation = Assert.Single(
            resolver.Observe(world.GetEntity(Anna), worldEvent, world));

        Assert.Null(observation.PerceivedActor);
        Assert.Equal(0.20f, observation.Confidence);
        Assert.Equal(PerceptionChannel.Audio, observation.Channel);
    }

    [Fact]
    public void SameLocationAudibleEvent_DoesNotInventActorIdentity()
    {
        (WorldState world, WorldEvent worldEvent) = CreateWorldAndEvent(
            observerLocation: Basement,
            eventTags: [EventTag.Audible]);
        var resolver = new LogicalPerceptionResolver(new SequentialObservationIdGenerator());

        Observation observation = Assert.Single(
            resolver.Observe(world.GetEntity(Anna), worldEvent, world));

        Assert.Null(observation.PerceivedActor);
        Assert.Equal(0.75f, observation.Confidence);
        Assert.Equal(PerceptionChannel.Audio, observation.Channel);
    }

    [Fact]
    public void NonAdjacentOrInvisibleEvent_CreatesNoObservation()
    {
        (WorldState world, WorldEvent audibleEvent) = CreateWorldAndEvent(
            observerLocation: Lobby,
            eventTags: [EventTag.Audible]);
        var resolver = new LogicalPerceptionResolver(new SequentialObservationIdGenerator());

        IReadOnlyList<Observation> observations = resolver.Observe(
            world.GetEntity(Anna),
            audibleEvent,
            world);

        Assert.Empty(observations);
    }

    [Fact]
    public void Actor_DoesNotCreateObservationOfOwnEvent()
    {
        (WorldState world, WorldEvent worldEvent) = CreateWorldAndEvent(
            observerLocation: Basement,
            eventTags: [EventTag.Visible]);
        var resolver = new LogicalPerceptionResolver(new SequentialObservationIdGenerator());

        IReadOnlyList<Observation> observations = resolver.Observe(
            world.GetEntity(George),
            worldEvent,
            world);

        Assert.Empty(observations);
    }

    [Fact]
    public void RestrictedEvent_HasHigherSalience()
    {
        (WorldState world, WorldEvent worldEvent) = CreateWorldAndEvent(
            observerLocation: Basement,
            eventTags: [EventTag.Visible, EventTag.Restricted]);
        var resolver = new LogicalPerceptionResolver(new SequentialObservationIdGenerator());

        Observation observation = Assert.Single(
            resolver.Observe(world.GetEntity(Anna), worldEvent, world));

        Assert.Equal(0.90f, observation.Salience);
    }

    [Fact]
    public void PerceptionSystem_AssignsIdsInStableObserverOrder()
    {
        var world = CreateWorld();
        world.AddEntity(new EntityState(Bob, Basement));
        world.AddEntity(new EntityState(Anna, Basement));
        var worldEvent = CreateEvent([EventTag.Visible]);
        var resolver = new LogicalPerceptionResolver(new SequentialObservationIdGenerator());
        var system = new PerceptionSystem(resolver);

        IReadOnlyList<Observation> observations = system.Process(worldEvent, world);

        Assert.Equal([Anna, Bob], observations.Select(observation => observation.Observer));
        Assert.Equal(
            [new ObservationId(1), new ObservationId(2)],
            observations.Select(observation => observation.Id));
    }

    private static (WorldState World, WorldEvent Event) CreateWorldAndEvent(
        LocationId observerLocation,
        IReadOnlyCollection<EventTag> eventTags)
    {
        WorldState world = CreateWorld();
        world.AddEntity(new EntityState(Anna, observerLocation));
        return (world, CreateEvent(eventTags));
    }

    private static WorldState CreateWorld()
    {
        var world = new WorldState();
        world.AddLocation(new LocationState(Lobby));
        world.AddLocation(new LocationState(Hallway));
        world.AddLocation(new LocationState(Basement));
        world.AddEntity(new EntityState(George, Basement));
        return world;
    }

    private static WorldEvent CreateEvent(IReadOnlyCollection<EventTag> eventTags) =>
        new(
            new EventId(1),
            SimTime.Zero,
            George,
            EventType.EnterLocation,
            Basement,
            tags: eventTags);
}
