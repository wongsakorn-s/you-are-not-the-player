using Game.Sim.Actions;
using Game.Sim.Anomalies;
using Game.Sim.Behaviors;
using Game.Sim.Cases;
using Game.Sim.Brain;
using Game.Sim.Conspiracy;
using Game.Sim.Entities;
using Game.Sim.Events;
using Game.Sim.Locations;
using Game.Sim.Memory;
using Game.Sim.Needs;
using Game.Sim.Objects;
using Game.Sim.Patterns;
using Game.Sim.Perception;
using Game.Sim.Player;
using Game.Sim.PlayerAi;
using Game.Sim.Roles;
using Game.Sim.Routines;
using Game.Sim.Schedules;
using Game.Sim.Secrets;
using Game.Sim.Snapshots;
using Game.Sim.Suspicion;
using Game.Sim.Time;
using Game.Sim.World;

namespace Game.Sim.Scenarios;

public sealed class BasementScenarioSession
{
    private static readonly LocationId Hallway = new("hallway");

    private readonly BasementScenarioOptions _options;
    private readonly SimClock _clock;
    private readonly WorldState _world;
    private readonly WorldEventBuffer _buffer = new();
    private readonly CoordinatedNpcMovementExecutor _movement;
    private readonly InteractionActionHandler _interactions;
    private readonly HotelObjectRegistry _objects;
    private readonly ObjectActionHandler _objectActions;
    private readonly RealityAnomalySystem _anomalies;
    private readonly ConspiracySystem _conspiracy;
    private readonly PerceptionSystem _perception;
    private readonly MemorySystem _memories;
    private readonly SuspicionSystem _suspicion;
    private readonly DialogueSystem _dialogue;
    private readonly PlayerSessionController _playerController;
    private readonly NpcRoutineSystem _playerRoutine;
    private readonly NpcRoutineSystem _behaviorRoutine;
    private readonly List<WorldEvent> _events = [];
    private readonly List<WorldEvent> _newEvents = [];
    private readonly List<NpcRoutineDecision> _decisions = [];
    private readonly Dictionary<EntityId, SimTime> _firstSuspicion = [];
    private readonly RoleDutySystem? _duties;
    private readonly SecretPlanRepository? _secretPlans;
    private readonly Queue<AnomalyBeat> _pendingAnomalies = new();
    private readonly EntityId _hiddenPlayer;
    private readonly PlayerAiArchetype _hiddenPlayerArchetype;
    private WorldEvent? _restrictedEntry;
    private NpcRoutineDecision? _annaInitialDecision;
    private NpcRoutineDecision? _bobInitialDecision;
    private int _observationCount;

    public BasementScenarioSession(
        ISuspicionRuleRepository rules,
        BasementScenarioOptions options,
        bool autoCompleteMovements = false,
        long initialTick = 0,
        long firstEventId = 1,
        long firstMemoryId = 1,
        long firstObservationId = 1)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(options);
        _options = options;

        // One tick is one minute of the night, starting at 23:00, so the schedules
        // line up with the clock the player is shown. Without a truth this stays
        // the real-time clock the pinned scenario fingerprints were taken against.
        _clock = options.Truth is null
            ? new SimClock(ticksPerSecond: 1)
            : new SimClock(
                ticksPerSecond: 1,
                startOfDay: SimMinuteOfDay.FromHourMinute(23, 0),
                ticksPerMinute: 1);

        // Without a truth the session keeps its scripted arrangement, which is what
        // the regression scenarios and their pinned fingerprints depend on.
        _hiddenPlayer = options.Truth?.HiddenPlayer ?? BasementScenario.George;
        _hiddenPlayerArchetype = options.Truth?.HiddenPlayerArchetype ?? PlayerAiArchetype.Explorer;
        _world = CreateWorld();

        if (initialTick > 0)
        {
            _clock.Advance(new SimDelta(initialTick));
        }

        var eventFactory = new WorldEventFactory(_clock, new SequentialEventIdGenerator(Math.Max(1, firstEventId)));
        var patterns = new BehaviorPatternSystem(
            _clock,
            new RuleBasedBehaviorPatternDetector(
                _clock.TicksPerSecond,
                options.Truth is null ? null : HotelNightRoutines.PatternPolicy()),
            eventFactory,
            _buffer);
        _interactions = new InteractionActionHandler(_world, eventFactory, _buffer, patterns);
        var probes = new BoundaryProbeActionHandler(_world, eventFactory, _buffer, patterns);
        var movementHandler = new MoveEntityActionHandler(_world, eventFactory, _buffer);
        var access = new PortalAccessPolicy();
        access.SetAccess("basement-door", isAccessible: true);
        LocationGraph graph = CreateLocationGraph();
        var coordinator = new LiveMovementCoordinator(
            _world,
            graph,
            access,
            movementHandler);
        _movement = new CoordinatedNpcMovementExecutor(coordinator, autoCompleteMovements);
        _perception = new PerceptionSystem(
            new LogicalPerceptionResolver(new SequentialObservationIdGenerator(Math.Max(1, firstObservationId))));
        _memories = new MemorySystem(
            _world,
            new SequentialMemoryIdGenerator(Math.Max(1, firstMemoryId)),
            new ExponentialMemoryDecayPolicy(0.0, 0.0));
        _suspicion = new SuspicionSystem(_memories, rules);
        _objects = HotelObjectRegistry.CreateDefaultHotelObjects();
        if (options.Truth is { AnomalySchedule.Count: > 0 } anomalyTruth)
        {
            foreach (AnomalyBeat beat in anomalyTruth.AnomalySchedule.OrderBy(beat => beat.Tick))
            {
                _pendingAnomalies.Enqueue(beat);
            }
        }

        if (options.Truth is not null)
        {
            _duties = new RoleDutySystem(_clock, _world, eventFactory, _buffer, patterns);
            foreach ((EntityId entity, RoleId role) in HotelRoles)
            {
                _duties.Register(entity, role);
            }
        }

        _dialogue = new DialogueSystem(
            _clock,
            _world,
            _memories,
            _suspicion,
            eventFactory,
            _buffer,
            _objects);
        _objectActions = new ObjectActionHandler(
            _world,
            _objects,
            eventFactory,
            _buffer,
            patterns,
            _memories);
        _anomalies = new RealityAnomalySystem(
            _world,
            _clock,
            eventFactory,
            _buffer,
            _memories,
            _suspicion);
        _conspiracy = new ConspiracySystem(
            _world,
            _clock,
            _suspicion,
            _memories,
            eventFactory,
            _buffer);
        _playerController = new PlayerSessionController(
            BasementScenario.George,
            _clock,
            _world,
            graph,
            _movement,
            _interactions,
            probes,
            _dialogue,
            _memories,
            _suspicion,
            _objects,
            _objectActions);

        var playerDirector = new PlayerAiDirector(
            [CreatePlayerAiProfile(_hiddenPlayer, _hiddenPlayerArchetype)],
            _interactions,
            probes);
        _playerRoutine = new NpcRoutineSystem(
            _clock,
            _world,
            _movement,
            new UtilityNpcBrain([new ScheduleGoalSource(), playerDirector]),
            [playerDirector]);
        _playerRoutine.Register(CreateRoutineProfile(
            _hiddenPlayer,
            BasementScenario.Lobby));

        // Without a truth this stays the two hand-written profiles the scripted
        // scenario was pinned against; with one, everybody can react to what they
        // see and has somebody to tell.
        SuspicionBehaviorRepository behaviorProfiles = options.Truth is null
            ? new SuspicionBehaviorRepository([
                new SuspicionBehaviorProfile(
                    BasementScenario.Anna,
                    [BasementScenario.Bob],
                    BasementScenario.Lobby),
                new SuspicionBehaviorProfile(
                    BasementScenario.Bob,
                    [],
                    BasementScenario.Lobby),
            ])
            : new SuspicionBehaviorRepository(HotelRoles.Select(item =>
                new SuspicionBehaviorProfile(
                    item.Entity,
                    HotelSocialGraph.Confidants(item.Role)
                        .SelectMany(confidant => HotelRoles
                            .Where(other => other.Role == confidant)
                            .Select(other => other.Entity))
                        .Where(contact => contact != item.Entity),
                    HotelSocialGraph.SafePlace(item.Role))));
        var behaviorGoals = new SuspicionDrivenGoalSource(
            _suspicion,
            _memories,
            _clock,
            behaviorProfiles,
            options.Truth is null ? null : HotelNightRoutines.BehaviorPolicy());
        var behaviorActions = new SuspicionBehaviorActionSystem(
            _clock,
            _world,
            _memories,
            _suspicion,
            eventFactory,
            _buffer);
        // Secrets are what make an odd-looking character ambiguous. Without them
        // the only person in the hotel behaving strangely is the Player AI, and
        // "strange means Player" becomes a rule that simply works.
        // NeedGoalSource joins the ordinary cast only. Whatever is steering the
        // hidden player is not the sort of thing that gets hungry, and giving it
        // errands would blunt the Player-like behaviour that is meant to give it
        // away.
        var goalSources = new List<INpcGoalSource>
        {
            new ScheduleGoalSource(),
            behaviorGoals,
            new NeedGoalSource(),
        };
        var observers = new List<INpcRoutineDecisionObserver> { behaviorActions };
        if (_options.Truth is { Secrets.Count: > 0 } truth)
        {
            _secretPlans = HotelSecretStaging.Stage(
                truth.Secrets,
                entity => HotelRoles.FirstOrDefault(item => item.Entity == entity).Role);
            goalSources.Add(new SecretGoalSource(_secretPlans));
            observers.Add(new SecretBehaviorSystem(
                _clock,
                _world,
                eventFactory,
                _buffer,
                _secretPlans));
        }

        _behaviorRoutine = new NpcRoutineSystem(
            _clock,
            _world,
            _movement,
            new UtilityNpcBrain(goalSources),
            observers);
        // Whoever the Player AI is steering is deliberately left out: two routine
        // systems issuing movement for the same actor would cancel each other's
        // requests every tick.
        (EntityId Entity, LocationId Home)[] behaviorRoster =
        [
            (BasementScenario.Anna, BasementScenario.Basement),
            (BasementScenario.Bob, BasementScenario.Lobby),
            (BasementScenario.Charlie, BasementScenario.Lobby),
            (BasementScenario.Dana, BasementScenario.Lobby),
            (BasementScenario.Evelyn, BasementScenario.Lobby),
        ];
        foreach ((EntityId entity, LocationId home) in behaviorRoster)
        {
            if (entity == _hiddenPlayer)
            {
                continue;
            }

            _behaviorRoutine.Register(CreateRoutineProfile(entity, home));
        }
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

    public NpcMovementExecution RequestNpcMove(EntityId actor, LocationId destination) =>
        _movement.Execute(new MoveEntityCommand(actor, destination));

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
                 movement.Actor == _hiddenPlayer)
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
                 movement.Actor == _hiddenPlayer)
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

    /// <summary>
    /// Turns the ordinary suspicion pipeline around and asks what the hotel has
    /// on the host. Nothing hidden is consulted; every entry traces back to an
    /// observation some NPC actually made.
    /// </summary>
    /// <summary>
    /// The private business the cast is conducting tonight. Exposed for tests and
    /// tooling only - nothing the player can see may be derived from it, since the
    /// whole point is that a secret and a Player look alike from the outside.
    /// </summary>
    public IReadOnlyList<SecretPlan> SecretPlans => _secretPlans?.Plans ?? [];

    /// <summary>Everything the cast has told the player about their whereabouts.</summary>
    public IReadOnlyList<AlibiClaim> Claims => _dialogue.Claims.Claims;

    /// <summary>
    /// The claims the player currently holds a clue against. First-hand clues sort
    /// first because those are the ones worth staking a confrontation on.
    /// </summary>
    public IReadOnlyList<Contradiction> FindContradictions(EntityId host) =>
        ContradictionFinder.Find(_dialogue.Claims.Claims, GetMemories(host));

    public ExposureReport GetExposure(EntityId host)
    {
        var observers = new List<ObserverExposure>();
        var reasons = new List<ExposureReason>();
        foreach (EntityState entity in _world.Entities.OrderBy(
            entity => entity.Id.Value,
            StringComparer.Ordinal))
        {
            if (entity.Id == host)
            {
                continue;
            }

            SuspicionSnapshot snapshot = GetSuspicion(entity.Id, host);
            if (snapshot.Evidence.Count == 0)
            {
                continue;
            }

            observers.Add(new ObserverExposure(
                entity.Id,
                ExposureReport.WeighVector(snapshot.Vector),
                ExposureReport.WeighPlayerLike(snapshot.Vector),
                snapshot.Vector,
                snapshot.Evidence.Count));
            reasons.AddRange(snapshot.Evidence.Select(evidence => new ExposureReason(
                entity.Id,
                evidence.Contribution.RuleId,
                evidence.Contribution.Dimension,
                evidence.EffectiveStrength)));
        }

        return new ExposureReport(host, observers, reasons);
    }

    public void Interact(EntityId actor, string interactionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(interactionId);
        _ = _interactions.Execute(new InteractionCommand(
            actor,
            InteractionKind.Generic,
            interactionId));
        ProcessPendingEvents();
    }

    public PlayerSessionController PlayerController => _playerController;

    public DialogueSystem Dialogue => _dialogue;

    public DialogueOutcome Talk(DialogueRequest request)
    {
        DialogueOutcome outcome = _dialogue.Execute(request);
        ProcessPendingEvents();
        return outcome;
    }

    public PlayerJournal GetPlayerJournal(EntityId? actor = null)
    {
        if (actor is not null)
        {
            _playerController.SetPlayerEntity(actor.Value);
        }

        return _playerController.GetJournal();
    }

    public HotelObjectRegistry Objects => _objects;

    public ObjectActionResult InspectObject(string objectId)
    {
        ObjectActionResult result = _playerController.InspectObject(objectId);
        ProcessPendingEvents();
        return result;
    }

    public ObjectActionResult TamperObject(string objectId, string? keyId = null)
    {
        ObjectActionResult result = _playerController.TamperObject(objectId, keyId);
        ProcessPendingEvents();
        return result;
    }

    public RealityAnomalySystem Anomalies => _anomalies;

    public WorldEvent TriggerSaveReloadAnomaly(EntityId? player = null)
    {
        EntityId p = player ?? _playerController.PlayerEntity;
        LocationId loc = _world.GetEntity(p).LogicalLocation;
        WorldEvent evt = _anomalies.TriggerSaveReloadAnomaly(p, loc);
        ProcessPendingEvents();
        return evt;
    }

    public WorldEvent TriggerFastTravelAnomaly(EntityId actor, LocationId destination)
    {
        WorldEvent evt = _anomalies.TriggerFastTravelAnomaly(actor, destination);
        ProcessPendingEvents();
        return evt;
    }

    public ConspiracySystem Conspiracy => _conspiracy;

    public AccusationCoalition? ActiveCoalition => _conspiracy.ActiveCoalition;

    public ClimaxResolution? LastClimaxResolution => _conspiracy.LastResolution;

    public AccusationCoalition? EvaluateConspiracy(EntityId? target = null)
    {
        EntityId t = target ?? _playerController.PlayerEntity;
        return _conspiracy.EvaluateAndFormCoalition(t);
    }

    public WorldEvent? TriggerConfrontation(LocationId? location = null)
    {
        WorldEvent? evt = _conspiracy.TriggerConfrontation(location ?? BasementScenario.Lobby);
        if (evt is not null)
        {
            ProcessPendingEvents();
        }

        return evt;
    }

    public ClimaxResolution ResolveClimax(PlayerClimaxChoice choice, EntityId? target = null)
    {
        EntityId t = target ?? _playerController.PlayerEntity;
        return _conspiracy.ResolveClimax(choice, t);
    }

    public bool CanResolveClimax(EntityId? target = null)
    {
        EntityId t = target ?? _playerController.PlayerEntity;
        return _conspiracy.CanResolveClimax(t);
    }

    public SessionSnapshot CaptureSnapshot()
    {
        DecisionSnapshot? annaDecision = _annaInitialDecision is not null
            ? new DecisionSnapshot(
                _annaInitialDecision.Time.Tick,
                _annaInitialDecision.Entity.Value,
                _annaInitialDecision.Goal.Type.ToString(),
                _annaInitialDecision.Goal.Destination.Value,
                _annaInitialDecision.Goal.BaseUtility,
                _annaInitialDecision.Moved,
                _annaInitialDecision.Goal.Target?.Value,
                _annaInitialDecision.Goal.InteractionPartner?.Value,
                _annaInitialDecision.Goal.IntentId)
            : null;

        DecisionSnapshot? bobDecision = _bobInitialDecision is not null
            ? new DecisionSnapshot(
                _bobInitialDecision.Time.Tick,
                _bobInitialDecision.Entity.Value,
                _bobInitialDecision.Goal.Type.ToString(),
                _bobInitialDecision.Goal.Destination.Value,
                _bobInitialDecision.Goal.BaseUtility,
                _bobInitialDecision.Moved,
                _bobInitialDecision.Goal.Target?.Value,
                _bobInitialDecision.Goal.InteractionPartner?.Value,
                _bobInitialDecision.Goal.IntentId)
            : null;

        var firstSuspicionTicks = _firstSuspicion.ToDictionary(kvp => kvp.Key.Value, kvp => kvp.Value.Tick);

        var metadata = new SnapshotMetadata(
            Seed: _options.Seed,
            CurrentTick: _clock.Now.Tick,
            MinimumTicks: _options.Ticks,
            Scenario: "basement",
            Phase: Phase.ToString(),
            ActivePlayerActor: _playerController.PlayerEntity.Value,
            ObservationCount: _observationCount,
            SavedAt: DateTimeOffset.UtcNow,
            AnnaInitialDecision: annaDecision,
            BobInitialDecision: bobDecision,
            FirstSuspicionTicks: firstSuspicionTicks);

        EntityStateSnapshot[] entities = _world.Entities
            .Select(e => new EntityStateSnapshot(e.Id.Value, e.LogicalLocation.Value))
            .ToArray();

        WorldEventSnapshot[] events = _events
            .Select(SessionSnapshotSerializer.ConvertEventToSnapshot)
            .ToArray();

        EntityMemoryStoreSnapshot[] memories = _memories.GetAllStores()
            .Select(store => new EntityMemoryStoreSnapshot(
                store.Owner.Value,
                store.Memories.Select(SessionSnapshotSerializer.ConvertMemoryToSnapshot).ToArray()))
            .ToArray();

        SuspicionCaseSnapshot[] suspicions = _suspicion.GetAllCases()
            .Select(c => new SuspicionCaseSnapshot(
                c.Observer.Value,
                c.Subject.Value,
                c.Contributions.Select(contrib => new EvidenceContributionSnapshot(
                    contrib.SourceMemory.Value,
                    contrib.RuleId,
                    contrib.Dimension.ToString(),
                    contrib.Strength)).ToArray()))
            .ToArray();

        MovementRequestSnapshot[] movements = _movement.PendingMovements
            .Select(m => new MovementRequestSnapshot(
                m.RequestId.Value,
                m.Actor.Value,
                m.Origin.Value,
                m.Destination.Value,
                m.Route?.Select(l => l.Value).ToArray() ?? [],
                m.Status.ToString()))
            .ToArray();

        InteractiveObjectSnapshot[] objects = _objects.AllObjects
            .Select(obj => new InteractiveObjectSnapshot(
                obj.Id,
                obj.IsLocked,
                obj.IsTampered))
            .ToArray();

        AccusationCoalitionSnapshot? coalition = _conspiracy.ActiveCoalition is { } activeCoalition
            ? new AccusationCoalitionSnapshot(
                activeCoalition.Initiator.Value,
                activeCoalition.Target.Value,
                activeCoalition.Members.Select(member => member.Value).ToArray(),
                activeCoalition.EvidenceSummaries.ToArray(),
                activeCoalition.CombinedSuspicionScore,
                activeCoalition.Stage.ToString())
            : null;

        ClimaxResolutionSnapshot? climax = _conspiracy.LastResolution is { } resolution
            ? new ClimaxResolutionSnapshot(
                resolution.Choice.ToString(),
                resolution.Title,
                resolution.NarrativeText,
                resolution.PlayerVindicated,
                resolution.ExistentialAwakeningTriggered,
                resolution.PlayerFled)
            : null;

        return new SessionSnapshot(
            metadata,
            entities,
            events,
            memories,
            suspicions,
            movements,
            objects,
            coalition,
            climax);
    }

    public static BasementScenarioSession FromSnapshot(
        SessionSnapshot snapshot,
        ISuspicionRuleRepository rules,
        bool autoCompleteMovements = false)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(rules);
        SessionSnapshotValidator.Validate(snapshot);

        var options = new BasementScenarioOptions(
            seed: snapshot.Metadata.Seed,
            ticks: snapshot.Metadata.MinimumTicks > 0 ? snapshot.Metadata.MinimumTicks : 16);

        long maxEventId = snapshot.Events.Count > 0 ? snapshot.Events.Max(e => e.Id) : 0;
        long maxMemoryId = snapshot.Memories.SelectMany(m => m.Memories).Any()
            ? snapshot.Memories.SelectMany(m => m.Memories).Max(m => m.Id)
            : 0;

        var session = new BasementScenarioSession(
            rules,
            options,
            autoCompleteMovements,
            initialTick: snapshot.Metadata.CurrentTick,
            firstEventId: maxEventId + 1,
            firstMemoryId: maxMemoryId + 1);

        foreach (EntityStateSnapshot entitySnapshot in snapshot.Entities)
        {
            var entityId = new EntityId(entitySnapshot.EntityId);
            var locId = new LocationId(entitySnapshot.LocationId);
            session._world.RelocateEntity(entityId, locId);
        }

        foreach (EntityMemoryStoreSnapshot storeSnapshot in snapshot.Memories)
        {
            var owner = new EntityId(storeSnapshot.Owner);
            foreach (MemoryRecordSnapshot memorySnapshot in storeSnapshot.Memories)
            {
                MemoryRecord memory = SessionSnapshotSerializer.ConvertSnapshotToMemory(memorySnapshot);
                session._memories.LoadMemory(owner, memory);
            }
        }

        foreach (SuspicionCaseSnapshot caseSnapshot in snapshot.Suspicions)
        {
            var observer = new EntityId(caseSnapshot.Observer);
            var subject = new EntityId(caseSnapshot.Subject);
            var contributions = caseSnapshot.Contributions.Select(c => new EvidenceContribution(
                new MemoryId(c.SourceMemory),
                c.RuleId,
                Enum.Parse<SuspicionDimension>(c.Dimension, ignoreCase: true),
                c.Strength));
            session._suspicion.LoadCase(observer, subject, contributions);
        }

        session._events.Clear();
        foreach (WorldEventSnapshot eventSnapshot in snapshot.Events)
        {
            WorldEvent worldEvent = SessionSnapshotSerializer.ConvertSnapshotToEvent(eventSnapshot);
            session._events.Add(worldEvent);
        }

        session._observationCount = snapshot.Metadata.ObservationCount;
        session.Phase = Enum.Parse<BasementSessionPhase>(snapshot.Metadata.Phase, ignoreCase: true);

        session._restrictedEntry = session._events.FirstOrDefault(e =>
            e.Actor == session._hiddenPlayer &&
            e.Location == BasementScenario.Basement &&
            e.Type == EventType.EnterLocation);

        if (snapshot.Metadata.AnnaInitialDecision is not null)
        {
            var d = snapshot.Metadata.AnnaInitialDecision;
            session._annaInitialDecision = new NpcRoutineDecision(
                new SimTime(d.Tick),
                new EntityId(d.Entity),
                new GoalCandidate(
                    Enum.Parse<GoalType>(d.GoalType, ignoreCase: true),
                    new LocationId(d.Destination),
                    d.BaseUtility,
                    intentId: d.IntentId,
                    target: string.IsNullOrEmpty(d.Target) ? null : new EntityId(d.Target),
                    interactionPartner: string.IsNullOrEmpty(d.InteractionPartner) ? null : new EntityId(d.InteractionPartner)),
                d.Moved);
        }

        if (snapshot.Metadata.BobInitialDecision is not null)
        {
            var d = snapshot.Metadata.BobInitialDecision;
            session._bobInitialDecision = new NpcRoutineDecision(
                new SimTime(d.Tick),
                new EntityId(d.Entity),
                new GoalCandidate(
                    Enum.Parse<GoalType>(d.GoalType, ignoreCase: true),
                    new LocationId(d.Destination),
                    d.BaseUtility,
                    intentId: d.IntentId,
                    target: string.IsNullOrEmpty(d.Target) ? null : new EntityId(d.Target),
                    interactionPartner: string.IsNullOrEmpty(d.InteractionPartner) ? null : new EntityId(d.InteractionPartner)),
                d.Moved);
        }

        if (snapshot.Metadata.FirstSuspicionTicks is not null)
        {
            foreach ((string actor, long tick) in snapshot.Metadata.FirstSuspicionTicks)
            {
                session._firstSuspicion[new EntityId(actor)] = new SimTime(tick);
            }
        }

        if (!string.IsNullOrEmpty(snapshot.Metadata.ActivePlayerActor))
        {
            session._playerController.SetPlayerEntity(new EntityId(snapshot.Metadata.ActivePlayerActor));
        }

        foreach (InteractiveObjectSnapshot objectSnapshot in snapshot.Objects ?? [])
        {
            InteractiveObject obj = session._objects.GetObject(objectSnapshot.Id)
                ?? throw new InvalidDataException($"Snapshot references unknown object '{objectSnapshot.Id}'.");
            obj.RestoreState(objectSnapshot.IsLocked, objectSnapshot.IsTampered);
        }

        session._conspiracy.RestoreState(snapshot.Coalition, snapshot.ClimaxResolution);

        return session;
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
            $"Bob did not retain exactly one social memory of {_hiddenPlayer}'s basement entry.");
        SuspicionSnapshot annaSuspicion = GetSuspicion(
            BasementScenario.Anna,
            _hiddenPlayer);
        SuspicionSnapshot bobSuspicion = GetSuspicion(
            BasementScenario.Bob,
            _hiddenPlayer);
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
            GetLogicalLocation(_hiddenPlayer));
    }

    /// <summary>
    /// Fires the anomalies the seed scheduled for this point in the night.
    /// </summary>
    /// <remarks>
    /// These were generated by CaseGenerator since Milestone 4 and read by nobody,
    /// so RealityAnomalySystem - the subsystem the whole premise is named after -
    /// had never produced a single event in a playable run.
    /// <para>
    /// Whether anyone is standing there to see it is left entirely to chance. An
    /// anomaly nobody witnesses simply did not happen as far as the case is
    /// concerned, which is the point: the strongest evidence in the game is also
    /// the easiest to miss.
    /// </para>
    /// </remarks>
    private void ReleaseDueAnomalies()
    {
        while (_pendingAnomalies.Count > 0 && _pendingAnomalies.Peek().Tick <= _clock.Now.Tick)
        {
            AnomalyBeat beat = _pendingAnomalies.Dequeue();
            LocationId where = GetLogicalLocation(beat.Subject);
            _ = beat.Kind switch
            {
                AnomalyKind.TheBlink => _anomalies.TriggerFastTravelAnomaly(beat.Subject, where),
                AnomalyKind.DialogueReset => _anomalies.TriggerDialogueResetAnomaly(beat.Subject, where),
                _ => _anomalies.TriggerSaveReloadAnomaly(beat.Subject, where),
            };
        }
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
        Phase = _movement.IsBusy(_hiddenPlayer)
            ? BasementSessionPhase.WaitingForExplorer
            : BasementSessionPhase.FeedbackLoop;
    }

    private void RunFeedbackTick()
    {
        ReleaseDueAnomalies();

        // Before the cast decides anything, ask whether anyone is already off
        // their post. The event this publishes is what RoleNeglect counts and what
        // the RoleDeviation suspicion rule scores.
        _ = _duties?.Tick();
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
            if (worldEvent.Actor == _hiddenPlayer &&
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
            _hiddenPlayer);
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
        world.AddLocation(new LocationState(Hallway));
        world.AddLocation(new LocationState(new LocationId("kitchen")));
        world.AddLocation(new LocationState(new LocationId("room-201")));
        world.AddLocation(new LocationState(BasementScenario.Basement, isRestricted: true));
        world.AddLocation(new LocationState(new LocationId("garden")));
        world.AddLocation(new LocationState(new LocationId("security-room"), isRestricted: true));
        world.AddLocation(new LocationState(new LocationId("office")));

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
        graph.AddLocation(new LocationId("kitchen"));
        graph.AddLocation(new LocationId("room-201"));
        graph.AddLocation(BasementScenario.Basement);
        graph.AddLocation(new LocationId("garden"));
        graph.AddLocation(new LocationId("security-room"));
        graph.AddLocation(new LocationId("office"));

        graph.ConnectBidirectional(BasementScenario.Lobby, Hallway, "lobby-arch");
        graph.ConnectBidirectional(BasementScenario.Lobby, new LocationId("garden"), "garden-gate");
        graph.ConnectBidirectional(Hallway, new LocationId("kitchen"), "kitchen-door");
        graph.ConnectBidirectional(Hallway, new LocationId("room-201"), "room-201-door");
        graph.ConnectBidirectional(Hallway, new LocationId("security-room"), "security-door", requiresAccess: true);
        graph.ConnectBidirectional(Hallway, new LocationId("office"), "office-door");
        graph.ConnectBidirectional(
            Hallway,
            BasementScenario.Basement,
            "basement-door",
            requiresAccess: true);
        return graph;
    }

    // PlayerAiProfile validates that the objective set matches the archetype, so
    // each archetype gets the objectives it is defined by: an Explorer probes a
    // boundary, a Completionist exhausts interactions, a Roleplayer does neither
    // and is only visible through how ordinary it tries to look.
    private static PlayerAiProfile CreatePlayerAiProfile(
        EntityId entity,
        PlayerAiArchetype archetype) => archetype switch
        {
            PlayerAiArchetype.Explorer => new PlayerAiProfile(
                entity,
                archetype,
                explorationObjectives: [new ExplorationObjective(
                    "explore-basement",
                    BasementScenario.Basement,
                    "basement-door")]),
            PlayerAiArchetype.Completionist => new PlayerAiProfile(
                entity,
                archetype,
                completionObjectives:
                [
                    new CompletionObjective(
                        "sweep-lobby",
                        BasementScenario.Lobby,
                        InteractionKind.Generic),
                    new CompletionObjective(
                        "sweep-kitchen",
                        new LocationId("kitchen"),
                        InteractionKind.LootContainer),
                ]),
            PlayerAiArchetype.Roleplayer => new PlayerAiProfile(entity, archetype),
            _ => throw new ArgumentOutOfRangeException(
                nameof(archetype),
                archetype,
                "Unknown archetype."),
        };

    /// <summary>
    /// Who does what on this shift. Matches the roles in characters.json, which is
    /// where this belongs once schedules move into content.
    /// </summary>
    private static readonly (EntityId Entity, RoleId Role)[] HotelRoles =
    [
        (BasementScenario.George, HotelNightRoutines.Receptionist),
        (BasementScenario.Anna, HotelNightRoutines.Cleaner),
        (BasementScenario.Bob, HotelNightRoutines.Security),
        (BasementScenario.Charlie, HotelNightRoutines.Guest),
        (BasementScenario.Dana, HotelNightRoutines.Cook),
        (BasementScenario.Evelyn, HotelNightRoutines.Manager),
    ];

    private NpcRoutineProfile CreateRoutineProfile(
        EntityId entity,
        LocationId scheduleLocation)
    {
        // With a truth in play the cast works a real night; without one they keep
        // the flat Idle routine the scripted scenario was pinned against.
        if (_options.Truth is not null)
        {
            RoleId assigned = HotelRoles
                .FirstOrDefault(item => item.Entity == entity).Role;
            if (!string.IsNullOrEmpty(assigned.Value))
            {
                return new NpcRoutineProfile(
                    entity,
                    HotelNightRoutines.Permissions(assigned),
                    HotelNightRoutines.For(assigned),
                    new NeedState(),
                    HotelNeeds.Profile(),
                    HotelNeeds.Destinations(assigned));
            }
        }

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
