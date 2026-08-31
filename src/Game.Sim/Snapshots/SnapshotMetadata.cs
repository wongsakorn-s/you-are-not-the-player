namespace Game.Sim.Snapshots;

public sealed record SnapshotMetadata(
    ulong Seed,
    long CurrentTick,
    int MinimumTicks,
    string Scenario,
    string Phase,
    string? ActivePlayerActor,
    int ObservationCount,
    DateTimeOffset SavedAt,
    DecisionSnapshot? AnnaInitialDecision = null,
    DecisionSnapshot? BobInitialDecision = null,
    IReadOnlyDictionary<string, long>? FirstSuspicionTicks = null);
