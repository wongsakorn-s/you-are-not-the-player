using Game.Sim.Entities;
using Game.Sim.Events;
using Game.Sim.Locations;
using Game.Sim.Memory;
using Game.Sim.Patterns;
using Game.Sim.Perception;
using Game.Sim.Suspicion;
using Game.Sim.Time;
using Game.Sim.World;

namespace Game.Sim.Tests.Patterns;

public sealed class BehaviorPatternSystemTests
{
    private static readonly EntityId Actor = new("actor");
    private static readonly EntityId Witness = new("witness");
    private static readonly LocationId Room = new("room");

    [Fact]
    public void LootSweep_FlowsThroughEventPerceptionMemoryAndSuspicion()
    {
        var world = new WorldState();
        world.AddLocation(new LocationState(Room));
        world.AddEntity(new EntityState(Actor, Room));
        world.AddEntity(new EntityState(Witness, Room));
        var clock = new SimClock(ticksPerSecond: 1);
        var ids = new SequentialEventIdGenerator();
        var eventFactory = new WorldEventFactory(clock, ids);
        var buffer = new WorldEventBuffer();
        var detector = new RuleBasedBehaviorPatternDetector(clock.TicksPerSecond);
        var patterns = new BehaviorPatternSystem(clock, detector, eventFactory, buffer);

        for (int index = 1; index <= 11; index++)
        {
            clock.AdvanceOneTick();
            WorldEvent interaction = eventFactory.Create(
                Actor,
                EventType.Interaction,
                Room,
                payload: new InteractionPayload(
                    InteractionKind.LootContainer,
                    $"container-{index}"));
            _ = patterns.Process(interaction);
        }

        WorldEvent patternEvent = Assert.Single(buffer.Drain());
        BehaviorPatternPayload payload = Assert.IsType<BehaviorPatternPayload>(patternEvent.Payload);
        Assert.Equal(BehaviorPatternKind.LootSweep, payload.Pattern);
        Assert.Equal(10, payload.EvidenceEvents.Count);

        var resolver = new LogicalPerceptionResolver(new SequentialObservationIdGenerator());
        Observation observation = Assert.Single(
            resolver.Observe(world.GetEntity(Witness), patternEvent, world));
        Assert.Equal(BehaviorPatternKind.LootSweep, observation.BehaviorPattern);

        var memories = new MemorySystem(
            world,
            new SequentialMemoryIdGenerator(),
            new ExponentialMemoryDecayPolicy(0.0, 0.0));
        MemoryRecord memory = Assert.IsType<MemoryRecord>(memories.Remember(observation));
        Assert.Equal(BehaviorPatternKind.LootSweep, memory.BehaviorPattern);
        string rulesPath = Path.Combine(
            AppContext.BaseDirectory,
            "Data",
            "SuspicionRules",
            "mvp.json");
        var suspicion = new SuspicionSystem(
            memories,
            JsonSuspicionRuleParser.Parse(File.ReadAllText(rulesPath)));

        _ = suspicion.ProcessMemory(Witness, memory);
        SuspicionVector vector = suspicion.GetSnapshot(Witness, Actor, clock.Now).Vector;

        Assert.Equal(33.25f, vector.MetaBehavior, precision: 5);
        Assert.Equal(14.25f, vector.RoleDeviation, precision: 5);
        Assert.Equal(0.0f, vector.Criminality);
    }
}
