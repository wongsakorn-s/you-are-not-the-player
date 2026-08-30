namespace Game.Sim.Time;

public readonly record struct SimMinuteOfDay : IComparable<SimMinuteOfDay>
{
    public const int MinutesPerDay = 1_440;

    public SimMinuteOfDay(int value)
    {
        if (value is < 0 or >= MinutesPerDay)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                $"Minute of day must be between 0 and {MinutesPerDay - 1}.");
        }

        Value = value;
    }

    public int Value { get; }

    public int Hour => Value / 60;

    public int Minute => Value % 60;

    public static SimMinuteOfDay FromHourMinute(int hour, int minute)
    {
        if (hour is < 0 or > 23)
        {
            throw new ArgumentOutOfRangeException(nameof(hour), hour, "Hour must be between 0 and 23.");
        }

        if (minute is < 0 or > 59)
        {
            throw new ArgumentOutOfRangeException(
                nameof(minute),
                minute,
                "Minute must be between 0 and 59.");
        }

        return new SimMinuteOfDay((hour * 60) + minute);
    }

    public int CompareTo(SimMinuteOfDay other) => Value.CompareTo(other.Value);

    public override string ToString() => $"{Hour:00}:{Minute:00}";

    public static bool operator <(SimMinuteOfDay left, SimMinuteOfDay right) => left.Value < right.Value;

    public static bool operator <=(SimMinuteOfDay left, SimMinuteOfDay right) => left.Value <= right.Value;

    public static bool operator >(SimMinuteOfDay left, SimMinuteOfDay right) => left.Value > right.Value;

    public static bool operator >=(SimMinuteOfDay left, SimMinuteOfDay right) => left.Value >= right.Value;
}
