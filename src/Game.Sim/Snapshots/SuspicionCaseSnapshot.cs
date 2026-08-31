namespace Game.Sim.Snapshots;

public sealed record SuspicionCaseSnapshot(
    string Observer,
    string Subject,
    IReadOnlyList<EvidenceContributionSnapshot> Contributions);
