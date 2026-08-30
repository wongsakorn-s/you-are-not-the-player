namespace Game.Sim.Brain;

public sealed class UtilityNpcBrain
{
    private readonly IReadOnlyList<INpcGoalSource> _goalSources;

    public UtilityNpcBrain(IEnumerable<INpcGoalSource> goalSources)
    {
        ArgumentNullException.ThrowIfNull(goalSources);
        INpcGoalSource[] materializedSources = goalSources.ToArray();
        if (materializedSources.Length == 0)
        {
            throw new ArgumentException("NPC brain requires at least one goal source.", nameof(goalSources));
        }

        _goalSources = Array.AsReadOnly(materializedSources);
    }

    public GoalCandidate SelectGoal(NpcDecisionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        GoalCandidate? selected = _goalSources
            .SelectMany(source => source.Generate(context))
            .Where(candidate =>
                candidate.IgnoresRolePermissions ||
                context.Profile.Role.CanEnter(candidate.Destination))
            .OrderByDescending(candidate => candidate.TotalUtility)
            .ThenBy(candidate => candidate.Type)
            .ThenBy(candidate => candidate.Destination.Value, StringComparer.Ordinal)
            .ThenBy(candidate => candidate.IntentId, StringComparer.Ordinal)
            .FirstOrDefault();

        return selected ?? new GoalCandidate(
            GoalType.Idle,
            context.Entity.LogicalLocation,
            baseUtility: 0.0f,
            [new UtilityReason("fallback:idle", 0.0f)]);
    }
}
