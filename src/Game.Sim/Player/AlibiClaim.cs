using Game.Sim.Entities;
using Game.Sim.Locations;
using Game.Sim.Time;

namespace Game.Sim.Player;

/// <summary>
/// Something a character stated out loud about where they were.
/// </summary>
/// <remarks>
/// Deliberately carries no flag saying whether it is true. Whether a claim holds
/// is decided later by comparing it against evidence, which is the only way the
/// player can decide it too. Storing the answer here would make it one
/// dereference away from leaking into the UI.
/// </remarks>
public sealed record AlibiClaim
{
    public AlibiClaim(
        int id,
        EntityId speaker,
        LocationId claimedLocation,
        SimTime claimedTime,
        SimTime statedAt)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(id);
        if (speaker.IsEmpty)
        {
            throw new ArgumentException("Claim speaker cannot be empty.", nameof(speaker));
        }

        if (claimedLocation.IsEmpty)
        {
            throw new ArgumentException("Claimed location cannot be empty.", nameof(claimedLocation));
        }

        Id = id;
        Speaker = speaker;
        ClaimedLocation = claimedLocation;
        ClaimedTime = claimedTime;
        StatedAt = statedAt;
    }

    public int Id { get; }

    public EntityId Speaker { get; }

    public LocationId ClaimedLocation { get; }

    /// <summary>The moment the claim is about.</summary>
    public SimTime ClaimedTime { get; }

    /// <summary>The moment the claim was made to the player.</summary>
    public SimTime StatedAt { get; }
}
