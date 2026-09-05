namespace Game.Sim.Behaviors;

public sealed record SuspicionBehaviorPolicy
{
    public SuspicionBehaviorPolicy(
        float observeThreshold = 5.0f,
        float askThreshold = 10.0f,
        float followThreshold = 15.0f,
        float shareThreshold = 25.0f,
        float avoidCriminalityThreshold = 20.0f,
        float maxBeliefWeight = float.MaxValue)
    {
        ValidateThreshold(observeThreshold, nameof(observeThreshold));
        ValidateThreshold(askThreshold, nameof(askThreshold));
        ValidateThreshold(followThreshold, nameof(followThreshold));
        ValidateThreshold(shareThreshold, nameof(shareThreshold));
        ValidateThreshold(avoidCriminalityThreshold, nameof(avoidCriminalityThreshold));
        ObserveThreshold = observeThreshold;
        AskThreshold = askThreshold;
        FollowThreshold = followThreshold;
        ShareThreshold = shareThreshold;
        AvoidCriminalityThreshold = avoidCriminalityThreshold;
        MaxBeliefWeight = maxBeliefWeight;
    }

    public float ObserveThreshold { get; }

    public float AskThreshold { get; }

    public float FollowThreshold { get; }

    public float ShareThreshold { get; }

    public float AvoidCriminalityThreshold { get; }

    /// <summary>
    /// How much a suspicion is allowed to add to a goal's utility.
    /// </summary>
    /// <remarks>
    /// The raw concern score feeds straight into the utility, and it is unbounded.
    /// Once witnessed anomalies started scoring in the hundreds, following the
    /// person you suspect outranked every shift, every need and every secret in
    /// the building by a factor of five, and the whole cast spent the night
    /// trailing one another. Suspicion is supposed to weigh on a decision, not
    /// replace it.
    /// </remarks>
    public float MaxBeliefWeight { get; }

    private static void ValidateThreshold(float value, string parameterName)
    {
        if (!float.IsFinite(value) || value < 0.0f)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                "Suspicion behavior threshold must be a finite non-negative number.");
        }
    }
}
