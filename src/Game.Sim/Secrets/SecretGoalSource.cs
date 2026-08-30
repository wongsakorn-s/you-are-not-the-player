using Game.Sim.Brain;

namespace Game.Sim.Secrets;

public sealed class SecretGoalSource : INpcGoalSource
{
    private readonly SecretPlanRepository _plans;

    public SecretGoalSource(SecretPlanRepository plans)
    {
        ArgumentNullException.ThrowIfNull(plans);
        _plans = plans;
    }

    public IReadOnlyList<GoalCandidate> Generate(NpcDecisionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return _plans
            .GetActivePlans(context.Entity.Id, context.TimeOfDay)
            .Select(plan => new GoalCandidate(
                GetGoalType(plan.Behavior),
                plan.Location,
                plan.Utility,
                [new UtilityReason($"secret:{plan.Behavior}", 0.0f)],
                plan.IgnoresRolePermissions,
                plan.Id))
            .ToArray();
    }

    internal static GoalType GetGoalType(SecretBehaviorKind behavior) => behavior switch
    {
        SecretBehaviorKind.Theft => GoalType.Steal,
        SecretBehaviorKind.SecretMeeting => GoalType.MeetSecretly,
        SecretBehaviorKind.NightOwl => GoalType.WanderAtNight,
        _ => throw new ArgumentOutOfRangeException(nameof(behavior), behavior, "Unknown secret behavior."),
    };
}
