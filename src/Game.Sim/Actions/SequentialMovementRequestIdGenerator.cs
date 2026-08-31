namespace Game.Sim.Actions;

public sealed class SequentialMovementRequestIdGenerator : IMovementRequestIdGenerator
{
    private long _nextValue;

    public SequentialMovementRequestIdGenerator(long firstValue = 1)
    {
        if (firstValue <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(firstValue), firstValue, "First ID must be positive.");
        }

        _nextValue = firstValue;
    }

    public MovementRequestId NextId()
    {
        long current = _nextValue;
        _nextValue = checked(_nextValue + 1);
        return new MovementRequestId(current);
    }
}
