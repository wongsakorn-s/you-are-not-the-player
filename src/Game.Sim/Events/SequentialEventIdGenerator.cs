namespace Game.Sim.Events;

public sealed class SequentialEventIdGenerator : IEventIdGenerator
{
    private long _nextValue;

    public SequentialEventIdGenerator(long firstValue = 1)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(firstValue);
        _nextValue = firstValue;
    }

    public EventId NextId()
    {
        long value = _nextValue;
        _nextValue = checked(value + 1);
        return new EventId(value);
    }
}
