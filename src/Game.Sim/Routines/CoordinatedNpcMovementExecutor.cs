using Game.Sim.Actions;
using Game.Sim.Entities;

namespace Game.Sim.Routines;

public sealed class CoordinatedNpcMovementExecutor : INpcMovementExecutor
{
    private readonly LiveMovementCoordinator _coordinator;
    private readonly bool _autoComplete;
    private readonly Dictionary<EntityId, MovementRequestId> _activeRequests = [];

    public CoordinatedNpcMovementExecutor(
        LiveMovementCoordinator coordinator,
        bool autoComplete = false)
    {
        ArgumentNullException.ThrowIfNull(coordinator);
        _coordinator = coordinator;
        _autoComplete = autoComplete;
    }

    public IReadOnlyList<MovementSnapshot> PendingMovements => _activeRequests.Values
        .Select(_coordinator.Get)
        .OrderBy(movement => movement.RequestId.Value)
        .ToArray();

    public bool IsBusy(EntityId actor) => _activeRequests.ContainsKey(actor);

    public NpcMovementExecution Execute(MoveEntityCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        MovementSnapshot movement = _coordinator.Request(command);
        if (movement.Status == MovementStatus.Failed)
        {
            return new NpcMovementExecution(NpcMovementExecutionStatus.Failed, movement);
        }

        if (movement.Status == MovementStatus.Completed)
        {
            return new NpcMovementExecution(NpcMovementExecutionStatus.NoMovement, movement);
        }

        movement = _coordinator.AcknowledgeNavigationStarted(movement.RequestId);
        if (movement.Status == MovementStatus.Failed)
        {
            return new NpcMovementExecution(NpcMovementExecutionStatus.Failed, movement);
        }

        if (_autoComplete)
        {
            movement = _coordinator.Complete(movement.RequestId);
            return new NpcMovementExecution(NpcMovementExecutionStatus.Completed, movement);
        }

        _activeRequests[command.Actor] = movement.RequestId;
        return new NpcMovementExecution(NpcMovementExecutionStatus.Pending, movement);
    }

    public MovementSnapshot Complete(MovementRequestId requestId)
    {
        MovementSnapshot active = RequireActive(requestId);
        MovementSnapshot completed = _coordinator.Complete(requestId);
        _activeRequests.Remove(active.Actor);
        return completed;
    }

    public MovementSnapshot Fail(
        MovementRequestId requestId,
        MovementFailureReason failureReason)
    {
        MovementSnapshot active = RequireActive(requestId);
        MovementSnapshot failed = _coordinator.Fail(requestId, failureReason);
        _activeRequests.Remove(active.Actor);
        return failed;
    }

    public MovementSnapshot? GetPending(EntityId actor) =>
        _activeRequests.TryGetValue(actor, out MovementRequestId requestId)
            ? _coordinator.Get(requestId)
            : null;

    private MovementSnapshot RequireActive(MovementRequestId requestId)
    {
        MovementSnapshot movement = _coordinator.Get(requestId);
        if (!_activeRequests.TryGetValue(movement.Actor, out MovementRequestId activeId) ||
            activeId != requestId)
        {
            throw new InvalidOperationException(
                $"Movement request '{requestId}' is not active in the NPC executor.");
        }

        return movement;
    }
}
