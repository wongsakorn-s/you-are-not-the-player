namespace Game.Sim.Time;

public readonly record struct SimDelta
{
    public static readonly SimDelta Zero = new(0);
    public static readonly SimDelta OneTick = new(1);

    public SimDelta(long ticks)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(ticks);
        Ticks = ticks;
    }

    public long Ticks { get; }
}
