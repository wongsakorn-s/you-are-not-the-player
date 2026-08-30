namespace Game.Sim.Memory;

public readonly record struct MemoryId
{
    public MemoryId(long value)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
        Value = value;
    }

    public long Value { get; }

    public bool IsEmpty => Value <= 0;

    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
