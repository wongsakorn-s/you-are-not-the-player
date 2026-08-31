using Game.Sim.Actions;
using Game.Sim.Entities;

namespace Game.Sim.Routines;

public interface INpcMovementExecutor
{
    bool IsBusy(EntityId actor);

    NpcMovementExecution Execute(MoveEntityCommand command);
}
