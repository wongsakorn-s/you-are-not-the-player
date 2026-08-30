using Game.Sim.Time;

namespace Game.Sim.Schedules;

public sealed class DailySchedule
{
    private readonly ScheduleEntry?[] _entryByMinute = new ScheduleEntry?[SimMinuteOfDay.MinutesPerDay];

    public DailySchedule(IEnumerable<ScheduleEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ScheduleEntry[] materializedEntries = entries
            .OrderBy(entry => entry.Start.Value)
            .ThenBy(entry => entry.Activity)
            .ToArray();

        foreach (ScheduleEntry entry in materializedEntries)
        {
            for (int minute = 0; minute < SimMinuteOfDay.MinutesPerDay; minute++)
            {
                if (!entry.Contains(new SimMinuteOfDay(minute)))
                {
                    continue;
                }

                if (_entryByMinute[minute] is ScheduleEntry existing)
                {
                    throw new ArgumentException(
                        $"Schedule entries '{existing.Activity}' and '{entry.Activity}' overlap at " +
                        $"{new SimMinuteOfDay(minute)}.",
                        nameof(entries));
                }

                _entryByMinute[minute] = entry;
            }
        }

        Entries = Array.AsReadOnly(materializedEntries);
    }

    public IReadOnlyList<ScheduleEntry> Entries { get; }

    public ScheduleEntry? GetEntry(SimMinuteOfDay time) => _entryByMinute[time.Value];
}
