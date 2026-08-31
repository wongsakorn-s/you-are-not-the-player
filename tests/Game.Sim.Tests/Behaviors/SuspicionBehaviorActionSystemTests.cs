using Game.Sim.Behaviors;
using Game.Sim.Brain;
using Game.Sim.Entities;
using Game.Sim.Events;
using Game.Sim.Locations;
using Game.Sim.Memory;
using Game.Sim.Perception;
using Game.Sim.Routines;
using Game.Sim.Suspicion;
using Game.Sim.Time;
using Game.Sim.World;

namespace Game.Sim.Tests.Behaviors;

public sealed class SuspicionBehaviorActionSystemTests
{
    private static readonly EntityId Requester = new("requester");
    private static readonly EntityId Contact = new("contact");
    private static readonly EntityId Subject = new("subject");
    private static readonly LocationId Room = new("room");

    [Fact]
    public void Observe_AskInformationTransfersUnknownMemoryToRequester()
    {
        var world = new WorldState();
        world.AddLocation(new LocationState(Room));
        world.AddEntity(new EntityState(Requester, Room));
        world.AddEntity(new EntityState(Contact, Room));
        world.AddEntity(new EntityState(Subject, Room));
        var clock = new SimClock(ticksPerSecond: 1);
        var buffer = new WorldEventBuffer();
        var eventFactory = new WorldEventFactory(clock, new SequentialEventIdGenerator());
        var memories = new MemorySystem(
            world,
            new SequentialMemoryIdGenerator(),
            new ExponentialMemoryDecayPolicy(0.0, 0.0));
        var observation = new Observation(
            new ObservationId(1),
            new EventId(42),
            Contact,
            Subject,
            EventType.EnterLocation,
            Room,
            [EventTag.Visible],
            SimTime.Zero,
            confidence: 1.0f,
            salience: 0.5f,
            PerceptionChannel.Visual);
        _ = memories.Remember(observation);
        var suspicion = new SuspicionSystem(
            memories,
            new InMemorySuspicionRuleRepository([]));
        var actions = new SuspicionBehaviorActionSystem(
            clock,
            world,
            memories,
            suspicion,
            eventFactory,
            buffer);
        var goal = new GoalCandidate(
            GoalType.AskAboutTarget,
            Room,
            baseUtility: 10.0f,
            intentId: "ask",
            target: Subject,
            interactionPartner: Contact);

        actions.Observe(new NpcRoutineDecision(
            clock.Now,
            Requester,
            goal,
            Moved: false));

        MemoryRecord received = Assert.Single(memories.GetStore(Requester).Memories);
        WorldEvent informationEvent = Assert.Single(buffer.Drain());
        Assert.Equal(MemoryKind.Social, received.Kind);
        Assert.Equal(Contact, received.InformationSource);
        Assert.Equal(new EventId(42), received.RootEventId);
        Assert.Equal(EventType.AskInformation, informationEvent.Type);
        Assert.Equal(Requester, informationEvent.Actor);
        Assert.Equal(Contact, informationEvent.Target);
    }
}
