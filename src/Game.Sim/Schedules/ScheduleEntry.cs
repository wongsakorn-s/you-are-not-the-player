using Game.Sim.Locations;
using Game.Sim.Time;

namespace Game.Sim.Schedules;

public sealed record ScheduleEntry
{
    public ScheduleEntry(
        SimMinuteOfDay start,
        SimMinuteOfDay end,
        RoutineActivity activity,
        LocationId location,
        float utility = 70.0f)
    {
        if (start == end)
        {
            throw new ArgumentException("A schedule entry must have a non-zero duration.", nameof(end));
        }

        if (!Enum.IsDefined(activity))
        {
            throw new ArgumentOutOfRangeException(nameof(activity), activity, "Unknown routine activity.");
        }

        if (location.IsEmpty)
        {
            throw new ArgumentException("Schedule location cannot be empty.", nameof(location));
        }

        if (!float.IsFinite(utility) || utility < 0.0f)
        {
            throw new ArgumentOutOfRangeException(
                nameof(utility),
                utility,
                "Schedule utility must be a finite non-negative number.");
        }

        Start = start;
        End = end;
        Activity = activity;
        Location = location;
        Utility = utility;
    }

    public SimMinuteOfDay Start { get; }

    public SimMinuteOfDay End { get; }

    public RoutineActivity Activity { get; }

    public LocationId Location { get; }

    public float Utility { get; }

    public bool Contains(SimMinuteOfDay time) => Start < End
        ? time >= Start && time < End
        : time >= Start || time < End;
}
