namespace Game.Sim.Perception;

public readonly record struct ObservationId
{
    public ObservationId(long value)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
        Value = value;
    }

    public long Value { get; }

    public bool IsEmpty => Value <= 0;

    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
