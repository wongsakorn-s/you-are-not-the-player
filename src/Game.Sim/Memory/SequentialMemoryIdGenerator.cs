namespace Game.Sim.Memory;

public sealed class SequentialMemoryIdGenerator : IMemoryIdGenerator
{
    private long _nextValue;

    public SequentialMemoryIdGenerator(long firstValue = 1)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(firstValue);
        _nextValue = firstValue;
    }

    public MemoryId NextId()
    {
        long value = _nextValue;
        _nextValue = checked(value + 1);
        return new MemoryId(value);
    }
}
