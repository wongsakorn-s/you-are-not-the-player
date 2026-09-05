using Game.Sim.Actions;
using Game.Sim.Brain;
using Game.Sim.Entities;
using Game.Sim.Events;
using Game.Sim.Locations;
using Game.Sim.Memory;
using Game.Sim.Needs;
using Game.Sim.Perception;
using Game.Sim.Roles;
using Game.Sim.Routines;
using Game.Sim.Schedules;
using Game.Sim.Secrets;
using Game.Sim.Suspicion;
using Game.Sim.Time;
using Game.Sim.World;

namespace Game.Sim.Tests.Secrets;

public sealed class SecretBehaviorSystemTests
{
    private static readonly EntityId Thief = new("thief");
    private static readonly EntityId NightOwl = new("night-owl");
    private static readonly EntityId ConspiratorA = new("conspirator-a");
    private static readonly EntityId ConspiratorB = new("conspirator-b");
    private static readonly EntityId Witness = new("witness");
    private static readonly LocationId Home = new("home");
    private static readonly LocationId Vault = new("vault");

    [Fact]
    public void Routine_KeepsEachSecretCatchableAndFalsePositivesMultidimensional()
    {
        Scenario scenario = CreateScenario();

        IReadOnlyList<NpcRoutineDecision> firstDecisions =
            scenario.Routine.Tick(new SimDelta(60));
        _ = scenario.Routine.Tick(new SimDelta(60));
        WorldEvent[] secretEvents = scenario.Buffer
            .Drain()
            .Where(worldEvent => worldEvent.Type is
                EventType.Theft or EventType.SecretMeeting or EventType.NightActivity)
            .ToArray();

        // Three secrets, three kinds, and each one catchable more than once
        // while it is in progress: a single event per night meant a secret with
        // nobody standing in the room left no trace at all.
        Assert.Equal(
            [EventType.Theft, EventType.SecretMeeting, EventType.NightActivity],
            secretEvents.Select(worldEvent => worldEvent.Type).Distinct().Order());
        Assert.All(
            secretEvents,
            worldEvent => Assert.Equal(Vault, worldEvent.Location));
        Assert.All(
            secretEvents.Where(worldEvent => worldEvent.Type == EventType.SecretMeeting),
            worldEvent => Assert.Equal(ConspiratorB, worldEvent.Target));

        // Two ticks sixty minutes apart, and the cooldown is twelve, so every
        // secret has had a second chance to be seen.
        Assert.All(
            secretEvents.GroupBy(worldEvent => worldEvent.Type),
            group => Assert.True(
                group.Count() > 1,
                $"{group.Key} was only catchable once."));

        NpcRoutineDecision thiefDecision = Assert.Single(
            firstDecisions,
            decision => decision.Entity == Thief);
        Assert.Equal(GoalType.Steal, thiefDecision.Goal.Type);
        Assert.True(thiefDecision.Goal.IgnoresRolePermissions);
        Assert.True(thiefDecision.Moved);

        SuspicionSystem suspicion = RememberAndEvaluate(scenario, secretEvents);
        SuspicionVector theft = suspicion
            .GetSnapshot(Witness, Thief, scenario.Clock.Now)
            .Vector;
        SuspicionVector nightActivity = suspicion
            .GetSnapshot(Witness, NightOwl, scenario.Clock.Now)
            .Vector;
        SuspicionVector meeting = suspicion
            .GetSnapshot(Witness, ConspiratorA, scenario.Clock.Now)
            .Vector;

        Assert.True(theft.Criminality > 0.0f);
        Assert.Equal(0.0f, theft.MetaBehavior);
        Assert.Equal(0.0f, theft.ImpossibleBehavior);
        Assert.Equal(0.0f, nightActivity.Criminality);
        Assert.True(nightActivity.RoleDeviation > 0.0f);
        Assert.Equal(0.0f, nightActivity.MetaBehavior);
        Assert.Equal(0.0f, meeting.Criminality);
        Assert.True(meeting.Secrecy > nightActivity.Secrecy);
    }

    [Fact]
    public void Brain_DoesNotSelectForbiddenSecretWithoutExplicitRoleBypass()
    {
        var world = new WorldState();
        world.AddLocation(new LocationState(Home));
        world.AddLocation(new LocationState(Vault));
        world.AddEntity(new EntityState(Thief, Home));
        NpcRoutineProfile profile = CreateProfile(Thief, canEnterVault: false);
        var plan = new SecretPlan(
            "forbidden-plan",
            SecretBehaviorKind.Theft,
            [Thief],
            At(0, 0),
            At(1, 0),
            Vault,
            utility: 100.0f,
            ignoresRolePermissions: false);
        var brain = new UtilityNpcBrain([
            new ScheduleGoalSource(),
            new SecretGoalSource(new SecretPlanRepository([plan])),
        ]);

        GoalCandidate selected = brain.SelectGoal(new NpcDecisionContext(
            world.GetEntity(Thief),
            profile,
            At(0, 30)));

        Assert.Equal(GoalType.Idle, selected.Type);
        Assert.Equal(Home, selected.Destination);
    }

    [Fact]
    public void SecretPlan_ResolvesOvernightWindowWithoutIncludingEndBoundary()
    {
        var plan = new SecretPlan(
            "overnight",
            SecretBehaviorKind.NightOwl,
            [NightOwl],
            At(22, 0),
            At(2, 0),
            Vault,
            utility: 80.0f);

        Assert.True(plan.IsActive(At(23, 59)));
        Assert.True(plan.IsActive(At(1, 59)));
        Assert.False(plan.IsActive(At(2, 0)));
        Assert.False(plan.IsActive(At(12, 0)));
    }

    private static Scenario CreateScenario()
    {
        var world = new WorldState();
        world.AddLocation(new LocationState(Home));
        world.AddLocation(new LocationState(Vault));
        foreach (EntityId entity in new[] { Thief, NightOwl, ConspiratorA, ConspiratorB })
        {
            world.AddEntity(new EntityState(entity, Home));
        }

        world.AddEntity(new EntityState(Witness, Vault));
        var plans = new SecretPlanRepository([
            Plan("theft", SecretBehaviorKind.Theft, [Thief], ignoresRolePermissions: true),
            Plan("night-owl", SecretBehaviorKind.NightOwl, [NightOwl]),
            Plan("meeting", SecretBehaviorKind.SecretMeeting, [ConspiratorA, ConspiratorB]),
        ]);
        var clock = new SimClock(ticksPerSecond: 1);
        var buffer = new WorldEventBuffer();
        var eventFactory = new WorldEventFactory(clock, new SequentialEventIdGenerator());
        var movement = new MoveEntityActionHandler(world, eventFactory, buffer);
        var secrets = new SecretBehaviorSystem(clock, world, eventFactory, buffer, plans);
        var brain = new UtilityNpcBrain([
            new ScheduleGoalSource(),
            new NeedGoalSource(),
            new SecretGoalSource(plans),
        ]);
        var routine = new NpcRoutineSystem(clock, world, movement, brain, [secrets]);
        routine.Register(CreateProfile(Thief, canEnterVault: false));
        routine.Register(CreateProfile(NightOwl, canEnterVault: true));
        routine.Register(CreateProfile(ConspiratorA, canEnterVault: true));
        routine.Register(CreateProfile(ConspiratorB, canEnterVault: true));
        return new Scenario(world, clock, buffer, routine);
    }

    private static SuspicionSystem RememberAndEvaluate(
        Scenario scenario,
        IEnumerable<WorldEvent> worldEvents)
    {
        var memories = new MemorySystem(
            scenario.World,
            new SequentialMemoryIdGenerator(),
            new ExponentialMemoryDecayPolicy(0.0, 0.0));
        string rulesPath = Path.Combine(
            AppContext.BaseDirectory,
            "Data",
            "SuspicionRules",
            "mvp.json");
        var suspicion = new SuspicionSystem(
            memories,
            JsonSuspicionRuleParser.Parse(File.ReadAllText(rulesPath)));
        var resolver = new LogicalPerceptionResolver(new SequentialObservationIdGenerator());

        foreach (WorldEvent worldEvent in worldEvents)
        {
            Observation observation = Assert.Single(
                resolver.Observe(scenario.World.GetEntity(Witness), worldEvent, scenario.World));
            MemoryRecord memory = Assert.IsType<MemoryRecord>(memories.Remember(observation));
            _ = suspicion.ProcessMemory(Witness, memory);
        }

        return suspicion;
    }

    private static NpcRoutineProfile CreateProfile(EntityId entity, bool canEnterVault)
    {
        LocationId[] allowedLocations = canEnterVault ? [Home, Vault] : [Home];
        var role = new RolePermissions(new RoleId("resident"), allowedLocations);
        var schedule = new DailySchedule([
            new ScheduleEntry(At(0, 0), At(12, 0), RoutineActivity.Idle, Home, utility: 10.0f),
            new ScheduleEntry(At(12, 0), At(0, 0), RoutineActivity.Idle, Home, utility: 10.0f),
        ]);
        return new NpcRoutineProfile(
            entity,
            role,
            schedule,
            new NeedState(),
            new NeedProfile(new NeedRates(0.0, 0.0, 0.0), 0.0, 0.0, 0.0),
            new NeedDestinations(Home, Home, Home));
    }

    private static SecretPlan Plan(
        string id,
        SecretBehaviorKind behavior,
        IEnumerable<EntityId> participants,
        bool ignoresRolePermissions = false) =>
        new(
            id,
            behavior,
            participants,
            At(0, 1),
            At(0, 10),
            Vault,
            utility: 100.0f,
            ignoresRolePermissions);

    private static SimMinuteOfDay At(int hour, int minute) =>
        SimMinuteOfDay.FromHourMinute(hour, minute);

    private sealed record Scenario(
        WorldState World,
        SimClock Clock,
        WorldEventBuffer Buffer,
        NpcRoutineSystem Routine);
}
