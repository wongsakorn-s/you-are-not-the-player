namespace Game.Sim.Behaviors;

public sealed record SuspicionBehaviorPolicy
{
    public SuspicionBehaviorPolicy(
        float observeThreshold = 5.0f,
        float askThreshold = 10.0f,
        float followThreshold = 15.0f,
        float shareThreshold = 25.0f,
        float avoidCriminalityThreshold = 20.0f)
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
    }

    public float ObserveThreshold { get; }

    public float AskThreshold { get; }

    public float FollowThreshold { get; }

    public float ShareThreshold { get; }

    public float AvoidCriminalityThreshold { get; }

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
