namespace Game.Sim.Time;

public sealed class SimClock
{
    public const int DefaultTicksPerSecond = 4;

    public SimClock(int ticksPerSecond = DefaultTicksPerSecond)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(ticksPerSecond);
        TicksPerSecond = ticksPerSecond;
    }

    public int TicksPerSecond { get; }

    public SimTime Now { get; private set; } = SimTime.Zero;

    public TimeSpan Elapsed => TimeSpan.FromSeconds((double)Now.Tick / TicksPerSecond);

    public SimTime Advance(SimDelta delta)
    {
        Now += delta;
        return Now;
    }

    public SimTime AdvanceOneTick() => Advance(SimDelta.OneTick);
}
