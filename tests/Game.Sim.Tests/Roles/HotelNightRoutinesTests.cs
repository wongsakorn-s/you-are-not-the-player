using Game.Sim.Cases;
using Game.Sim.Entities;
using Game.Sim.Events;
using Game.Sim.Locations;
using Game.Sim.PlayerAi;
using Game.Sim.Roles;
using Game.Sim.Scenarios;
using Game.Sim.Schedules;
using Game.Sim.Suspicion;
using Game.Sim.Time;

namespace Game.Sim.Tests.Roles;

/// <summary>
/// The first design pillar: you cannot notice that somebody is out of place
/// unless there is a place they are supposed to be.
/// </summary>
public sealed class HotelNightRoutinesTests
{
    private static readonly RoleId[] AllRoles =
    [
        HotelNightRoutines.Receptionist,
        HotelNightRoutines.Cleaner,
        HotelNightRoutines.Security,
        HotelNightRoutines.Cook,
        HotelNightRoutines.Manager,
        HotelNightRoutines.Guest,
    ];

    [Fact]
    public void EveryRoleIsAllowedEverywhereItsOwnScheduleSendsIt()
    {
        // NpcRoutineProfile enforces this at construction, so a mismatch is a
        // crash at session start rather than a bad night.
        foreach (RoleId role in AllRoles)
        {
            RolePermissions permissions = HotelNightRoutines.Permissions(role);
            foreach (ScheduleEntry entry in HotelNightRoutines.For(role).Entries)
            {
                Assert.True(
                    permissions.CanEnter(entry.Location),
                    $"{role} is scheduled into {entry.Location} but not allowed there.");
            }
        }
    }

    [Fact]
    public void EveryMinuteOfTheDayHasSomethingToDo()
    {
        foreach (RoleId role in AllRoles)
        {
            DailySchedule schedule = HotelNightRoutines.For(role);
            for (int minute = 0; minute < SimMinuteOfDay.MinutesPerDay; minute += 15)
            {
                Assert.True(
                    schedule.GetEntry(new SimMinuteOfDay(minute)) is not null,
                    $"{role} has no entry at minute {minute}.");
            }
        }
    }

    [Fact]
    public void TheNightIsNotOneLongBlockForAnybodyWithAJob()
    {
        // A post you never leave is as unreadable as no post at all: the player
        // learns "normal" from seeing people move between places on a rhythm.
        foreach (RoleId role in AllRoles.Where(r => r != HotelNightRoutines.Guest))
        {
            LocationId[] duringShift = Enumerable.Range(0, 360)
                .Select(minute => HotelNightRoutines.DutyLocation(
                    role,
                    new SimMinuteOfDay((23 * 60 + minute) % SimMinuteOfDay.MinutesPerDay)))
                .Where(location => location is not null)
                .Select(location => location!.Value)
                .Distinct()
                .ToArray();

            Assert.True(
                duringShift.Length >= 2,
                $"{role} stands in one place all night ({string.Join(",", duringShift)}).");
        }
    }

    [Fact]
    public void AGuestHasNoDutyToNeglect()
    {
        for (int minute = 0; minute < SimMinuteOfDay.MinutesPerDay; minute += 15)
        {
            Assert.Null(HotelNightRoutines.DutyLocation(
                HotelNightRoutines.Guest,
                new SimMinuteOfDay(minute)));
        }
    }

    [Fact]
    public void OnlySecurityBelongsInTheCameraRoom()
    {
        var cameraRoom = new LocationId("security-room");

        Assert.True(HotelNightRoutines.Permissions(HotelNightRoutines.Security).CanEnter(cameraRoom));
        Assert.All(
            AllRoles.Where(role => role != HotelNightRoutines.Security),
            role => Assert.False(HotelNightRoutines.Permissions(role).CanEnter(cameraRoom)));
    }

    [Fact]
    public void NobodyIsSupposedToBeInTheBasement()
    {
        // The whole case turns on a door nobody had a reason to open.
        var basement = new LocationId("basement");

        Assert.All(
            AllRoles,
            role => Assert.False(HotelNightRoutines.Permissions(role).CanEnter(basement)));
    }

    [Fact]
    public void LeavingYourPostIsReportedAndCountsAgainstYou()
    {
        BasementScenarioSession session = CreateSession();

        // The receptionist is due in the lobby all night. Walk him out of it and
        // leave him away past the grace period.
        _ = session.PlayerController.RequestMove(new LocationId("garden"));
        Advance(session, (int)RoleDutySystem.GraceTicks + 6);

        Assert.Contains(
            session.Events,
            worldEvent => worldEvent.Type == EventType.RoleDutyMissed &&
                worldEvent.Actor == BasementScenario.George);
    }

    [Fact]
    public void StayingWhereYouBelongIsNeverReported()
    {
        BasementScenarioSession session = CreateSession();
        Advance(session, (int)RoleDutySystem.GraceTicks + 20);

        Assert.DoesNotContain(
            session.Events,
            worldEvent => worldEvent.Type == EventType.RoleDutyMissed &&
                worldEvent.Actor == BasementScenario.George);
    }

    [Fact]
    public void ACastWithSomewhereToBeActuallyGoesThere()
    {
        // The measurement that started this: a night used to produce a handful of
        // events and 60% Idle decisions, which left the case file with nothing in
        // it worth comparing.
        BasementScenarioSession session = CreateSession();
        Advance(session, 120);

        int movements = session.Events.Count(worldEvent =>
            worldEvent.Type is EventType.EnterLocation or EventType.LeaveLocation);
        int movers = session.Events
            .Where(worldEvent => worldEvent.Type == EventType.EnterLocation)
            .Select(worldEvent => worldEvent.Actor)
            .Distinct()
            .Count();

        Assert.True(movements >= 12, $"Only {movements} movements in two hours.");
        Assert.True(movers >= 3, $"Only {movers} characters went anywhere.");
    }

    private static void Advance(BasementScenarioSession session, int ticks)
    {
        for (int index = 0; index < ticks && !session.IsComplete; index++)
        {
            _ = session.AdvanceOneTick();
        }
    }

    private static BasementScenarioSession CreateSession()
    {
        InMemorySuspicionRuleRepository rules = JsonSuspicionRuleParser.Parse(
            File.ReadAllText(Path.Combine(
                AppContext.BaseDirectory,
                "Data",
                "SuspicionRules",
                "mvp.json")));
        var truth = new SessionTruth(
            seed: 481_516,
            humanHost: BasementScenario.George,
            hiddenPlayer: BasementScenario.Charlie,
            hiddenPlayerArchetype: PlayerAiArchetype.Explorer,
            incidentCulprit: BasementScenario.George);
        return new BasementScenario(rules).CreateSession(
            new BasementScenarioOptions(481_516, 360, truth),
            autoCompleteMovements: true);
    }
}
