using Game.Sim.Entities;
using Game.Sim.Memory;

namespace Game.Sim.Player;

/// <summary>
/// Matches what people said against what the player has actually seen or heard.
/// </summary>
public static class ContradictionFinder
{
    /// <summary>
    /// How far apart a clue and a claim can sit and still be about the same
    /// moment. Wide enough that the player is not asked to match exact minutes,
    /// narrow enough that "earlier tonight" is not treated as an alibi.
    /// </summary>
    public const long ToleranceTicks = 20;

    public static IReadOnlyList<Contradiction> Find(
        IEnumerable<AlibiClaim> claims,
        IEnumerable<MemoryRecord> playerMemories)
    {
        ArgumentNullException.ThrowIfNull(claims);
        ArgumentNullException.ThrowIfNull(playerMemories);

        MemoryRecord[] usable = playerMemories
            .Where(memory => memory.Subject is not null && memory.Location is { IsEmpty: false })
            .ToArray();

        var found = new List<Contradiction>();
        foreach (AlibiClaim claim in claims)
        {
            foreach (MemoryRecord memory in usable)
            {
                if (memory.Subject != claim.Speaker ||
                    memory.Location == claim.ClaimedLocation)
                {
                    continue;
                }

                // A person can only be in one place at a time, so a clue that puts
                // them somewhere else at the same moment is the contradiction.
                if (Math.Abs(memory.EventTime.Tick - claim.ClaimedTime.Tick) > ToleranceTicks)
                {
                    continue;
                }

                found.Add(new Contradiction(
                    claim,
                    memory,
                    memory.Kind == MemoryKind.Episodic));
            }
        }

        return found
            // First-hand first: that is the evidence worth spending a confrontation on.
            .OrderByDescending(item => item.EvidenceIsFirstHand)
            .ThenByDescending(item => item.Evidence.EventTime.Tick)
            .ThenBy(item => item.Claim.Id)
            .ToArray();
    }

    public static bool ContradictsAnyClaim(
        IEnumerable<AlibiClaim> claims,
        MemoryRecord evidence,
        EntityId speaker,
        out AlibiClaim? contradicted)
    {
        ArgumentNullException.ThrowIfNull(claims);
        ArgumentNullException.ThrowIfNull(evidence);

        IReadOnlyList<Contradiction> matches = Find(
            claims.Where(claim => claim.Speaker == speaker),
            [evidence]);
        contradicted = matches.Count == 0 ? null : matches[0].Claim;
        return contradicted is not null;
    }
}
