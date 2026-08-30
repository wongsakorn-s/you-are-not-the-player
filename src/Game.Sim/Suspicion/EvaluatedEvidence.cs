namespace Game.Sim.Suspicion;

public sealed record EvaluatedEvidence
{
    public EvaluatedEvidence(
        EvidenceContribution contribution,
        float retainedConfidence)
    {
        ArgumentNullException.ThrowIfNull(contribution);

        if (float.IsNaN(retainedConfidence) || retainedConfidence is < 0.0f or > 1.0f)
        {
            throw new ArgumentOutOfRangeException(
                nameof(retainedConfidence),
                retainedConfidence,
                "Retained confidence must be between 0 and 1 inclusive.");
        }

        Contribution = contribution;
        RetainedConfidence = retainedConfidence;
        EffectiveStrength = contribution.Strength * retainedConfidence;
    }

    public EvidenceContribution Contribution { get; }

    public float RetainedConfidence { get; }

    public float EffectiveStrength { get; }
}
