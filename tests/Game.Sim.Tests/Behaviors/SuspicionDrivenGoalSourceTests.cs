using Game.Sim.Actions;
using Game.Sim.Behaviors;
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
using Game.Sim.Suspicion;
using Game.Sim.Time;
using Game.Sim.World;

namespace Game.Sim.Tests.Behaviors;

public sealed class SuspicionDrivenGoalSourceTests
{
    private static readonly EntityId Anna = new("anna");
    private static readonly EntityId Bob = new("bob");
    private static readonly EntityId George = new("george");
    private static readonly EntityId Charlie = new("charlie");
    private static readonly LocationId Home = new("home");
    private static readonly LocationId RememberedLocation = new("remembered-location");
    private static readonly LocationId ActualLocation = new("actual-location");
    private static readonly LocationId ContactLocation = new("contact-location");
    private static readonly LocationId SafeLocation = new("safe-location");

    [Fact]
    public void Generate_CreatesAllGoalsFromObserverBeliefsAndLastKnownLocations()
    {
        Scenario scenario = CreateScenario();
        _ = scenario.Suspicion.ProcessMemory(Anna, scenario.SuspiciousMemory);

        IReadOnlyList<GoalCandidate> informedGoals = scenario.GoalSource.Generate(
            new NpcDecisionContext(
                scenario.World.GetEntity(Anna),
                scenario.AnnaProfile,
                At(0, 0)));
        IReadOnlyList<GoalCandidate> uninformedGoals = scenario.GoalSource.Generate(
            new NpcDecisionContext(
                scenario.World.GetEntity(Bob),
                scenario.BobProfile,
                At(0, 0)));

        Assert.Equal(
            [
                GoalType.ObserveTarget,
                GoalType.FollowTarget,
                GoalType.AskAboutTarget,
                GoalType.ShareSuspicion,
                GoalType.AvoidTarget,
            ],
            informedGoals.Select(goal => goal.Type));
        Assert.All(
            informedGoals.Where(goal => goal.Type is GoalType.ObserveTarget or GoalType.FollowTarget),
            goal => Assert.Equal(RememberedLocation, goal.Destination));
        Assert.DoesNotContain(informedGoals, goal => goal.Destination == ActualLocation);
        Assert.All(
            informedGoals.Where(goal => goal.Type is GoalType.AskAboutTarget or GoalType.ShareSuspicion),
            goal => Assert.Equal(ContactLocation, goal.Destination));
        Assert.Equal(
            SafeLocation,
            Assert.Single(informedGoals, goal => goal.Type == GoalType.AvoidTarget).Destination);
        Assert.Empty(uninformedGoals);
    }

    [Fact]
    public void Routine_ChangesBehaviorOnlyAfterEvidenceCreatesBelief()
    {
        Scenario scenario = CreateScenario();
        var brain = new UtilityNpcBrain([
            new ScheduleGoalSource(),
            scenario.GoalSource,
        ]);
        GoalCandidate beforeEvidence = brain.SelectGoal(new NpcDecisionContext(
            scenario.World.GetEntity(Anna),
            scenario.AnnaProfile,
            At(0, 0)));
        _ = scenario.Suspicion.ProcessMemory(Anna, scenario.SuspiciousMemory);
        var buffer = new WorldEventBuffer();
        var eventFactory = new WorldEventFactory(
            scenario.Clock,
            new SequentialEventIdGenerator());
        var movement = new MoveEntityActionHandler(scenario.World, eventFactory, buffer);
        var routine = new NpcRoutineSystem(scenario.Clock, scenario.World, movement, brain);
        routine.Register(scenario.AnnaProfile);

        NpcRoutineDecision afterEvidence = Assert.Single(routine.Tick(SimDelta.OneTick));

        Assert.Equal(GoalType.Idle, beforeEvidence.Type);
        Assert.Equal(Home, beforeEvidence.Destination);
        Assert.Equal(GoalType.AvoidTarget, afterEvidence.Goal.Type);
        Assert.Equal(SafeLocation, afterEvidence.Goal.Destination);
        Assert.Equal(SafeLocation, scenario.World.GetEntity(Anna).LogicalLocation);
        Assert.Equal(ActualLocation, scenario.World.GetEntity(George).LogicalLocation);
    }

    private static Scenario CreateScenario()
    {
        var world = new WorldState();
        foreach (LocationId location in new[]
        {
            Home,
            RememberedLocation,
            ActualLocation,
            ContactLocation,
            SafeLocation,
        })
        {
            world.AddLocation(new LocationState(location));
        }

        world.AddEntity(new EntityState(Anna, Home));
        world.AddEntity(new EntityState(Bob, Home));
        world.AddEntity(new EntityState(George, ActualLocation));
        world.AddEntity(new EntityState(Charlie, ContactLocation));

        var memories = new MemorySystem(
            world,
            new SequentialMemoryIdGenerator(),
            new ExponentialMemoryDecayPolicy(0.0, 0.0));
        MemoryRecord suspiciousMemory = Remember(
            memories,
            Anna,
            George,
            RememberedLocation,
            new EventId(1),
            [EventTag.Visible, EventTag.Restricted]);
        _ = Remember(
            memories,
            Anna,
            Charlie,
            ContactLocation,
            new EventId(2),
            [EventTag.Visible]);

        var beliefRule = new SuspicionRule(
            "dangerous_restricted_entry",
            EventType.EnterLocation,
            [EventTag.Restricted],
            memoryKind: null,
            [
                new SuspicionEffect(SuspicionDimension.Criminality, 30.0f),
                new SuspicionEffect(SuspicionDimension.Secrecy, 10.0f),
            ]);
        var suspicion = new SuspicionSystem(
            memories,
            new InMemorySuspicionRuleRepository([beliefRule]));
        var clock = new SimClock(ticksPerSecond: 1);
        NpcRoutineProfile annaProfile = CreateRoutineProfile(Anna);
        NpcRoutineProfile bobProfile = CreateRoutineProfile(Bob);
        var behaviorProfiles = new SuspicionBehaviorRepository([
            new SuspicionBehaviorProfile(Anna, [Charlie], SafeLocation),
            new SuspicionBehaviorProfile(Bob, [Charlie], SafeLocation),
        ]);
        var goalSource = new SuspicionDrivenGoalSource(
            suspicion,
            memories,
            clock,
            behaviorProfiles);
        return new Scenario(
            world,
            clock,
            suspicion,
            suspiciousMemory,
            goalSource,
            annaProfile,
            bobProfile);
    }

    private static NpcRoutineProfile CreateRoutineProfile(EntityId entity)
    {
        var role = new RolePermissions(
            new RoleId("resident"),
            [Home, RememberedLocation, ActualLocation, ContactLocation, SafeLocation]);
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

    private static MemoryRecord Remember(
        MemorySystem memories,
        EntityId observer,
        EntityId subject,
        LocationId location,
        EventId sourceEvent,
        IEnumerable<EventTag> tags)
    {
        var observation = new Observation(
            new ObservationId(sourceEvent.Value),
            sourceEvent,
            observer,
            subject,
            EventType.EnterLocation,
            location,
            tags,
            SimTime.Zero,
            confidence: 1.0f,
            salience: 1.0f,
            PerceptionChannel.Visual);
        return Assert.IsType<MemoryRecord>(memories.Remember(observation));
    }

    private static SimMinuteOfDay At(int hour, int minute) =>
        SimMinuteOfDay.FromHourMinute(hour, minute);

    private sealed record Scenario(
        WorldState World,
        SimClock Clock,
        SuspicionSystem Suspicion,
        MemoryRecord SuspiciousMemory,
        SuspicionDrivenGoalSource GoalSource,
        NpcRoutineProfile AnnaProfile,
        NpcRoutineProfile BobProfile);
}
