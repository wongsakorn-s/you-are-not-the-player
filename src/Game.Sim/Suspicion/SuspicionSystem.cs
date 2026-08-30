using Game.Sim.Entities;
using Game.Sim.Memory;
using Game.Sim.Time;

namespace Game.Sim.Suspicion;

public sealed class SuspicionSystem
{
    private readonly MemorySystem _memories;
    private readonly ISuspicionRuleRepository _rules;
    private readonly Dictionary<CaseKey, SuspicionCase> _cases = [];

    public SuspicionSystem(MemorySystem memories, ISuspicionRuleRepository rules)
    {
        ArgumentNullException.ThrowIfNull(memories);
        ArgumentNullException.ThrowIfNull(rules);
        _memories = memories;
        _rules = rules;
    }

    public int ProcessMemory(EntityId observer, MemoryRecord memory)
    {
        ArgumentNullException.ThrowIfNull(memory);
        MemoryRecord registeredMemory = _memories.GetStore(observer).GetMemory(memory.Id);
        if (!ReferenceEquals(memory, registeredMemory))
        {
            throw new ArgumentException(
                "Memory must be the instance registered for the supplied observer.",
                nameof(memory));
        }

        if (memory.Subject is not EntityId subject || subject == observer)
        {
            return 0;
        }

        SuspicionRule[] matchingRules = _rules.Rules
            .Where(rule => rule.Matches(memory))
            .ToArray();
        if (matchingRules.Length == 0)
        {
            return 0;
        }

        var key = new CaseKey(observer, subject);
        if (!_cases.TryGetValue(key, out SuspicionCase? suspicionCase))
        {
            suspicionCase = new SuspicionCase(observer, subject);
            _cases.Add(key, suspicionCase);
        }

        int added = 0;
        foreach (SuspicionRule rule in matchingRules)
        {
            foreach (SuspicionEffect effect in rule.Effects)
            {
                var contribution = new EvidenceContribution(
                    memory.Id,
                    rule.Id,
                    effect.Dimension,
                    effect.Strength);
                if (suspicionCase.Add(contribution))
                {
                    added++;
                }
            }
        }

        return added;
    }

    public SuspicionSnapshot GetSnapshot(
        EntityId observer,
        EntityId subject,
        SimTime now)
    {
        _ = _memories.GetStore(observer);
        _ = _memories.GetStore(subject);

        if (!_cases.TryGetValue(new CaseKey(observer, subject), out SuspicionCase? suspicionCase))
        {
            return new SuspicionSnapshot(observer, subject, SuspicionVector.Zero, []);
        }

        EvaluatedEvidence[] evaluated = suspicionCase.Contributions
            .Select(contribution => new EvaluatedEvidence(
                contribution,
                _memories.GetRetainedConfidence(observer, contribution.SourceMemory, now)))
            .ToArray();
        return new SuspicionSnapshot(
            observer,
            subject,
            SuspicionVector.FromEvidence(evaluated),
            evaluated);
    }

    private readonly record struct CaseKey(EntityId Observer, EntityId Subject);
}
