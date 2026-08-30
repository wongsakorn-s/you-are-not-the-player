using Game.Sim.Schedules;

namespace Game.Sim.Brain;

public sealed class ScheduleGoalSource : INpcGoalSource
{
    public IReadOnlyList<GoalCandidate> Generate(NpcDecisionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        ScheduleEntry? entry = context.Profile.Schedule.GetEntry(context.TimeOfDay);
        if (entry is null)
        {
            return [];
        }

        return [new GoalCandidate(
            MapGoal(entry.Activity),
            entry.Location,
            entry.Utility,
            [new UtilityReason($"schedule:{entry.Activity}", 0.0f)])];
    }

    private static GoalType MapGoal(RoutineActivity activity) => activity switch
    {
        RoutineActivity.Idle => GoalType.Idle,
        RoutineActivity.Work => GoalType.Work,
        RoutineActivity.Eat => GoalType.Eat,
        RoutineActivity.Sleep => GoalType.Sleep,
        RoutineActivity.Rest => GoalType.Rest,
        RoutineActivity.Socialize => GoalType.Socialize,
        _ => throw new ArgumentOutOfRangeException(nameof(activity), activity, "Unknown activity."),
    };
}
