using System.Globalization;
using Game.Sim.Actions;
using Game.Sim.Brain;
using Game.Sim.Entities;
using Game.Sim.Events;
using Game.Sim.Locations;
using Game.Sim.Needs;
using Game.Sim.Roles;
using Game.Sim.Routines;
using Game.Sim.Schedules;
using Game.Sim.Time;
using Game.Sim.World;

namespace Game.Sim.Tests.Routines;

public sealed class NpcRoutineSystemTests
{
    private static readonly LocationId Office = new("office");
    private static readonly LocationId Kitchen = new("kitchen");
    private static readonly LocationId Lounge = new("lounge");

    [Fact]
    public void Tick_RunsSixNpcsForFullDayDeterministically()
    {
        ScenarioResult first = RunFullDayScenario();
        ScenarioResult second = RunFullDayScenario();

        Assert.Equal(8_640, first.Decisions.Count);
        Assert.Equal(first.Decisions, second.Decisions);
        Assert.Equal(first.Events, second.Events);
        Assert.Equal(1, first.DayIndex);
        Assert.Equal(SimMinuteOfDay.FromHourMinute(0, 0), first.TimeOfDay);
        Assert.All(first.FinalLocations, location => Assert.False(location.IsEmpty));
        Assert.NotEmpty(first.Events);
    }

    private static ScenarioResult RunFullDayScenario()
    {
        var world = new WorldState();
        world.AddLocation(new LocationState(Office));
        world.AddLocation(new LocationState(Kitchen));
        world.AddLocation(new LocationState(Lounge));

        var bedrooms = new List<LocationId>(6);
        for (int index = 1; index <= 6; index++)
        {
            var bedroom = new LocationId($"bedroom-{index.ToString(CultureInfo.InvariantCulture)}");
            bedrooms.Add(bedroom);
            world.AddLocation(new LocationState(bedroom));
        }

        var clock = new SimClock(ticksPerSecond: 1);
        var buffer = new WorldEventBuffer();
        var eventFactory = new WorldEventFactory(clock, new SequentialEventIdGenerator());
        var movement = new MoveEntityActionHandler(world, eventFactory, buffer);
        var brain = new UtilityNpcBrain([new ScheduleGoalSource(), new NeedGoalSource()]);
        var routine = new NpcRoutineSystem(clock, world, movement, brain);

        for (int index = 0; index < bedrooms.Count; index++)
        {
            var entityId = new EntityId($"npc-{(index + 1).ToString(CultureInfo.InvariantCulture)}");
            world.AddEntity(new EntityState(entityId, bedrooms[index]));
            routine.Register(CreateProfile(entityId, bedrooms[index]));
        }

        var decisionFingerprint = new List<string>(SimMinuteOfDay.MinutesPerDay * 6);
        for (int minute = 0; minute < SimMinuteOfDay.MinutesPerDay; minute++)
        {
            IReadOnlyList<NpcRoutineDecision> decisions = routine.Tick(new SimDelta(60));
            decisionFingerprint.AddRange(decisions.Select(FormatDecision));
        }

        string[] eventFingerprint = buffer.Drain().Select(FormatEvent).ToArray();
        LocationId[] finalLocations = world.Entities.Select(entity => entity.LogicalLocation).ToArray();
        return new ScenarioResult(
            decisionFingerprint,
            eventFingerprint,
            finalLocations,
            clock.DayIndex,
            clock.TimeOfDay);
    }

    private static NpcRoutineProfile CreateProfile(EntityId entity, LocationId bedroom)
    {
        var role = new RolePermissions(
            new RoleId("resident"),
            [Office, Kitchen, Lounge, bedroom]);
        var schedule = new DailySchedule([
            Entry(0, 0, 7, 0, RoutineActivity.Sleep, bedroom, 80.0f),
            Entry(7, 0, 12, 0, RoutineActivity.Work, Office),
            Entry(12, 0, 13, 0, RoutineActivity.Eat, Kitchen, 75.0f),
            Entry(13, 0, 18, 0, RoutineActivity.Work, Office),
            Entry(18, 0, 20, 0, RoutineActivity.Socialize, Lounge, 75.0f),
            Entry(20, 0, 22, 0, RoutineActivity.Rest, bedroom),
            Entry(22, 0, 0, 0, RoutineActivity.Sleep, bedroom, 80.0f),
        ]);
        var needProfile = new NeedProfile(
            new NeedRates(hungerPerHour: 0.08, fatiguePerHour: 0.05, socialPerHour: 0.04),
            eatingRecoveryPerHour: 1.0,
            sleepingRecoveryPerHour: 0.7,
            socialRecoveryPerHour: 0.6);
        return new NpcRoutineProfile(
            entity,
            role,
            schedule,
            new NeedState(hunger: 0.15f, fatigue: 0.2f, social: 0.1f),
            needProfile,
            new NeedDestinations(Kitchen, bedroom, Lounge));
    }

    private static ScheduleEntry Entry(
        int startHour,
        int startMinute,
        int endHour,
        int endMinute,
        RoutineActivity activity,
        LocationId location,
        float utility = 70.0f) =>
        new(
            SimMinuteOfDay.FromHourMinute(startHour, startMinute),
            SimMinuteOfDay.FromHourMinute(endHour, endMinute),
            activity,
            location,
            utility);

    private static string FormatDecision(NpcRoutineDecision decision) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{decision.Time.Tick}|{decision.Entity.Value}|{decision.Goal.Type}|{decision.Goal.Destination.Value}|{decision.Goal.TotalUtility:R}|{decision.Moved}");

    private static string FormatEvent(WorldEvent worldEvent) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{worldEvent.Id.Value}|{worldEvent.Time.Tick}|{worldEvent.Actor.Value}|{worldEvent.Type}|{worldEvent.Location.Value}");

    private sealed record ScenarioResult(
        IReadOnlyList<string> Decisions,
        IReadOnlyList<string> Events,
        IReadOnlyList<LocationId> FinalLocations,
        long DayIndex,
        SimMinuteOfDay TimeOfDay);
}
