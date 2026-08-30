using Game.Sim.Needs;

namespace Game.Sim.Brain;

public sealed class NeedGoalSource : INpcGoalSource
{
    private const float HungerThreshold = 0.65f;
    private const float FatigueThreshold = 0.75f;
    private const float SocialThreshold = 0.80f;

    public IReadOnlyList<GoalCandidate> Generate(NpcDecisionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var goals = new List<GoalCandidate>(3);
        AddIfUrgent(
            goals,
            context.Profile.Needs.GetUrgency(NeedType.Hunger),
            HungerThreshold,
            GoalType.Eat,
            context.Profile.NeedDestinations.MealLocation,
            "need:hunger",
            baseUtility: 40.0f,
            scale: 60.0f);
        AddIfUrgent(
            goals,
            context.Profile.Needs.GetUrgency(NeedType.Fatigue),
            FatigueThreshold,
            GoalType.Sleep,
            context.Profile.NeedDestinations.RestLocation,
            "need:fatigue",
            baseUtility: 35.0f,
            scale: 70.0f);
        AddIfUrgent(
            goals,
            context.Profile.Needs.GetUrgency(NeedType.Social),
            SocialThreshold,
            GoalType.Socialize,
            context.Profile.NeedDestinations.SocialLocation,
            "need:social",
            baseUtility: 30.0f,
            scale: 70.0f);
        return goals;
    }

    private static void AddIfUrgent(
        List<GoalCandidate> goals,
        float urgency,
        float threshold,
        GoalType type,
        Locations.LocationId destination,
        string reasonCode,
        float baseUtility,
        float scale)
    {
        if (urgency < threshold)
        {
            return;
        }

        goals.Add(new GoalCandidate(
            type,
            destination,
            baseUtility,
            [new UtilityReason(reasonCode, urgency * scale)]));
    }
}
