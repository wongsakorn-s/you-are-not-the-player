using Game.Sim.Actions;
using Game.Sim.Brain;
using Game.Sim.Entities;
using Game.Sim.Time;
using Game.Sim.World;

namespace Game.Sim.Routines;

public sealed class NpcRoutineSystem
{
    private readonly SimClock _clock;
    private readonly WorldState _world;
    private readonly INpcMovementExecutor _movement;
    private readonly UtilityNpcBrain _brain;
    private readonly IReadOnlyList<INpcRoutineDecisionObserver> _observers;
    private readonly Dictionary<EntityId, NpcRoutineProfile> _profiles = [];
    private readonly Dictionary<EntityId, PendingDecision> _pendingDecisions = [];

    public NpcRoutineSystem(
        SimClock clock,
        WorldState world,
        MoveEntityActionHandler movement,
        UtilityNpcBrain brain,
        IEnumerable<INpcRoutineDecisionObserver>? observers = null)
        : this(
            clock,
            world,
            new ImmediateNpcMovementExecutor(movement),
            brain,
            observers)
    {
    }

    public NpcRoutineSystem(
        SimClock clock,
        WorldState world,
        INpcMovementExecutor movement,
        UtilityNpcBrain brain,
        IEnumerable<INpcRoutineDecisionObserver>? observers = null)
    {
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(movement);
        ArgumentNullException.ThrowIfNull(brain);
        _clock = clock;
        _world = world;
        _movement = movement;
        _brain = brain;
        INpcRoutineDecisionObserver[] materializedObservers = observers?.ToArray() ?? [];
        if (materializedObservers.Any(observer => observer is null))
        {
            throw new ArgumentException("Routine observers cannot contain null values.", nameof(observers));
        }

        _observers = Array.AsReadOnly(materializedObservers);
    }

    public bool HasPendingMovement(EntityId actor) => _pendingDecisions.ContainsKey(actor);

    public void Register(NpcRoutineProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        EntityState entity = _world.GetEntity(profile.Entity);
        if (!profile.Role.CanEnter(entity.LogicalLocation))
        {
            throw new InvalidOperationException(
                $"Entity '{entity.Id}' starts in a location forbidden by role '{profile.Role.Role}'.");
        }

        if (!_profiles.TryAdd(profile.Entity, profile))
        {
            throw new InvalidOperationException(
                $"Entity '{profile.Entity}' already has a routine profile.");
        }
    }

    public IReadOnlyList<NpcRoutineDecision> Tick(SimDelta delta)
    {
        _clock.Advance(delta);
        var decisions = new List<NpcRoutineDecision>(_profiles.Count);

        foreach (NpcRoutineProfile profile in _profiles.Values
            .OrderBy(profile => profile.Entity.Value, StringComparer.Ordinal))
        {
            profile.Needs.Advance(delta, _clock.TicksPerSecond, profile.NeedProfile.GrowthRates);
            if (_movement.IsBusy(profile.Entity))
            {
                continue;
            }

            EntityState entity = _world.GetEntity(profile.Entity);
            var context = new NpcDecisionContext(entity, profile, _clock.TimeOfDay);
            GoalCandidate goal = _brain.SelectGoal(context);

            if (!goal.IgnoresRolePermissions && !profile.Role.CanEnter(goal.Destination))
            {
                throw new InvalidOperationException(
                    $"Goal '{goal.Type}' selected forbidden location '{goal.Destination}'.");
            }

            NpcMovementExecution execution = entity.LogicalLocation == goal.Destination
                ? new NpcMovementExecution(NpcMovementExecutionStatus.NoMovement)
                : _movement.Execute(new MoveEntityCommand(entity.Id, goal.Destination));
            bool moved = execution.Status is
                NpcMovementExecutionStatus.Completed or NpcMovementExecutionStatus.Pending;
            bool actionCompleted = execution.Status is
                NpcMovementExecutionStatus.NoMovement or NpcMovementExecutionStatus.Completed;
            if (actionCompleted && entity.LogicalLocation == goal.Destination)
            {
                profile.ApplyRecovery(goal.Type, delta, _clock.TicksPerSecond);
            }

            var decision = new NpcRoutineDecision(_clock.Now, entity.Id, goal, moved);
            if (execution.Status == NpcMovementExecutionStatus.Pending)
            {
                _pendingDecisions.Add(entity.Id, new PendingDecision(decision, delta));
            }
            else if (actionCompleted)
            {
                NotifyObservers(decision);
            }

            decisions.Add(decision);
        }

        return decisions;
    }

    public void AcknowledgeMovementCompleted(EntityId actor)
    {
        if (!_pendingDecisions.Remove(actor, out PendingDecision? pending))
        {
            throw new InvalidOperationException($"Entity '{actor}' has no pending routine movement.");
        }

        EntityState entity = _world.GetEntity(actor);
        if (entity.LogicalLocation != pending.Decision.Goal.Destination)
        {
            throw new InvalidOperationException(
                $"Entity '{actor}' did not arrive at '{pending.Decision.Goal.Destination}'.");
        }

        NpcRoutineProfile profile = _profiles[actor];
        profile.ApplyRecovery(
            pending.Decision.Goal.Type,
            pending.Delta,
            _clock.TicksPerSecond);
        NotifyObservers(pending.Decision with { Time = _clock.Now });
    }

    public void AcknowledgeMovementFailed(EntityId actor)
    {
        if (!_pendingDecisions.Remove(actor))
        {
            throw new InvalidOperationException($"Entity '{actor}' has no pending routine movement.");
        }
    }

    private void NotifyObservers(NpcRoutineDecision decision)
    {
        foreach (INpcRoutineDecisionObserver observer in _observers)
        {
            observer.Observe(decision);
        }
    }

    private sealed record PendingDecision(NpcRoutineDecision Decision, SimDelta Delta);
}
