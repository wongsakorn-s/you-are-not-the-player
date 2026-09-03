using Game.Sim.Memory;

namespace Game.Sim.Player;

/// <summary>
/// A claim the player holds evidence against.
/// </summary>
/// <param name="Claim">What was said.</param>
/// <param name="Evidence">The clue that disagrees with it.</param>
/// <param name="EvidenceIsFirstHand">
/// Whether the player saw it themselves. This is the whole risk of confronting:
/// a first-hand clue is worth staking a challenge on, a second-hand one is a
/// story that may already have been distorted by the time it reached you.
/// </param>
public sealed record Contradiction(
    AlibiClaim Claim,
    MemoryRecord Evidence,
    bool EvidenceIsFirstHand);
