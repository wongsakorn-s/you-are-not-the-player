using Game.Sim.Actions;

namespace Game.Sim.Routines;

public sealed record NpcMovementExecution(
    NpcMovementExecutionStatus Status,
    MovementSnapshot? Movement = null);
