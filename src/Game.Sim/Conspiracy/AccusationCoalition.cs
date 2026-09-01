using Game.Sim.Entities;
using Game.Sim.Suspicion;

namespace Game.Sim.Conspiracy;

public sealed class AccusationCoalition
{
    private readonly HashSet<EntityId> _members = [];
    private readonly List<string> _evidenceSummaries = [];

    public AccusationCoalition(EntityId initiator, EntityId target)
    {
        if (initiator.IsEmpty)
        {
            throw new ArgumentException("Initiator cannot be empty.", nameof(initiator));
        }

        if (target.IsEmpty)
        {
            throw new ArgumentException("Target cannot be empty.", nameof(target));
        }

        Initiator = initiator;
        Target = target;
        _members.Add(initiator);
        Stage = CoalitionStage.Forming;
    }

    public EntityId Initiator { get; }

    public EntityId Target { get; }

    public IReadOnlySet<EntityId> Members => _members;

    public IReadOnlyList<string> EvidenceSummaries => _evidenceSummaries;

    public CoalitionStage Stage { get; internal set; }

    public float CombinedSuspicionScore { get; internal set; }

    public bool ConsensusReached => _members.Count >= 2 && CombinedSuspicionScore >= 40.0f;

    public void AddMember(EntityId member, float suspicionScore, IEnumerable<string>? evidence = null)
    {
        if (member.IsEmpty || member == Target)
        {
            return;
        }

        _ = _members.Add(member);
        CombinedSuspicionScore += suspicionScore;

        if (evidence is not null)
        {
            foreach (string item in evidence)
            {
                if (!_evidenceSummaries.Contains(item))
                {
                    _evidenceSummaries.Add(item);
                }
            }
        }

        if (ConsensusReached && Stage == CoalitionStage.Forming)
        {
            Stage = CoalitionStage.ConsensusReached;
        }
    }

    public void Dissolve()
    {
        _members.Clear();
        _evidenceSummaries.Clear();
        CombinedSuspicionScore = 0f;
        Stage = CoalitionStage.Concluded;
    }

    internal static AccusationCoalition Restore(
        EntityId initiator,
        EntityId target,
        IEnumerable<EntityId> members,
        IEnumerable<string> evidenceSummaries,
        float combinedSuspicionScore,
        CoalitionStage stage)
    {
        var coalition = new AccusationCoalition(initiator, target);
        coalition._members.Clear();
        foreach (EntityId member in members)
        {
            if (!member.IsEmpty && member != target)
            {
                _ = coalition._members.Add(member);
            }
        }

        coalition._evidenceSummaries.Clear();
        foreach (string evidence in evidenceSummaries.Where(item => !string.IsNullOrWhiteSpace(item)))
        {
            if (!coalition._evidenceSummaries.Contains(evidence, StringComparer.Ordinal))
            {
                coalition._evidenceSummaries.Add(evidence);
            }
        }

        coalition.CombinedSuspicionScore = combinedSuspicionScore;
        coalition.Stage = stage;
        return coalition;
    }
}
