using Game.Sim.Entities;
using Game.Sim.Memory;

namespace Game.Sim.Suspicion;

public sealed class SuspicionCase
{
    private readonly Dictionary<EvidenceKey, EvidenceContribution> _contributions = [];

    public SuspicionCase(EntityId observer, EntityId subject)
    {
        if (observer.IsEmpty)
        {
            throw new ArgumentException("Observer cannot be empty.", nameof(observer));
        }

        if (subject.IsEmpty)
        {
            throw new ArgumentException("Subject cannot be empty.", nameof(subject));
        }

        if (observer == subject)
        {
            throw new ArgumentException("Observer and subject must be different entities.", nameof(subject));
        }

        Observer = observer;
        Subject = subject;
    }

    public EntityId Observer { get; }

    public EntityId Subject { get; }

    public IReadOnlyList<EvidenceContribution> Contributions => _contributions.Values
        .OrderBy(contribution => contribution.SourceMemory.Value)
        .ThenBy(contribution => contribution.RuleId, StringComparer.Ordinal)
        .ThenBy(contribution => contribution.Dimension)
        .ToArray();

    internal bool Add(EvidenceContribution contribution)
    {
        ArgumentNullException.ThrowIfNull(contribution);
        var key = new EvidenceKey(
            contribution.SourceMemory,
            contribution.RuleId,
            contribution.Dimension);
        return _contributions.TryAdd(key, contribution);
    }

    private readonly record struct EvidenceKey(
        MemoryId SourceMemory,
        string RuleId,
        SuspicionDimension Dimension);
}
