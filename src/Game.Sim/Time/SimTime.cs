namespace Game.Sim.Time;

public readonly record struct SimTime : IComparable<SimTime>
{
    public static readonly SimTime Zero = new(0);

    public SimTime(long tick)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(tick);
        Tick = tick;
    }

    public long Tick { get; }

    public int CompareTo(SimTime other) => Tick.CompareTo(other.Tick);

    public static SimTime operator +(SimTime time, SimDelta delta) =>
        new(checked(time.Tick + delta.Ticks));

    public static bool operator <(SimTime left, SimTime right) => left.Tick < right.Tick;

    public static bool operator <=(SimTime left, SimTime right) => left.Tick <= right.Tick;

    public static bool operator >(SimTime left, SimTime right) => left.Tick > right.Tick;

    public static bool operator >=(SimTime left, SimTime right) => left.Tick >= right.Tick;
}
