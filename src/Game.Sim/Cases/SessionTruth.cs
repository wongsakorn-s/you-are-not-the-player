using Game.Sim.Entities;
using Game.Sim.PlayerAi;

namespace Game.Sim.Cases;

/// <summary>
/// Everything a run knows that the cast does not.
/// </summary>
/// <remarks>
/// This type is the single home for hidden truth and must never be reachable
/// from <c>WorldEvent</c>, <c>Observation</c>, <c>MemoryRecord</c> or
/// <c>SuspicionCase</c>. NPCs are only allowed to infer these facts from
/// behaviour that actually happened, so nothing in the perception, memory or
/// suspicion pipeline may read a <see cref="SessionTruth"/>. The three roles are
/// deliberately independent: the body the human drives, the character the Player
/// AI is steering, and whoever actually opened the basement door can be three
/// different people, or the same person wearing all three hats.
/// </remarks>
public sealed class SessionTruth
{
    public SessionTruth(
        ulong seed,
        EntityId humanHost,
        EntityId hiddenPlayer,
        PlayerAiArchetype hiddenPlayerArchetype,
        EntityId incidentCulprit,
        IEnumerable<SecretAssignment>? secrets = null,
        IEnumerable<AnomalyBeat>? anomalySchedule = null)
    {
        if (humanHost.IsEmpty)
        {
            throw new ArgumentException("Human host cannot be empty.", nameof(humanHost));
        }

        if (hiddenPlayer.IsEmpty)
        {
            throw new ArgumentException("Hidden player cannot be empty.", nameof(hiddenPlayer));
        }

        if (incidentCulprit.IsEmpty)
        {
            throw new ArgumentException("Incident culprit cannot be empty.", nameof(incidentCulprit));
        }

        if (!Enum.IsDefined(hiddenPlayerArchetype))
        {
            throw new ArgumentOutOfRangeException(
                nameof(hiddenPlayerArchetype),
                hiddenPlayerArchetype,
                "Unknown archetype.");
        }

        SecretAssignment[] materializedSecrets = (secrets ?? []).ToArray();
        if (materializedSecrets.Any(secret => secret is null))
        {
            throw new ArgumentException("Secrets cannot contain null values.", nameof(secrets));
        }

        if (materializedSecrets
                .Select(secret => secret.Owner)
                .Distinct()
                .Count() != materializedSecrets.Length)
        {
            throw new ArgumentException("Each character can own at most one secret.", nameof(secrets));
        }

        AnomalyBeat[] materializedAnomalies = (anomalySchedule ?? []).ToArray();
        if (materializedAnomalies.Any(beat => beat is null))
        {
            throw new ArgumentException(
                "Anomaly schedule cannot contain null values.",
                nameof(anomalySchedule));
        }

        Seed = seed;
        HumanHost = humanHost;
        HiddenPlayer = hiddenPlayer;
        HiddenPlayerArchetype = hiddenPlayerArchetype;
        IncidentCulprit = incidentCulprit;
        Secrets = Array.AsReadOnly(materializedSecrets
            .OrderBy(secret => secret.Owner.Value, StringComparer.Ordinal)
            .ToArray());
        AnomalySchedule = Array.AsReadOnly(materializedAnomalies
            .OrderBy(beat => beat.Tick)
            .ThenBy(beat => beat.Subject.Value, StringComparer.Ordinal)
            .ToArray());
    }

    public ulong Seed { get; }

    /// <summary>The character the human being is driving this run.</summary>
    public EntityId HumanHost { get; }

    /// <summary>The character the Player AI is steering.</summary>
    public EntityId HiddenPlayer { get; }

    public PlayerAiArchetype HiddenPlayerArchetype { get; }

    /// <summary>Whoever actually caused the incident, Player or not.</summary>
    public EntityId IncidentCulprit { get; }

    public IReadOnlyList<SecretAssignment> Secrets { get; }

    public IReadOnlyList<AnomalyBeat> AnomalySchedule { get; }

    /// <summary>The human has been driving the hidden Player all along.</summary>
    public bool HostIsHiddenPlayer => HumanHost == HiddenPlayer;

    /// <summary>The incident was the Player's doing rather than an NPC secret.</summary>
    public bool HiddenPlayerIsCulprit => HiddenPlayer == IncidentCulprit;

    public SecretAssignment? GetSecret(EntityId owner) =>
        Secrets.SingleOrDefault(secret => secret.Owner == owner);

    /// <summary>
    /// A stable identity for the case this truth describes. Two runs with the
    /// same fingerprint pose the player the same question.
    /// </summary>
    public string Fingerprint() => string.Join(
        '|',
        HumanHost.Value,
        HiddenPlayer.Value,
        HiddenPlayerArchetype.ToString(),
        IncidentCulprit.Value,
        string.Join(
            ',',
            Secrets.Select(secret =>
                $"{secret.Owner.Value}:{secret.Behavior}:{secret.Accomplice?.Value ?? "-"}")),
        string.Join(
            ',',
            AnomalySchedule.Select(beat => $"{beat.Tick}:{beat.Kind}:{beat.Subject.Value}")));
}
