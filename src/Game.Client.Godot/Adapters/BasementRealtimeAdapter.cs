using Game.Sim.Actions;
using Game.Sim.Entities;
using Game.Sim.Events;
using Game.Sim.Locations;
using Game.Sim.Memory;
using Game.Sim.Routines;
using Game.Sim.Scenarios;
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

    public void TogglePause() => IsPaused = !IsPaused;

    public void SetSpeed(float speed)
    {
        if (!float.IsFinite(speed) || speed <= 0.0f)
        {
            throw new ArgumentOutOfRangeException(nameof(speed), speed, "Speed must be positive.");
        }

        Speed = speed;
    }
}
