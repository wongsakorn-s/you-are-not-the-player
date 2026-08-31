namespace Game.Sim.Snapshots;

public sealed record WorldEventSnapshot(
    long Id,
    long Tick,
    string Actor,
    string Type,
    string Location,
    string? Target,
    string[] Tags,
    EventPayloadSnapshot Payload);
