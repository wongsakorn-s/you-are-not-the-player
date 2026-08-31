namespace Game.Sim.Snapshots;

public sealed record EvidenceContributionSnapshot(
    long SourceMemory,
    string RuleId,
    string Dimension,
    float Strength);
