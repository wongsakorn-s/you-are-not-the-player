namespace Game.Sim.Perception;

public sealed class SequentialObservationIdGenerator : IObservationIdGenerator
{
    private long _nextValue;

    public SequentialObservationIdGenerator(long firstValue = 1)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(firstValue);
        _nextValue = firstValue;
    }

    public ObservationId NextId()
    {
        long value = _nextValue;
        _nextValue = checked(value + 1);
        return new ObservationId(value);
    }
}
