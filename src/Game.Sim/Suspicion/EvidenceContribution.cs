using Game.Sim.Memory;

namespace Game.Sim.Suspicion;

public sealed record EvidenceContribution
{
    public EvidenceContribution(
        MemoryId sourceMemory,
        string ruleId,
        SuspicionDimension dimension,
        float strength)
    {
        if (sourceMemory.IsEmpty)
        {
            throw new ArgumentException("Source memory cannot be empty.", nameof(sourceMemory));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(ruleId);

        if (!Enum.IsDefined(dimension))
        {
            throw new ArgumentOutOfRangeException(nameof(dimension), dimension, "Unknown suspicion dimension.");
        }

        if (!float.IsFinite(strength) || strength <= 0.0f)
        {
            throw new ArgumentOutOfRangeException(
                nameof(strength),
                strength,
                "Evidence strength must be a finite positive number.");
        }

        SourceMemory = sourceMemory;
        RuleId = ruleId.Trim();
        Dimension = dimension;
        Strength = strength;
    }

    public MemoryId SourceMemory { get; }

    public string RuleId { get; }

    public SuspicionDimension Dimension { get; }

    public float Strength { get; }
}
