using Game.Sim.Actions;
using Game.Sim.Conspiracy;
using Game.Sim.Entities;
using Game.Sim.Events;
using Game.Sim.Locations;
using Game.Sim.Memory;
using Game.Sim.Objects;
using Game.Sim.Player;
using Game.Sim.Routines;
using Game.Sim.Scenarios;
using Game.Sim.Snapshots;
using Game.Sim.Suspicion;

namespace Game.Client.Godot.Adapters;

public sealed class BasementRealtimeAdapter
{
    private const double SecondsPerTick = 0.5;

    private readonly BasementScenarioSession _session;
    private readonly HashSet<MovementRequestId> _dispatchedMovements = [];
    private double _accumulator;

    public BasementRealtimeAdapter(BasementScenarioSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        _session = session;
    }

    public bool IsPaused { get; private set; }

    public float Speed { get; private set; } = 1.0f;

    public long CurrentTick => _session.Now.Tick;

    public int MinimumTicks => _session.MinimumTicks;

    public bool IsComplete => _session.IsComplete;

    public BasementSessionPhase Phase => _session.Phase;

    public ulong Seed => _session.Seed;

    public IReadOnlyList<WorldEvent> Events => _session.Events;

    public IReadOnlyList<NpcRoutineDecision> Decisions => _session.Decisions;

    public bool Update(double delta)
    {
        if (IsPaused || IsComplete)
        {
            return false;
        }

        _accumulator += delta * Speed;
        bool advanced = false;
        while (_accumulator >= SecondsPerTick && !IsComplete)
        {
            _accumulator -= SecondsPerTick;
            _ = _session.AdvanceOneTick();
            advanced = true;
        }

        return advanced;
    }

    public void Step()
    {
        if (!IsComplete)
        {
            _ = _session.AdvanceOneTick();
        }
    }

    public IReadOnlyList<MovementSnapshot> DrainNewMovements()
    {
        MovementSnapshot[] movements = _session.PendingMovements
            .Where(movement => _dispatchedMovements.Add(movement.RequestId))
            .ToArray();
        return movements;
    }

    public MovementSnapshot CompleteMovement(MovementRequestId requestId) =>
        _session.CompleteMovement(requestId);

    public MovementSnapshot FailMovement(MovementRequestId requestId) =>
        _session.FailMovement(requestId);

    public MovementSnapshot? GetPendingMovement(EntityId actor, LocationId destination) =>
        _session.PendingMovements.SingleOrDefault(movement =>
            movement.Actor == actor && movement.Destination == destination);

    public IReadOnlyList<WorldEvent> DrainNewEvents() => _session.DrainNewEvents();

    public LocationId GetLogicalLocation(EntityId actor) =>
        _session.GetLogicalLocation(actor);

    public IReadOnlyList<MemoryRecord> GetMemories(EntityId actor) =>
        _session.GetMemories(actor);

    public SuspicionSnapshot GetSuspicion(EntityId observer, EntityId subject) =>
        _session.GetSuspicion(observer, subject);

    public void Interact(EntityId actor, string interactionId) =>
        _session.Interact(actor, interactionId);

    public PlayerSessionController PlayerController => _session.PlayerController;

    public DialogueOutcome Talk(DialogueRequest request) => _session.Talk(request);

    public DialogueOutcome InquireObject(EntityId partner, string objectId) =>
        _session.PlayerController.InquireObject(partner, objectId);

    public DialogueOutcome ConfrontWithEvidence(EntityId partner, MemoryId evidenceMemoryId) =>
        _session.PlayerController.ConfrontWithEvidence(partner, evidenceMemoryId);

    public PlayerJournal GetPlayerJournal(EntityId? actor = null) => _session.GetPlayerJournal(actor);

    public NpcMovementExecution PlayerMove(LocationId destination) => _session.PlayerController.RequestMove(destination);

    public void TogglePause() => IsPaused = !IsPaused;

    public void SetSpeed(float speed)
    {
        if (!float.IsFinite(speed) || speed <= 0.0f)
        {
            throw new ArgumentOutOfRangeException(nameof(speed), speed, "Speed must be positive.");
        }

        Speed = speed;
    }

    public SessionSnapshot CaptureSnapshot() => _session.CaptureSnapshot();

    public HotelObjectRegistry Objects => _session.Objects;

    public IReadOnlyList<InteractiveObject> GetPresentObjects() => _session.PlayerController.GetPresentObjects();

    public ObjectActionResult InspectObject(string objectId) => _session.InspectObject(objectId);

    public ObjectActionResult TamperObject(string objectId, string? keyId = null) => _session.TamperObject(objectId, keyId);

    public WorldEvent TriggerSaveReloadAnomaly(EntityId? player = null) => _session.TriggerSaveReloadAnomaly(player);

    public WorldEvent TriggerFastTravelAnomaly(EntityId actor, LocationId destination) => _session.TriggerFastTravelAnomaly(actor, destination);

    public AccusationCoalition? EvaluateConspiracy(EntityId? target = null) => _session.EvaluateConspiracy(target);

    public WorldEvent? TriggerConfrontation(LocationId? location = null) => _session.TriggerConfrontation(location);

    public ClimaxResolution ResolveClimax(PlayerClimaxChoice choice, EntityId? target = null) => _session.ResolveClimax(choice, target);

    public static BasementRealtimeAdapter FromSnapshot(
        SessionSnapshot snapshot,
        ISuspicionRuleRepository rules) =>
        new(BasementScenarioSession.FromSnapshot(snapshot, rules));
}
