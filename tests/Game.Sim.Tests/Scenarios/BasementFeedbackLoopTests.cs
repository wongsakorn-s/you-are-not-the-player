using System.Globalization;
using Game.Sim.Actions;
using Game.Sim.Behaviors;
using Game.Sim.Brain;
using Game.Sim.Entities;
using Game.Sim.Events;
using Game.Sim.Locations;
using Game.Sim.Memory;
using Game.Sim.Needs;
using Game.Sim.Patterns;
using Game.Sim.Perception;
using Game.Sim.Roles;
using Game.Sim.Routines;
using Game.Sim.Schedules;
using Game.Sim.Suspicion;
using Game.Sim.Time;
using Game.Sim.World;

namespace Game.Sim.Tests.Scenarios;

public sealed class BasementFeedbackLoopTests
{
    private static readonly EntityId Anna = new("anna");
    private static readonly EntityId Bob = new("bob");
    private static readonly EntityId George = new("george");
    private static readonly LocationId Lobby = new("lobby");
    private static readonly LocationId Basement = new("basement");

    [Fact]
    public void Scenario_ClosesEventMemoryRumorSuspicionBehaviorLoopDeterministically()
    {
        ScenarioResult first = RunScenario();
        ScenarioResult second = RunScenario();

        Assert.Equal(first.EventFingerprint, second.EventFingerprint);
        Assert.Equal(MemoryKind.Episodic, first.AnnaMemoryKind);
        Assert.Equal(MemoryKind.Social, first.BobMemoryKind);
        Assert.Equal(first.RestrictedEntryRoot, first.BobRumorRoot);
        Assert.Equal(Anna, first.BobInformationSource);
        Assert.Equal(19.0f, first.AnnaSuspicion.RoleDeviation, precision: 5);
        Assert.Equal(7.6f, first.AnnaSuspicion.Secrecy, precision: 5);
        Assert.Equal(16.15f, first.BobSuspicion.RoleDeviation, precision: 5);
        Assert.Equal(6.46f, first.BobSuspicion.Secrecy, precision: 5);
        Assert.Equal(GoalType.ShareSuspicion, first.AnnaDecision.Goal.Type);
        Assert.Equal(Bob, first.AnnaDecision.Goal.InteractionPartner);
        Assert.Equal(GoalType.FollowTarget, first.BobDecision.Goal.Type);
        Assert.Equal(George, first.BobDecision.Goal.Target);
        Assert.Equal(Basement, first.BobDecision.Goal.Destination);
        Assert.True(first.ShareEventCreated);
        Assert.True(first.FollowEnterEventCreated);
        Assert.Equal(Basement, first.BobFinalLocation);
        Assert.Equal(Basement, first.GeorgeFinalLocation);
    }

    private static ScenarioResult RunScenario()
    {
        var world = new WorldState();
        world.AddLocation(new LocationState(Lobby));
        world.AddLocation(new LocationState(Basement, isRestricted: true));
        world.AddEntity(new EntityState(Anna, Lobby));
        world.AddEntity(new EntityState(Bob, Lobby));
        world.AddEntity(new EntityState(George, Lobby));

        var clock = new SimClock(ticksPerSecond: 1);
        var buffer = new WorldEventBuffer();
        var eventFactory = new WorldEventFactory(clock, new SequentialEventIdGenerator());
        var patternDetector = new RuleBasedBehaviorPatternDetector(clock.TicksPerSecond);
        var patternSystem = new BehaviorPatternSystem(
            clock,
            patternDetector,
            eventFactory,
            buffer);
        var interactions = new InteractionActionHandler(
            world,
            eventFactory,
            buffer,
            patternSystem);
        var movement = new MoveEntityActionHandler(world, eventFactory, buffer);
        var perception = new PerceptionSystem(
            new LogicalPerceptionResolver(new SequentialObservationIdGenerator()));
        var memories = new MemorySystem(
            world,
            new SequentialMemoryIdGenerator(),
            new ExponentialMemoryDecayPolicy(0.0, 0.0));
        var suspicion = new SuspicionSystem(memories, LoadRules());
        var eventFingerprint = new List<string>();

        clock.AdvanceOneTick();
        _ = interactions.Execute(new InteractionCommand(
            Bob,
            InteractionKind.Generic,
            "lobby-clock"));
        ProcessPendingEvents(buffer, world, perception, memories, suspicion, eventFingerprint);

        clock.AdvanceOneTick();
        _ = movement.Execute(new MoveEntityCommand(Anna, Basement));
        ProcessPendingEvents(buffer, world, perception, memories, suspicion, eventFingerprint);

        clock.AdvanceOneTick();
        _ = movement.Execute(new MoveEntityCommand(George, Basement));
        WorldEvent[] georgeMovementEvents = buffer.Drain().ToArray();
        WorldEvent restrictedEntry = Assert.Single(
            georgeMovementEvents,
            worldEvent =>
                worldEvent.Actor == George &&
                worldEvent.Type == EventType.EnterLocation &&
                worldEvent.Tags.Contains(EventTag.Restricted));
        ProcessEvents(
            georgeMovementEvents,
            world,
            perception,
            memories,
            suspicion,
            eventFingerprint);

        MemoryRecord annaMemory = Assert.Single(
            memories.GetStore(Anna).Memories,
            memory => memory.RootEventId == restrictedEntry.Id);
        SuspicionVector annaSuspicion = suspicion
            .GetSnapshot(Anna, George, clock.Now)
            .Vector;

        var behaviorProfiles = new SuspicionBehaviorRepository([
            new SuspicionBehaviorProfile(Anna, [Bob], Lobby),
            new SuspicionBehaviorProfile(Bob, [], Lobby),
        ]);
        var behaviorGoals = new SuspicionDrivenGoalSource(
            suspicion,
            memories,
            clock,
            behaviorProfiles);
        var behaviorActions = new SuspicionBehaviorActionSystem(
            clock,
            world,
            memories,
            suspicion,
            eventFactory,
            buffer);
        var brain = new UtilityNpcBrain([new ScheduleGoalSource(), behaviorGoals]);
        var routine = new NpcRoutineSystem(
            clock,
            world,
            movement,
            brain,
            [behaviorActions]);
        NpcRoutineProfile annaProfile = CreateRoutineProfile(Anna, Basement);
        NpcRoutineProfile bobProfile = CreateRoutineProfile(Bob, Lobby);
        routine.Register(annaProfile);
        routine.Register(bobProfile);

        IReadOnlyList<NpcRoutineDecision> decisions = routine.Tick(SimDelta.OneTick);
        NpcRoutineDecision annaDecision = Assert.Single(
            decisions,
            decision => decision.Entity == Anna);
        NpcRoutineDecision bobDecision = Assert.Single(
            decisions,
            decision => decision.Entity == Bob);
        WorldEvent[] feedbackEvents = buffer.Drain().ToArray();
        eventFingerprint.AddRange(feedbackEvents.Select(FormatEvent));

        MemoryRecord bobRumor = Assert.Single(
            memories.GetStore(Bob).Memories,
            memory =>
                memory.Kind == MemoryKind.Social &&
                memory.RootEventId == restrictedEntry.Id);
        SuspicionVector bobSuspicion = suspicion
            .GetSnapshot(Bob, George, clock.Now)
            .Vector;
        IReadOnlyList<GoalCandidate> annaNextGoals = behaviorGoals.Generate(
            new NpcDecisionContext(world.GetEntity(Anna), annaProfile, clock.TimeOfDay));
        Assert.DoesNotContain(
            annaNextGoals,
            goal => goal.Type == GoalType.ShareSuspicion && goal.Target == George);
        bool shareEventCreated = feedbackEvents.Any(worldEvent =>
            worldEvent.Actor == Anna &&
            worldEvent.Type == EventType.ShareInformation &&
            worldEvent.Target == Bob);
        bool followEnterEventCreated = feedbackEvents.Any(worldEvent =>
            worldEvent.Actor == Bob &&
            worldEvent.Type == EventType.EnterLocation &&
            worldEvent.Location == Basement);

        return new ScenarioResult(
            eventFingerprint,
            annaMemory.Kind,
            bobRumor.Kind,
            restrictedEntry.Id,
            bobRumor.RootEventId,
            bobRumor.InformationSource,
            annaSuspicion,
            bobSuspicion,
            annaDecision,
            bobDecision,
            shareEventCreated,
            followEnterEventCreated,
            world.GetEntity(Bob).LogicalLocation,
            world.GetEntity(George).LogicalLocation);
    }

    private static void ProcessPendingEvents(
        WorldEventBuffer buffer,
        WorldState world,
        PerceptionSystem perception,
        MemorySystem memories,
        SuspicionSystem suspicion,
        ICollection<string> fingerprint) =>
        ProcessEvents(
            buffer.Drain(),
            world,
            perception,
            memories,
            suspicion,
            fingerprint);

    private static void ProcessEvents(
        IEnumerable<WorldEvent> worldEvents,
        WorldState world,
        PerceptionSystem perception,
        MemorySystem memories,
        SuspicionSystem suspicion,
        ICollection<string> fingerprint)
    {
        foreach (WorldEvent worldEvent in worldEvents)
        {
            fingerprint.Add(FormatEvent(worldEvent));
            foreach (Observation observation in perception.Process(worldEvent, world))
            {
                MemoryRecord? memory = memories.Remember(observation);
                if (memory is not null)
                {
                    _ = suspicion.ProcessMemory(observation.Observer, memory);
                }
            }
        }
    }

    private static NpcRoutineProfile CreateRoutineProfile(
        EntityId entity,
        LocationId scheduleLocation)
    {
        var role = new RolePermissions(new RoleId("resident"), [Lobby, Basement]);
        var schedule = new DailySchedule([
            new ScheduleEntry(
                SimMinuteOfDay.FromHourMinute(0, 0),
                SimMinuteOfDay.FromHourMinute(12, 0),
                RoutineActivity.Idle,
                scheduleLocation,
                utility: 10.0f),
            new ScheduleEntry(
                SimMinuteOfDay.FromHourMinute(12, 0),
                SimMinuteOfDay.FromHourMinute(0, 0),
                RoutineActivity.Idle,
                scheduleLocation,
                utility: 10.0f),
        ]);
        return new NpcRoutineProfile(
            entity,
            role,
            schedule,
            new NeedState(),
            new NeedProfile(new NeedRates(0.0, 0.0, 0.0), 0.0, 0.0, 0.0),
            new NeedDestinations(scheduleLocation, scheduleLocation, scheduleLocation));
    }

    private static InMemorySuspicionRuleRepository LoadRules()
    {
        string rulesPath = Path.Combine(
            AppContext.BaseDirectory,
            "Data",
            "SuspicionRules",
            "mvp.json");
        return JsonSuspicionRuleParser.Parse(File.ReadAllText(rulesPath));
    }

    private static string FormatEvent(WorldEvent worldEvent)
    {
        string payload = worldEvent.Payload switch
        {
            InformationExchangePayload information =>
                $"{information.Subject.Value}:{information.RootEventId.Value.ToString(CultureInfo.InvariantCulture)}",
            LocationTransitionPayload transition =>
                $"{transition.Origin.Value}>{transition.Destination.Value}",
            InteractionPayload interaction => interaction.InteractionId,
            _ => string.Empty,
        };
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{worldEvent.Id.Value}|{worldEvent.Time.Tick}|{worldEvent.Actor.Value}|{worldEvent.Type}|{worldEvent.Location.Value}|{payload}");
    }

    private sealed record ScenarioResult(
        IReadOnlyList<string> EventFingerprint,
        MemoryKind AnnaMemoryKind,
        MemoryKind BobMemoryKind,
        EventId RestrictedEntryRoot,
        EventId BobRumorRoot,
        EntityId? BobInformationSource,
        SuspicionVector AnnaSuspicion,
        SuspicionVector BobSuspicion,
        NpcRoutineDecision AnnaDecision,
        NpcRoutineDecision BobDecision,
        bool ShareEventCreated,
        bool FollowEnterEventCreated,
        LocationId BobFinalLocation,
        LocationId GeorgeFinalLocation);
}
