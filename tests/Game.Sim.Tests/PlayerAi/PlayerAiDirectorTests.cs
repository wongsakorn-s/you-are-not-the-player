using System.Globalization;
using Game.Sim.Actions;
using Game.Sim.Brain;
using Game.Sim.Entities;
using Game.Sim.Events;
using Game.Sim.Locations;
using Game.Sim.Needs;
using Game.Sim.Patterns;
using Game.Sim.PlayerAi;
using Game.Sim.Roles;
using Game.Sim.Routines;
using Game.Sim.Schedules;
using Game.Sim.Time;
using Game.Sim.World;

namespace Game.Sim.Tests.PlayerAi;

public sealed class PlayerAiDirectorTests
{
    private static readonly EntityId Agent = new("agent");
    private static readonly LocationId Home = new("home");
    private static readonly LocationId Office = new("office");

    [Fact]
    public void Completionist_UsesInteractionActionsAndTriggersLootSweepDeterministically()
    {
        string[] firstRun = RunCompletionist();
        string[] secondRun = RunCompletionist();

        Assert.Equal(firstRun, secondRun);
        Assert.Equal(10, firstRun.Count(item => item.Contains("|Interaction|", StringComparison.Ordinal)));
        Assert.Equal(1, firstRun.Count(item =>
            item.Contains("|BehaviorPattern|LootSweep", StringComparison.Ordinal)));
        Assert.DoesNotContain(firstRun, item =>
            item.Contains("Completionist", StringComparison.Ordinal));
    }

    [Fact]
    public void Explorer_UsesMovementAndBoundaryActionsToTriggerBoundaryTesting()
    {
        LocationId[] locations = [
            Home,
            new LocationId("area-1"),
            new LocationId("area-2"),
            new LocationId("area-3"),
            new LocationId("area-4"),
        ];
        var world = CreateWorld(locations);
        var profile = new PlayerAiProfile(
            Agent,
            PlayerAiArchetype.Explorer,
            explorationObjectives: locations
                .Skip(1)
                .Select((location, index) => new ExplorationObjective(
                    $"explore-{index + 1}",
                    location,
                    $"boundary-{index + 1}")));
        Simulation simulation = CreateSimulation(world, profile, locations);

        for (int index = 0; index < 4; index++)
        {
            _ = simulation.Routine.Tick(SimDelta.OneTick);
        }

        WorldEvent[] events = simulation.Buffer.Drain().ToArray();
        Assert.Equal(4, events.Count(worldEvent => worldEvent.Type == EventType.EnterLocation));
        Assert.Equal(4, events.Count(worldEvent => worldEvent.Type == EventType.BoundaryProbe));
        WorldEvent patternEvent = Assert.Single(
            events,
            worldEvent => worldEvent.Type == EventType.BehaviorPattern);
        BehaviorPatternPayload payload = Assert.IsType<BehaviorPatternPayload>(patternEvent.Payload);
        Assert.Equal(BehaviorPatternKind.BoundaryTesting, payload.Pattern);
        Assert.Equal(locations[^1], world.GetEntity(Agent).LogicalLocation);
    }

    [Fact]
    public void Roleplayer_LeavesDecisionToNormalScheduleWithoutSpecialEvents()
    {
        LocationId[] locations = [Home, Office];
        var world = CreateWorld(locations);
        var profile = new PlayerAiProfile(Agent, PlayerAiArchetype.Roleplayer);
        Simulation simulation = CreateSimulation(
            world,
            profile,
            locations,
            scheduleLocation: Office);

        NpcRoutineDecision decision = Assert.Single(
            simulation.Routine.Tick(SimDelta.OneTick));
        WorldEvent[] events = simulation.Buffer.Drain().ToArray();

        Assert.Equal(GoalType.Work, decision.Goal.Type);
        Assert.Equal(Office, decision.Goal.Destination);
        Assert.Equal(Office, world.GetEntity(Agent).LogicalLocation);
        Assert.DoesNotContain(
            events,
            worldEvent => worldEvent.Type is EventType.Interaction or EventType.BoundaryProbe);
    }

    private static string[] RunCompletionist()
    {
        LocationId[] locations = [Home];
        var world = CreateWorld(locations);
        var profile = new PlayerAiProfile(
            Agent,
            PlayerAiArchetype.Completionist,
            completionObjectives: Enumerable.Range(1, 10).Select(index =>
                new CompletionObjective(
                    $"container-{index.ToString(CultureInfo.InvariantCulture)}",
                    Home,
                    InteractionKind.LootContainer)));
        Simulation simulation = CreateSimulation(world, profile, locations);

        for (int index = 0; index < 11; index++)
        {
            _ = simulation.Routine.Tick(SimDelta.OneTick);
        }

        return simulation.Buffer.Drain().Select(FormatEvent).ToArray();
    }

    private static Simulation CreateSimulation(
        WorldState world,
        PlayerAiProfile playerProfile,
        IReadOnlyCollection<LocationId> allowedLocations,
        LocationId? scheduleLocation = null)
    {
        var clock = new SimClock(ticksPerSecond: 1);
        var buffer = new WorldEventBuffer();
        var eventFactory = new WorldEventFactory(clock, new SequentialEventIdGenerator());
        var detector = new RuleBasedBehaviorPatternDetector(clock.TicksPerSecond);
        var patternSystem = new BehaviorPatternSystem(clock, detector, eventFactory, buffer);
        var interactions = new InteractionActionHandler(
            world,
            eventFactory,
            buffer,
            patternSystem);
        var probes = new BoundaryProbeActionHandler(
            world,
            eventFactory,
            buffer,
            patternSystem);
        var director = new PlayerAiDirector([playerProfile], interactions, probes);
        var brain = new UtilityNpcBrain([new ScheduleGoalSource(), director]);
        var movement = new MoveEntityActionHandler(world, eventFactory, buffer);
        var routine = new NpcRoutineSystem(clock, world, movement, brain, [director]);
        routine.Register(CreateRoutineProfile(
            allowedLocations,
            scheduleLocation ?? Home));
        return new Simulation(routine, buffer);
    }

    private static WorldState CreateWorld(IEnumerable<LocationId> locations)
    {
        var world = new WorldState();
        foreach (LocationId location in locations)
        {
            world.AddLocation(new LocationState(location));
        }

        world.AddEntity(new EntityState(Agent, Home));
        return world;
    }

    private static NpcRoutineProfile CreateRoutineProfile(
        IReadOnlyCollection<LocationId> allowedLocations,
        LocationId scheduleLocation)
    {
        var role = new RolePermissions(new RoleId("resident"), allowedLocations);
        RoutineActivity activity = scheduleLocation == Office
            ? RoutineActivity.Work
            : RoutineActivity.Idle;
        var schedule = new DailySchedule([
            new ScheduleEntry(At(0, 0), At(12, 0), activity, scheduleLocation, utility: 10.0f),
            new ScheduleEntry(At(12, 0), At(0, 0), activity, scheduleLocation, utility: 10.0f),
        ]);
        return new NpcRoutineProfile(
            Agent,
            role,
            schedule,
            new NeedState(),
            new NeedProfile(new NeedRates(0.0, 0.0, 0.0), 0.0, 0.0, 0.0),
            new NeedDestinations(Home, Home, Home));
    }

    private static string FormatEvent(WorldEvent worldEvent)
    {
        string pattern = worldEvent.Payload is BehaviorPatternPayload payload
            ? payload.Pattern.ToString()
            : string.Empty;
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{worldEvent.Id.Value}|{worldEvent.Time.Tick}|{worldEvent.Type}|{pattern}|{worldEvent.Location.Value}");
    }

    private static SimMinuteOfDay At(int hour, int minute) =>
        SimMinuteOfDay.FromHourMinute(hour, minute);

    private sealed record Simulation(
        NpcRoutineSystem Routine,
        WorldEventBuffer Buffer);
}
