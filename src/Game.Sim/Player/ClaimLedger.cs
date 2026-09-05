using Game.Sim.Entities;

namespace Game.Sim.Player;

/// <summary>
/// Everything the cast has told the player about their own whereabouts. Kept
/// separately from memory because a claim is not evidence: it is a statement that
/// evidence can later agree or disagree with.
/// </summary>
public sealed class ClaimLedger
{
    private readonly List<AlibiClaim> _claims = [];
    private int _nextId = 1;

    public IReadOnlyList<AlibiClaim> Claims => _claims;

    public AlibiClaim Record(
        EntityId speaker,
        Locations.LocationId claimedLocation,
        Time.SimTime claimedTime,
        Time.SimTime statedAt)
    {
        var claim = new AlibiClaim(_nextId++, speaker, claimedLocation, claimedTime, statedAt);
        _claims.Add(claim);
        return claim;
    }

    public IReadOnlyList<AlibiClaim> ClaimsBy(EntityId speaker) => _claims
        .Where(claim => claim.Speaker == speaker)
        .ToArray();

    public AlibiClaim? GetById(int id) => _claims.SingleOrDefault(claim => claim.Id == id);

    public void Restore(IEnumerable<AlibiClaim> claims)
    {
        ArgumentNullException.ThrowIfNull(claims);
        _claims.Clear();
        _claims.AddRange(claims);
        _nextId = _claims.Count == 0 ? 1 : _claims.Max(claim => claim.Id) + 1;
    }
}
