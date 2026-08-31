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
using Game.Sim.PlayerAi;
using Game.Sim.Roles;
using Game.Sim.Routines;
using Game.Sim.Schedules;
using Game.Sim.Suspicion;
using Game.Sim.Time;
using Game.Sim.World;

namespace Game.Sim.Scenarios;

public sealed class BasementScenario
{
    public static readonly EntityId Anna = new("anna");
    public static readonly EntityId Bob = new("bob");
    public static readonly EntityId George = new("george");
    public static readonly EntityId Charlie = new("charlie");
    public static readonly EntityId Dana = new("dana");
    public static readonly EntityId Evelyn = new("evelyn");
    public static readonly LocationId Lobby = new("lobby");
    public static readonly LocationId Basement = new("basement");

    private readonly ISuspicionRuleRepository _rules;

    public BasementScenario(ISuspicionRuleRepository rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        _rules = rules;
    }

    public BasementScenarioResult Run(BasementScenarioOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        WorldState world = CreateWorld();
        var clock = new SimClock(ticksPerSecond: 1);
        var buffer = new WorldEventBuffer();
        var eventFactory = new WorldEventFactory(clock, new SequentialEventIdGenerator());
        var patternSystem = new BehaviorPatternSystem(
            clock,
            new RuleBasedBehaviorPatternDetector(clock.TicksPerSecond),
            eventFactory,
            buffer);
        var interactions = new InteractionActionHandler(world, eventFactory, buffer, patternSystem);
        var probes = new BoundaryProbeActionHandler(world, eventFactory, buffer, patternSystem);
        var movement = new MoveEntityActionHandler(world, eventFactory, buffer);
        var perception = new PerceptionSystem(
            new LogicalPerceptionResolver(new SequentialObservationIdGenerator()));
        var memories = new MemorySystem(
            world,
            new SequentialMemoryIdGenerator(),
            new ExponentialMemoryDecayPolicy(0.0, 0.0));
        var suspicion = new SuspicionSystem(memories, _rules);
        var events = new List<WorldEvent>();
        var decisions = new List<NpcRoutineDecision>();
        var firstSuspicion = new Dictionary<EntityId, SimTime>();
        int observationCount = 0;

        clock.AdvanceOneTick();
        _ = interactions.Execute(new InteractionCommand(
            Bob,
            InteractionKind.Generic,
            "lobby-clock"));
        ProcessPendingEvents();

        clock.AdvanceOneTick();
        _ = movement.Execute(new MoveEntityCommand(Anna, Basement));
        ProcessPendingEvents();

        var playerDirector = new PlayerAiDirector(
            [new PlayerAiProfile(
                George,
                PlayerAiArchetype.Explorer,
                explorationObjectives: [new ExplorationObjective(
                    "explore-basement",
                    Basement,
                    "basement-door")])],
            interactions,
            probes);
        var playerRoutine = new NpcRoutineSystem(
            clock,
            world,
            movement,
            new UtilityNpcBrain([new ScheduleGoalSource(), playerDirector]),
            [playerDirector]);
        playerRoutine.Register(CreateRoutineProfile(George, Lobby));
        decisions.AddRange(playerRoutine.Tick(SimDelta.OneTick));
        WorldEvent[] georgeMovementEvents = buffer.Drain().ToArray();
        WorldEvent restrictedEntry = RequireSingle(
            georgeMovementEvents,
            worldEvent =>
                worldEvent.Actor == George &&
                worldEvent.Type == EventType.EnterLocation &&
                worldEvent.Tags.Contains(EventTag.Restricted),
            "George's restricted basement entry was not emitted exactly once.");
        ProcessEvents(georgeMovementEvents);

        MemoryRecord annaMemory = RequireSingle(
            memories.GetStore(Anna).Memories,
            memory => memory.RootEventId == restrictedEntry.Id,
            "Anna did not retain exactly one memory of George's basement entry.");
        SuspicionSnapshot annaSuspicion = suspicion.GetSnapshot(Anna, George, clock.Now);
        EnsureSuspicionExists(annaSuspicion, "Anna");

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
        routine.Register(CreateRoutineProfile(Anna, Basement));
        routine.Register(CreateRoutineProfile(Bob, Lobby));
        routine.Register(CreateRoutineProfile(Charlie, Lobby));
        routine.Register(CreateRoutineProfile(Dana, Lobby));
        routine.Register(CreateRoutineProfile(Evelyn, Lobby));

        while (clock.Now.Tick < options.Ticks)
        {
            IReadOnlyList<NpcRoutineDecision> tickDecisions = routine.Tick(SimDelta.OneTick);
            decisions.AddRange(tickDecisions);
            ProcessPendingEvents();
        }

        NpcRoutineDecision annaInitialDecision = RequireSingle(
            decisions,
            decision => decision.Entity == Anna && decision.Time.Tick == 4,
            "Anna's first feedback-loop decision was not emitted exactly once.");
        NpcRoutineDecision bobInitialDecision = RequireSingle(
            decisions,
            decision => decision.Entity == Bob && decision.Time.Tick == 4,
            "Bob's first feedback-loop decision was not emitted exactly once.");
        MemoryRecord bobRumor = RequireSingle(
            memories.GetStore(Bob).Memories,
            memory =>
                memory.Kind == MemoryKind.Social &&
                memory.RootEventId == restrictedEntry.Id,
            "Bob did not retain exactly one social memory of George's basement entry.");
        SuspicionSnapshot bobSuspicion = suspicion.GetSnapshot(Bob, George, clock.Now);
        EnsureSuspicionExists(bobSuspicion, "Bob");

        return new BasementScenarioResult(
            options.Seed,
            clock.Now,
            events,
            decisions,
            CollectMemories(world, memories),
            CollectActors(world),
            observationCount,
            restrictedEntry,
            annaMemory,
            bobRumor,
            annaSuspicion,
            bobSuspicion,
            firstSuspicion[Anna],
            firstSuspicion.GetValueOrDefault(Bob, bobInitialDecision.Time),
            annaInitialDecision,
            bobInitialDecision,
            world.GetEntity(Anna).LogicalLocation,
            world.GetEntity(Bob).LogicalLocation,
            world.GetEntity(George).LogicalLocation);

        void ProcessPendingEvents() => ProcessEvents(buffer.Drain());

        void ProcessEvents(IEnumerable<WorldEvent> pendingEvents)
        {
            foreach (WorldEvent worldEvent in pendingEvents)
            {
                events.Add(worldEvent);
                foreach (Observation observation in perception.Process(worldEvent, world))
                {
                    observationCount++;
                    MemoryRecord? memory = memories.Remember(observation);
                    if (memory is null)
                    {
                        continue;
                    }

                    int contributionsAdded = suspicion.ProcessMemory(observation.Observer, memory);
                    if (contributionsAdded > 0)
                    {
                        firstSuspicion.TryAdd(observation.Observer, clock.Now);
                    }
                }
            }

            SuspicionSnapshot bobSnapshot = suspicion.GetSnapshot(Bob, George, clock.Now);
            if (bobSnapshot.Evidence.Count > 0)
            {
                firstSuspicion.TryAdd(Bob, clock.Now);
            }
        }
    }

    private static WorldState CreateWorld()
    {
        var world = new WorldState();
        world.AddLocation(new LocationState(Lobby));
        world.AddLocation(new LocationState(Basement, isRestricted: true));
        world.AddEntity(new EntityState(Anna, Lobby));
        world.AddEntity(new EntityState(Bob, Lobby));
        world.AddEntity(new EntityState(George, Lobby));
        world.AddEntity(new EntityState(Charlie, Lobby));
        world.AddEntity(new EntityState(Dana, Lobby));
        world.AddEntity(new EntityState(Evelyn, Lobby));
        return world;
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

    private static OwnedMemory[] CollectMemories(
        WorldState world,
        MemorySystem memories) =>
        world.Entities
            .OrderBy(entity => entity.Id.Value, StringComparer.Ordinal)
            .SelectMany(entity => memories
                .GetStore(entity.Id)
                .Memories
                .Select(memory => new OwnedMemory(entity.Id, memory)))
            .ToArray();

    private static ScenarioActorSnapshot[] CollectActors(WorldState world) =>
        world.Entities
            .OrderBy(entity => entity.Id.Value, StringComparer.Ordinal)
            .Select(entity => new ScenarioActorSnapshot(entity.Id, entity.LogicalLocation))
            .ToArray();

    private static T RequireSingle<T>(
        IEnumerable<T> source,
        Func<T, bool> predicate,
        string message)
    {
        T[] matches = source.Where(predicate).Take(2).ToArray();
        return matches.Length == 1
            ? matches[0]
            : throw new InvalidOperationException(message);
    }

    private static void EnsureSuspicionExists(SuspicionSnapshot snapshot, string observer)
    {
        if (snapshot.Evidence.Count == 0)
        {
            throw new InvalidOperationException(
                $"{observer} did not derive suspicion from the basement evidence.");
        }
    }
}
