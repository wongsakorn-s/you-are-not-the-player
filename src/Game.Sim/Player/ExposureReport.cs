using Game.Sim.Entities;
using Game.Sim.Suspicion;

namespace Game.Sim.Player;

/// <summary>
/// The other half of the deduction: while the host works out who the Player is,
/// the hotel is working out the same thing about them.
/// </summary>
/// <remarks>
/// This reads the ordinary suspicion pipeline with the host as the subject. It
/// adds no hidden knowledge - every number here came from something an NPC
/// actually perceived, which is what makes it fair to show the player.
/// </remarks>
public sealed class ExposureReport
{
    /// <summary>
    /// Set from a played night rather than from the rule table. Weighted, the
    /// things that can happen to a host are: one confirmed absence from your post
    /// 14, one restricted-area entry 15, a loot sweep 44, one reality anomaly 60.
    /// </summary>
    /// <remarks>
    /// The first bar sits below a single absence on purpose - fifteen nights were
    /// played with it above, and the meter never once left Unnoticed, because the
    /// only thing an investigating player reliably does scored 14.4. The upper
    /// bars sit above a single anomaly for the opposite reason: one impossible
    /// thing used to clear two tiers in one step, which turned the whole ladder
    /// into an on-off switch. Now one gets you looked at, two gets you watched,
    /// and it takes a run of them to corner you.
    /// </remarks>
    public const float NoticedThreshold = 12.0f;

    public const float WatchedThreshold = 70.0f;

    public const float CorneredThreshold = 130.0f;

    public ExposureReport(
        EntityId host,
        IEnumerable<ObserverExposure> observers,
        IEnumerable<ExposureReason> reasons)
    {
        ArgumentNullException.ThrowIfNull(observers);
        ArgumentNullException.ThrowIfNull(reasons);
        if (host.IsEmpty)
        {
            throw new ArgumentException("Exposure host cannot be empty.", nameof(host));
        }

        Host = host;
        Observers = Array.AsReadOnly(observers
            .OrderByDescending(observer => observer.Score)
            .ThenBy(observer => observer.Observer.Value, StringComparer.Ordinal)
            .ToArray());
        Reasons = Array.AsReadOnly(reasons
            .OrderByDescending(reason => reason.Weight)
            .ThenBy(reason => reason.Observer.Value, StringComparer.Ordinal)
            .ThenBy(reason => reason.RuleId, StringComparer.Ordinal)
            .ToArray());
    }

    public EntityId Host { get; }

    /// <summary>Every character holding something, worst first.</summary>
    public IReadOnlyList<ObserverExposure> Observers { get; }

    public IReadOnlyList<ExposureReason> Reasons { get; }

    /// <summary>
    /// Driven by the single most convinced character rather than by the sum. One
    /// person who is sure is what starts a coalition; five people who mildly
    /// wonder is just a normal night in a hotel.
    /// </summary>
    public float Peak => Observers.Count == 0 ? 0.0f : Observers[0].Score;

    /// <summary>How many characters hold anything at all.</summary>
    public int Spread => Observers.Count(observer => observer.EvidenceCount > 0);

    /// <summary>
    /// The part of <see cref="Peak"/> that reads as "not behaving like a person
    /// who lives here" rather than as an ordinary secret.
    /// </summary>
    public float PlayerLikePeak => Observers.Count == 0 ? 0.0f : Observers.Max(o => o.PlayerLikeScore);

    public ExposureLevel Level => Peak switch
    {
        >= CorneredThreshold => ExposureLevel.Cornered,
        >= WatchedThreshold => ExposureLevel.Watched,
        >= NoticedThreshold => ExposureLevel.Noticed,
        _ => ExposureLevel.Unnoticed,
    };

    public ObserverExposure? MostSuspicious => Observers.Count == 0 ? null : Observers[0];

    public ExposureReason? LeadingReason => Reasons.Count == 0 ? null : Reasons[0];

    /// <summary>
    /// Whether this observer has enough on the host to start holding back. Read
    /// per observer, not from <see cref="Level"/>: the person who caught you is
    /// the person who gets guarded, and everyone else still talks.
    /// </summary>
    public bool IsGuardedTowards(EntityId observer) =>
        ScoreFor(observer) >= WatchedThreshold;

    public bool RefusesToGossipWith(EntityId observer) =>
        ScoreFor(observer) >= CorneredThreshold;

    public float ScoreFor(EntityId observer) => Observers
        .SingleOrDefault(item => item.Observer == observer)?.Score ?? 0.0f;

    /// <summary>
    /// Weights the dimensions by how much each one says "this is not one of us".
    /// MetaBehavior and ImpossibleBehavior are the tells the design cares about;
    /// criminality and secrecy are things an ordinary NPC could also be guilty of,
    /// so they raise exposure more slowly.
    /// </summary>
    public static float WeighVector(SuspicionVector vector)
    {
        ArgumentNullException.ThrowIfNull(vector);
        return vector.MetaBehavior +
            vector.ImpossibleBehavior +
            (0.6f * vector.RoleDeviation) +
            (0.6f * vector.Deception) +
            (0.4f * vector.Secrecy) +
            (0.4f * vector.Criminality);
    }

    public static float WeighPlayerLike(SuspicionVector vector)
    {
        ArgumentNullException.ThrowIfNull(vector);
        return vector.MetaBehavior + vector.ImpossibleBehavior;
    }
}
