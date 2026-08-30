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

    public long TicksPerDay => checked((long)TicksPerSecond * 86_400L);

    public long DayIndex => Now.Tick / TicksPerDay;

    public SimMinuteOfDay TimeOfDay => new(
        (int)((Now.Tick % TicksPerDay) / (TicksPerSecond * 60L)));

    public SimTime Advance(SimDelta delta)
    {
        Now += delta;
        return Now;
    }

    public SimTime AdvanceOneTick() => Advance(SimDelta.OneTick);
}
