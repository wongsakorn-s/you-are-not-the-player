namespace Game.Sim.Suspicion;

public sealed class InMemorySuspicionRuleRepository : ISuspicionRuleRepository
{
    public InMemorySuspicionRuleRepository(IEnumerable<SuspicionRule> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        SuspicionRule[] materializedRules = rules
            .OrderBy(rule => rule.Id, StringComparer.Ordinal)
            .ToArray();

        string? duplicateId = materializedRules
            .GroupBy(rule => rule.Id, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1)?
            .Key;
        if (duplicateId is not null)
        {
            throw new ArgumentException(
                $"Suspicion rule ID '{duplicateId}' is duplicated.",
                nameof(rules));
        }

        Rules = Array.AsReadOnly(materializedRules);
    }

    public IReadOnlyList<SuspicionRule> Rules { get; }
}
