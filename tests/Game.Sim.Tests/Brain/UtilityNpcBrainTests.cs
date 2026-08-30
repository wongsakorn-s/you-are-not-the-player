using Game.Sim.Brain;
using Game.Sim.Entities;
using Game.Sim.Locations;
using Game.Sim.Needs;
using Game.Sim.Roles;
using Game.Sim.Routines;
using Game.Sim.Schedules;
using Game.Sim.Time;

namespace Game.Sim.Tests.Brain;

public sealed class UtilityNpcBrainTests
{
    private static readonly EntityId Npc = new("npc");
    private static readonly LocationId Office = new("office");
    private static readonly LocationId Kitchen = new("kitchen");
    private static readonly LocationId Bedroom = new("bedroom");
    private static readonly LocationId Lounge = new("lounge");

    [Fact]
    public void SelectGoal_UrgentNeedOverridesSchedule()
    {
        NpcRoutineProfile profile = CreateProfile(new NeedState(hunger: 0.9f));
        var context = new NpcDecisionContext(
            new EntityState(Npc, Office),
            profile,
            SimMinuteOfDay.FromHourMinute(9, 0));
        var brain = new UtilityNpcBrain([new ScheduleGoalSource(), new NeedGoalSource()]);

        GoalCandidate selected = brain.SelectGoal(context);

        Assert.Equal(GoalType.Eat, selected.Type);
        Assert.Equal(Kitchen, selected.Destination);
        Assert.Contains(selected.Reasons, reason => reason.Code == "need:hunger");
    }

    [Fact]
    public void SelectGoal_UsesStableTieBreaking()
    {
        NpcRoutineProfile profile = CreateProfile(new NeedState());
        var context = new NpcDecisionContext(
            new EntityState(Npc, Office),
            profile,
            SimMinuteOfDay.FromHourMinute(9, 0));
        var brain = new UtilityNpcBrain([
            new FixedGoalSource([
                new GoalCandidate(GoalType.Rest, Lounge, 10.0f),
                new GoalCandidate(GoalType.Eat, Kitchen, 10.0f),
            ]),
        ]);

        GoalCandidate selected = brain.SelectGoal(context);

        Assert.Equal(GoalType.Eat, selected.Type);
    }

    private static NpcRoutineProfile CreateProfile(NeedState needs)
    {
        var schedule = new DailySchedule([
            new ScheduleEntry(
                SimMinuteOfDay.FromHourMinute(8, 0),
                SimMinuteOfDay.FromHourMinute(17, 0),
                RoutineActivity.Work,
                Office),
        ]);
        var role = new RolePermissions(
            new RoleId("staff"),
            [Office, Kitchen, Bedroom, Lounge]);
        return new NpcRoutineProfile(
            Npc,
            role,
            schedule,
            needs,
            new NeedProfile(new NeedRates(0.1, 0.1, 0.1), 1.0, 1.0, 1.0),
            new NeedDestinations(Kitchen, Bedroom, Lounge));
    }

    private sealed class FixedGoalSource(IReadOnlyList<GoalCandidate> goals) : INpcGoalSource
    {
        public IReadOnlyList<GoalCandidate> Generate(NpcDecisionContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            return goals;
        }
    }
}
