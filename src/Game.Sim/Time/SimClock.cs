namespace Game.Sim.Time;

public sealed class SimClock
{
    public const int DefaultTicksPerSecond = 4;

    /// <param name="startOfDay">
    /// What the wall clock reads at tick zero. A shift that begins at 23:00 needs
    /// its schedules to line up with the time the player is shown.
    /// </param>
    /// <param name="ticksPerMinute">
    /// How many ticks one minute of the day takes. Zero keeps the real-time
    /// reading derived from <paramref name="ticksPerSecond"/>; the hotel passes 1,
    /// because everything the player sees - the deadline, the shift beats, the
    /// times on clues - already treats one tick as one minute.
    /// </param>
    public SimClock(
        int ticksPerSecond = DefaultTicksPerSecond,
        SimMinuteOfDay? startOfDay = null,
        int ticksPerMinute = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(ticksPerSecond);
        ArgumentOutOfRangeException.ThrowIfNegative(ticksPerMinute);
        TicksPerSecond = ticksPerSecond;
        StartOfDay = startOfDay ?? new SimMinuteOfDay(0);
        TicksPerMinute = ticksPerMinute > 0
            ? ticksPerMinute
            : checked(ticksPerSecond * 60);
    }

    public int TicksPerSecond { get; }

    public SimMinuteOfDay StartOfDay { get; }

    public int TicksPerMinute { get; }

    public SimTime Now { get; private set; } = SimTime.Zero;

    public TimeSpan Elapsed => TimeSpan.FromSeconds((double)Now.Tick / TicksPerSecond);

    public long TicksPerDay => checked((long)TicksPerSecond * 86_400L);

    public long DayIndex => Now.Tick / TicksPerDay;

    public SimMinuteOfDay TimeOfDay => new((int)(
        (StartOfDay.Value + (Now.Tick / TicksPerMinute)) % SimMinuteOfDay.MinutesPerDay));

    public SimTime Advance(SimDelta delta)
    {
        Now += delta;
        return Now;
    }

    public SimTime AdvanceOneTick() => Advance(SimDelta.OneTick);
}
