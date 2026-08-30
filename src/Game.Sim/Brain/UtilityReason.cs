namespace Game.Sim.Brain;

public sealed record UtilityReason
{
    public UtilityReason(string code, float weight)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        if (!float.IsFinite(weight))
        {
            throw new ArgumentOutOfRangeException(
                nameof(weight),
                weight,
                "Utility weight must be finite.");
        }

        Code = code.Trim();
        Weight = weight;
    }

    public string Code { get; }

    public float Weight { get; }
}
