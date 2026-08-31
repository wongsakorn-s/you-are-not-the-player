using Game.Sim.Actions;
using Game.Sim.Entities;

namespace Game.Sim.Routines;

public sealed class ImmediateNpcMovementExecutor : INpcMovementExecutor
{
    private readonly MoveEntityActionHandler _movement;

    public ImmediateNpcMovementExecutor(MoveEntityActionHandler movement)
    {
        ArgumentNullException.ThrowIfNull(movement);
        _movement = movement;
    }

    public bool IsBusy(EntityId actor)
    {
        _ = actor;
        return false;
    }

    public NpcMovementExecution Execute(MoveEntityCommand command) =>
        _movement.Execute(command)
            ? new NpcMovementExecution(NpcMovementExecutionStatus.Completed)
            : new NpcMovementExecution(NpcMovementExecutionStatus.NoMovement);
}
