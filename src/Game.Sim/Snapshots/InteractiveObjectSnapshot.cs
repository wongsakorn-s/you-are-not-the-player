namespace Game.Sim.Snapshots;

public sealed record InteractiveObjectSnapshot(
    string Id,
    bool IsLocked,
    bool IsTampered);
