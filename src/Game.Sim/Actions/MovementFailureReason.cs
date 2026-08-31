namespace Game.Sim.Actions;

public enum MovementFailureReason
{
    None,
    AccessDenied,
    RouteUnavailable,
    PhysicalPathUnavailable,
    StaleWorldState,
}
