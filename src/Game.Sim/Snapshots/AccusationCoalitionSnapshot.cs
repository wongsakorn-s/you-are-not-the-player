namespace Game.Sim.Snapshots;

public sealed record AccusationCoalitionSnapshot(
    string Initiator,
    string Target,
    IReadOnlyList<string> Members,
    IReadOnlyList<string> EvidenceSummaries,
    float CombinedSuspicionScore,
    string Stage);
