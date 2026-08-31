namespace Game.Sim.Snapshots;

public sealed record MovementRequestSnapshot(
    long RequestId,
    string Actor,
    string Origin,
    string Destination,
    string[] Route,
    string Status);
