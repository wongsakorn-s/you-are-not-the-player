namespace Game.Sim.Suspicion;

public sealed record SuspicionEffect
{
    public SuspicionEffect(SuspicionDimension dimension, float strength)
    {
        if (!Enum.IsDefined(dimension))
        {
            throw new ArgumentOutOfRangeException(nameof(dimension), dimension, "Unknown suspicion dimension.");
        }

        if (!float.IsFinite(strength) || strength <= 0.0f)
        {
            throw new ArgumentOutOfRangeException(
                nameof(strength),
                strength,
                "Suspicion strength must be a finite positive number.");
        }

        Dimension = dimension;
        Strength = strength;
    }

    public SuspicionDimension Dimension { get; }

    public float Strength { get; }
}
