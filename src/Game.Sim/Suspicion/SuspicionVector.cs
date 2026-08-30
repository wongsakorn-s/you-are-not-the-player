namespace Game.Sim.Suspicion;

public sealed record SuspicionVector
{
    public SuspicionVector(
        float criminality,
        float secrecy,
        float roleDeviation,
        float metaBehavior,
        float impossibleBehavior,
        float deception)
    {
        ValidateScore(criminality, nameof(criminality));
        ValidateScore(secrecy, nameof(secrecy));
        ValidateScore(roleDeviation, nameof(roleDeviation));
        ValidateScore(metaBehavior, nameof(metaBehavior));
        ValidateScore(impossibleBehavior, nameof(impossibleBehavior));
        ValidateScore(deception, nameof(deception));
        Criminality = criminality;
        Secrecy = secrecy;
        RoleDeviation = roleDeviation;
        MetaBehavior = metaBehavior;
        ImpossibleBehavior = impossibleBehavior;
        Deception = deception;
    }

    public static SuspicionVector Zero { get; } = new(0, 0, 0, 0, 0, 0);

    public float Criminality { get; }

    public float Secrecy { get; }

    public float RoleDeviation { get; }

    public float MetaBehavior { get; }

    public float ImpossibleBehavior { get; }

    public float Deception { get; }

    public float GetScore(SuspicionDimension dimension) => dimension switch
    {
        SuspicionDimension.Criminality => Criminality,
        SuspicionDimension.Secrecy => Secrecy,
        SuspicionDimension.RoleDeviation => RoleDeviation,
        SuspicionDimension.MetaBehavior => MetaBehavior,
        SuspicionDimension.ImpossibleBehavior => ImpossibleBehavior,
        SuspicionDimension.Deception => Deception,
        _ => throw new ArgumentOutOfRangeException(nameof(dimension), dimension, "Unknown dimension."),
    };

    internal static SuspicionVector FromEvidence(IEnumerable<EvaluatedEvidence> evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        var scores = new float[Enum.GetValues<SuspicionDimension>().Length];

        foreach (EvaluatedEvidence item in evidence)
        {
            scores[(int)item.Contribution.Dimension - 1] += item.EffectiveStrength;
        }

        return new SuspicionVector(
            scores[(int)SuspicionDimension.Criminality - 1],
            scores[(int)SuspicionDimension.Secrecy - 1],
            scores[(int)SuspicionDimension.RoleDeviation - 1],
            scores[(int)SuspicionDimension.MetaBehavior - 1],
            scores[(int)SuspicionDimension.ImpossibleBehavior - 1],
            scores[(int)SuspicionDimension.Deception - 1]);
    }

    private static void ValidateScore(float score, string parameterName)
    {
        if (!float.IsFinite(score) || score < 0.0f)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                score,
                "Suspicion score must be a finite non-negative number.");
        }
    }
}
