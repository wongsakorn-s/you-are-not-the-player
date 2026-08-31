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

public sealed class BasementScenarioSession
{
    private static readonly LocationId Hallway = new("hallway");

    private readonly BasementScenarioOptions _options;
    private readonly SimClock _clock = new(ticksPerSecond: 1);
    private readonly WorldState _world;
    private readonly WorldEventBuffer _buffer = new();
    private readonly CoordinatedNpcMovementExecutor _movement;
    private readonly InteractionActionHandler _interactions;
    private readonly PerceptionSystem _perception;
    private readonly MemorySystem _memories;
    private readonly SuspicionSystem _suspicion;
    private readonly NpcRoutineSystem _playerRoutine;
    private readonly NpcRoutineSystem _behaviorRoutine;
    private readonly List<WorldEvent> _events = [];
    private readonly List<WorldEvent> _newEvents = [];
    private readonly List<NpcRoutineDecision> _decisions = [];
    private readonly Dictionary<EntityId, SimTime> _firstSuspicion = [];
    private WorldEvent? _restrictedEntry;
    private NpcRoutineDecision? _annaInitialDecision;
    private NpcRoutineDecision? _bobInitialDecision;
    private int _observationCount;

    public BasementScenarioSession(
        ISuspicionRuleRepository rules,
        BasementScenarioOptions options,
        bool autoCompleteMovements = false)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
        _world = CreateWorld();

        var eventFactory = new WorldEventFactory(_clock, new SequentialEventIdGenerator());
        var patterns = new BehaviorPatternSystem(
            _clock,
            new RuleBasedBehaviorPatternDetector(_clock.TicksPerSecond),
            eventFactory,
            _buffer);
        _interactions = new InteractionActionHandler(_world, eventFactory, _buffer, patterns);
        var probes = new BoundaryProbeActionHandler(_world, eventFactory, _buffer, patterns);
        var movementHandler = new MoveEntityActionHandler(_world, eventFactory, _buffer);
        var access = new PortalAccessPolicy();
        access.SetAccess("basement-door", isAccessible: true);
        var coordinator = new LiveMovementCoordinator(
            _world,
            CreateLocationGraph(),
            access,
            movementHandler);
        _movement = new CoordinatedNpcMovementExecutor(coordinator, autoCompleteMovements);
        _perception = new PerceptionSystem(
            new LogicalPerceptionResolver(new SequentialObservationIdGenerator()));
        _memories = new MemorySystem(
            _world,
            new SequentialMemoryIdGenerator(),
            new ExponentialMemoryDecayPolicy(0.0, 0.0));
        _suspicion = new SuspicionSystem(_memories, rules);

        var playerDirector = new PlayerAiDirector(
            [new PlayerAiProfile(
                BasementScenario.George,
                PlayerAiArchetype.Explorer,
                explorationObjectives: [new ExplorationObjective(
                    "explore-basement",
                    BasementScenario.Basement,
                    "basement-door")])],
            _interactions,
            probes);
        _playerRoutine = new NpcRoutineSystem(
            _clock,
            _world,
            _movement,
            new UtilityNpcBrain([new ScheduleGoalSource(), playerDirector]),
            [playerDirector]);
        _playerRoutine.Register(CreateRoutineProfile(
            BasementScenario.George,
            BasementScenario.Lobby));

        var behaviorProfiles = new SuspicionBehaviorRepository([
            new SuspicionBehaviorProfile(
                BasementScenario.Anna,
                [BasementScenario.Bob],
                BasementScenario.Lobby),
            new SuspicionBehaviorProfile(
                BasementScenario.Bob,
                [],
                BasementScenario.Lobby),
        ]);
        var behaviorGoals = new SuspicionDrivenGoalSource(
            _suspicion,
            _memories,
            _clock,
            behaviorProfiles);
        var behaviorActions = new SuspicionBehaviorActionSystem(
            _clock,
            _world,
            _memories,
            _suspicion,
            eventFactory,
            _buffer);
        _behaviorRoutine = new NpcRoutineSystem(
            _clock,
            _world,
            _movement,
            new UtilityNpcBrain([new ScheduleGoalSource(), behaviorGoals]),
            [behaviorActions]);
        _behaviorRoutine.Register(CreateRoutineProfile(
            BasementScenario.Anna,
            BasementScenario.Basement));
        _behaviorRoutine.Register(CreateRoutineProfile(
            BasementScenario.Bob,
            BasementScenario.Lobby));
        _behaviorRoutine.Register(CreateRoutineProfile(
            BasementScenario.Charlie,
            BasementScenario.Lobby));
        _behaviorRoutine.Register(CreateRoutineProfile(
            BasementScenario.Dana,
            BasementScenario.Lobby));
        _behaviorRoutine.Register(CreateRoutineProfile(
            BasementScenario.Evelyn,
            BasementScenario.Lobby));
    }

    public ulong Seed => _options.Seed;

    public SimTime Now => _clock.Now;

    public int MinimumTicks => _options.Ticks;

    public BasementSessionPhase Phase { get; private set; } =
        BasementSessionPhase.InitialInteraction;

    public bool IsComplete => Phase == BasementSessionPhase.Completed;

    public IReadOnlyList<WorldEvent> Events => _events;

    public IReadOnlyList<NpcRoutineDecision> Decisions => _decisions;

    public IReadOnlyList<MovementSnapshot> PendingMovements => _movement.PendingMovements;

    public int ObservationCount => _observationCount;

    public bool AdvanceOneTick()
    {
        if (IsComplete)
        {
            return false;
        }

        switch (Phase)
        {
            case BasementSessionPhase.InitialInteraction:
                RunInitialInteraction();
                break;
            case BasementSessionPhase.WitnessMovement:
                RequestWitnessMovement();
                break;
            case BasementSessionPhase.WaitingForWitness:
            case BasementSessionPhase.WaitingForExplorer:
                _clock.AdvanceOneTick();
                break;
            case BasementSessionPhase.ExplorerMovement:
                RunExplorerDecision();
                break;
            case BasementSessionPhase.FeedbackLoop:
                RunFeedbackTick();
                break;
            case BasementSessionPhase.Completed:
                return false;
            default:
                throw new InvalidOperationException($"Unsupported session phase '{Phase}'.");
        }

        TryCompleteSession();
        return true;
    }

    public MovementSnapshot CompleteMovement(MovementRequestId requestId)
    {
        MovementSnapshot movement = _movement.Complete(requestId);
        AcknowledgeRoutineCompletion(movement.Actor);
        ProcessPendingEvents();

        if (Phase == BasementSessionPhase.WaitingForWitness &&
            movement.Actor == BasementScenario.Anna)
        {
            Phase = BasementSessionPhase.ExplorerMovement;
        }
        else if (Phase == BasementSessionPhase.WaitingForExplorer &&
                 movement.Actor == BasementScenario.George)
        {
            Phase = BasementSessionPhase.FeedbackLoop;
        }

        TryCompleteSession();
        return movement;
    }

    public MovementSnapshot FailMovement(
        MovementRequestId requestId,
        MovementFailureReason reason = MovementFailureReason.PhysicalPathUnavailable)
    {
        MovementSnapshot movement = _movement.Fail(requestId, reason);
        AcknowledgeRoutineFailure(movement.Actor);
        if (Phase == BasementSessionPhase.WaitingForWitness &&
            movement.Actor == BasementScenario.Anna)
        {
            Phase = BasementSessionPhase.WitnessMovement;
        }
        else if (Phase == BasementSessionPhase.WaitingForExplorer &&
                 movement.Actor == BasementScenario.George)
        {
            Phase = BasementSessionPhase.ExplorerMovement;
        }

        return movement;
    }

    public IReadOnlyList<WorldEvent> DrainNewEvents()
    {
        WorldEvent[] events = [.. _newEvents];
        _newEvents.Clear();
        return events;
    }

    public LocationId GetLogicalLocation(EntityId actor) =>
        _world.GetEntity(actor).LogicalLocation;

    public IReadOnlyList<MemoryRecord> GetMemories(EntityId actor) =>
        _memories.GetStore(actor).Memories;

    public SuspicionSnapshot GetSuspicion(EntityId observer, EntityId subject) =>
        _suspicion.GetSnapshot(observer, subject, _clock.Now);

    public void Interact(EntityId actor, string interactionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(interactionId);
        _ = _interactions.Execute(new InteractionCommand(
            actor,
            InteractionKind.Generic,
            interactionId));
        ProcessPendingEvents();
    }

    public BasementScenarioResult BuildResult()
    {
        if (!IsComplete ||
            _restrictedEntry is null ||
            _annaInitialDecision is null ||
            _bobInitialDecision is null)
        {
            throw new InvalidOperationException("Basement session has not completed its milestone.");
        }

        MemoryRecord annaMemory = RequireSingle(
            _memories.GetStore(BasementScenario.Anna).Memories,
            memory => memory.RootEventId == _restrictedEntry.Id,
            "Anna did not retain exactly one memory of George's basement entry.");
        MemoryRecord bobRumor = RequireSingle(
            _memories.GetStore(BasementScenario.Bob).Memories,
            memory =>
                memory.Kind == MemoryKind.Social &&
                memory.RootEventId == _restrictedEntry.Id,
            "Bob did not retain exactly one social memory of George's basement entry.");
        SuspicionSnapshot annaSuspicion = GetSuspicion(
            BasementScenario.Anna,
            BasementScenario.George);
        SuspicionSnapshot bobSuspicion = GetSuspicion(
            BasementScenario.Bob,
            BasementScenario.George);
        EnsureSuspicionExists(annaSuspicion, "Anna");
        EnsureSuspicionExists(bobSuspicion, "Bob");

        return new BasementScenarioResult(
            _options.Seed,
            _clock.Now,
            _events,
            _decisions,
            CollectMemories(_world, _memories),
            CollectActors(_world),
            _observationCount,
            _restrictedEntry,
            annaMemory,
            bobRumor,
            annaSuspicion,
            bobSuspicion,
            _firstSuspicion[BasementScenario.Anna],
            _firstSuspicion.GetValueOrDefault(
                BasementScenario.Bob,
                _bobInitialDecision.Time),
            _annaInitialDecision,
            _bobInitialDecision,
            GetLogicalLocation(BasementScenario.Anna),
            GetLogicalLocation(BasementScenario.Bob),
            GetLogicalLocation(BasementScenario.George));
    }

    private void RunInitialInteraction()
    {
        _clock.AdvanceOneTick();
        _ = _interactions.Execute(new InteractionCommand(
            BasementScenario.Bob,
            InteractionKind.Generic,
            "lobby-clock"));
        ProcessPendingEvents();
        Phase = BasementSessionPhase.WitnessMovement;
    }

    private void RequestWitnessMovement()
    {
        _clock.AdvanceOneTick();
        NpcMovementExecution execution = _movement.Execute(new MoveEntityCommand(
            BasementScenario.Anna,
            BasementScenario.Basement));
        RequireMovementAccepted(execution, BasementScenario.Anna);
        ProcessPendingEvents();
        Phase = execution.Status == NpcMovementExecutionStatus.Pending
            ? BasementSessionPhase.WaitingForWitness
            : BasementSessionPhase.ExplorerMovement;
    }

    private void RunExplorerDecision()
    {
        IReadOnlyList<NpcRoutineDecision> decisions = _playerRoutine.Tick(SimDelta.OneTick);
        _decisions.AddRange(decisions);
        ProcessPendingEvents();
        Phase = _movement.IsBusy(BasementScenario.George)
            ? BasementSessionPhase.WaitingForExplorer
            : BasementSessionPhase.FeedbackLoop;
    }

    private void RunFeedbackTick()
    {
        IReadOnlyList<NpcRoutineDecision> decisions = _behaviorRoutine.Tick(SimDelta.OneTick);
        foreach (NpcRoutineDecision decision in decisions)
        {
            if (decision.Entity == BasementScenario.Anna && _annaInitialDecision is null)
            {
                _annaInitialDecision = decision;
            }

            if (decision.Entity == BasementScenario.Bob &&
                decision.Goal.Type == GoalType.FollowTarget &&
                _bobInitialDecision is null)
            {
                _bobInitialDecision = decision;
            }
        }

        _decisions.AddRange(decisions);
        ProcessPendingEvents();
    }

    private void AcknowledgeRoutineCompletion(EntityId actor)
    {
        if (_playerRoutine.HasPendingMovement(actor))
        {
            _playerRoutine.AcknowledgeMovementCompleted(actor);
        }
        else if (_behaviorRoutine.HasPendingMovement(actor))
        {
            _behaviorRoutine.AcknowledgeMovementCompleted(actor);
        }
    }

    private void AcknowledgeRoutineFailure(EntityId actor)
    {
        if (_playerRoutine.HasPendingMovement(actor))
        {
            _playerRoutine.AcknowledgeMovementFailed(actor);
        }
        else if (_behaviorRoutine.HasPendingMovement(actor))
        {
            _behaviorRoutine.AcknowledgeMovementFailed(actor);
        }
    }

    private void ProcessPendingEvents()
    {
        foreach (WorldEvent worldEvent in _buffer.Drain())
        {
            _events.Add(worldEvent);
            _newEvents.Add(worldEvent);
            if (worldEvent.Actor == BasementScenario.George &&
                worldEvent.Type == EventType.EnterLocation &&
                worldEvent.Tags.Contains(EventTag.Restricted))
            {
                _restrictedEntry ??= worldEvent;
            }

            foreach (Observation observation in _perception.Process(worldEvent, _world))
            {
                _observationCount++;
                MemoryRecord? memory = _memories.Remember(observation);
                if (memory is null)
                {
                    continue;
                }

                int added = _suspicion.ProcessMemory(observation.Observer, memory);
                if (added > 0)
                {
                    _firstSuspicion.TryAdd(observation.Observer, _clock.Now);
                }
            }
        }

        SuspicionSnapshot bobSnapshot = GetSuspicion(
            BasementScenario.Bob,
            BasementScenario.George);
        if (bobSnapshot.Evidence.Count > 0)
        {
            _firstSuspicion.TryAdd(BasementScenario.Bob, _clock.Now);
        }
    }

    private void TryCompleteSession()
    {
        bool milestoneComplete =
            _restrictedEntry is not null &&
            _annaInitialDecision is not null &&
            _bobInitialDecision is not null &&
            _memories.GetStore(BasementScenario.Bob).Memories.Any(memory =>
                memory.Kind == MemoryKind.Social &&
                memory.RootEventId == _restrictedEntry.Id) &&
            GetLogicalLocation(BasementScenario.Bob) == BasementScenario.Basement;
        if (_clock.Now.Tick >= _options.Ticks &&
            milestoneComplete &&
            _movement.PendingMovements.Count == 0)
        {
            Phase = BasementSessionPhase.Completed;
        }
    }

    private static WorldState CreateWorld()
    {
        var world = new WorldState();
        world.AddLocation(new LocationState(BasementScenario.Lobby));
        world.AddLocation(new LocationState(BasementScenario.Basement, isRestricted: true));
        world.AddEntity(new EntityState(BasementScenario.Anna, BasementScenario.Lobby));
        world.AddEntity(new EntityState(BasementScenario.Bob, BasementScenario.Lobby));
        world.AddEntity(new EntityState(BasementScenario.George, BasementScenario.Lobby));
        world.AddEntity(new EntityState(BasementScenario.Charlie, BasementScenario.Lobby));
        world.AddEntity(new EntityState(BasementScenario.Dana, BasementScenario.Lobby));
        world.AddEntity(new EntityState(BasementScenario.Evelyn, BasementScenario.Lobby));
        return world;
    }

    private static LocationGraph CreateLocationGraph()
    {
        var graph = new LocationGraph();
        graph.AddLocation(BasementScenario.Lobby);
        graph.AddLocation(Hallway);
        graph.AddLocation(BasementScenario.Basement);
        graph.ConnectBidirectional(BasementScenario.Lobby, Hallway, "lobby-arch");
        graph.ConnectBidirectional(
            Hallway,
            BasementScenario.Basement,
            "basement-door",
            requiresAccess: true);
        return graph;
    }

    private static NpcRoutineProfile CreateRoutineProfile(
        EntityId entity,
        LocationId scheduleLocation)
    {
        var role = new RolePermissions(
            new RoleId("resident"),
            [BasementScenario.Lobby, BasementScenario.Basement]);
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

    private static void RequireMovementAccepted(
        NpcMovementExecution execution,
        EntityId actor)
    {
        if (execution.Status == NpcMovementExecutionStatus.Failed)
        {
            throw new InvalidOperationException(
                $"Movement for '{actor}' failed with '{execution.Movement?.FailureReason}'.");
        }
    }

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
