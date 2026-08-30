using Game.Sim.Locations;
using Game.Sim.Schedules;
using Game.Sim.Time;

namespace Game.Sim.Tests.Schedules;

public sealed class DailyScheduleTests
{
    private static readonly LocationId Bedroom = new("bedroom");
    private static readonly LocationId Office = new("office");

    [Fact]
    public void GetEntry_ResolvesEntriesThatCrossMidnight()
    {
        var sleep = new ScheduleEntry(
            At(22, 0),
            At(6, 0),
            RoutineActivity.Sleep,
            Bedroom);
        var schedule = new DailySchedule([sleep]);

        Assert.Same(sleep, schedule.GetEntry(At(23, 30)));
        Assert.Same(sleep, schedule.GetEntry(At(5, 59)));
        Assert.Null(schedule.GetEntry(At(6, 0)));
    }

    [Fact]
    public void Constructor_RejectsOverlappingEntries()
    {
        var work = new ScheduleEntry(At(8, 0), At(12, 0), RoutineActivity.Work, Office);
        var lunch = new ScheduleEntry(At(11, 30), At(13, 0), RoutineActivity.Eat, Office);

        Assert.Throws<ArgumentException>(() => new DailySchedule([work, lunch]));
    }

    private static SimMinuteOfDay At(int hour, int minute) =>
        SimMinuteOfDay.FromHourMinute(hour, minute);
}
