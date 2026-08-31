namespace Game.Sim.Actions;

public enum MovementStatus
{
    Requested,
    Navigating,
    Completed,
    Failed,
    Cancelled,
}
