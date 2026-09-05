using Game.Sim.Entities;
using Game.Sim.PlayerAi;

namespace Game.Sim.Cases;

/// <summary>
/// The roster and the rails the generator has to stay on. Content decides which
/// facts a case is allowed to vary; the seed decides which way each one lands.
/// </summary>
public sealed record CaseGenerationOptions
{
    public const int DefaultAnomalyCount = 3;

    public CaseGenerationOptions(
        EntityId humanHost,
        IEnumerable<EntityId> roster,
        long shiftTicks,
        EntityId? pinnedHiddenPlayer = null,
        EntityId? pinnedIncidentCulprit = null,
        PlayerAiArchetype? pinnedArchetype = null,
        bool allowHostAsHiddenPlayer = false,
        float hiddenPlayerIsCulpritChance = 0.35f,
        float secretChancePerCharacter = 0.45f,
        int anomalyCount = DefaultAnomalyCount)
    {
        ArgumentNullException.ThrowIfNull(roster);
        if (humanHost.IsEmpty)
        {
            throw new ArgumentException("Human host cannot be empty.", nameof(humanHost));
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(shiftTicks);
        ArgumentOutOfRangeException.ThrowIfNegative(anomalyCount);
        ThrowIfNotProbability(hiddenPlayerIsCulpritChance, nameof(hiddenPlayerIsCulpritChance));
        ThrowIfNotProbability(secretChancePerCharacter, nameof(secretChancePerCharacter));

        // Ordinal ordering, not input order: the generator's draws must not depend
        // on how a caller happened to enumerate the cast.
        EntityId[] materializedRoster = roster
            .Distinct()
            .OrderBy(entity => entity.Value, StringComparer.Ordinal)
            .ToArray();
        if (materializedRoster.Length == 0 || materializedRoster.Any(entity => entity.IsEmpty))
        {
            throw new ArgumentException(
                "The roster must contain at least one valid character.",
                nameof(roster));
        }

        if (!materializedRoster.Contains(humanHost))
        {
            throw new ArgumentException(
                $"Human host '{humanHost}' is not part of the roster.",
                nameof(humanHost));
        }

        ThrowIfNotOnRoster(pinnedHiddenPlayer, materializedRoster, nameof(pinnedHiddenPlayer));
        ThrowIfNotOnRoster(pinnedIncidentCulprit, materializedRoster, nameof(pinnedIncidentCulprit));

        if (pinnedArchetype is { } archetype && !Enum.IsDefined(archetype))
        {
            throw new ArgumentOutOfRangeException(
                nameof(pinnedArchetype),
                pinnedArchetype,
                "Unknown archetype.");
        }

        if (!allowHostAsHiddenPlayer &&
            pinnedHiddenPlayer is { } pinned &&
            pinned == humanHost)
        {
            throw new ArgumentException(
                "The host cannot be pinned as the hidden player unless " +
                $"{nameof(allowHostAsHiddenPlayer)} is set.",
                nameof(pinnedHiddenPlayer));
        }

        if (!allowHostAsHiddenPlayer && materializedRoster.Length < 2)
        {
            throw new ArgumentException(
                "A hidden player other than the host needs at least two characters.",
                nameof(roster));
        }

        HumanHost = humanHost;
        Roster = Array.AsReadOnly(materializedRoster);
        ShiftTicks = shiftTicks;
        PinnedHiddenPlayer = pinnedHiddenPlayer;
        PinnedIncidentCulprit = pinnedIncidentCulprit;
        PinnedArchetype = pinnedArchetype;
        AllowHostAsHiddenPlayer = allowHostAsHiddenPlayer;
        HiddenPlayerIsCulpritChance = hiddenPlayerIsCulpritChance;
        SecretChancePerCharacter = secretChancePerCharacter;
        AnomalyCount = anomalyCount;
    }

    public EntityId HumanHost { get; }

    public IReadOnlyList<EntityId> Roster { get; }

    public long ShiftTicks { get; }

    /// <summary>Content override; leave null to let the seed choose.</summary>
    public EntityId? PinnedHiddenPlayer { get; }

    /// <summary>
    /// Content override; leave null to let the seed choose. The first playable
    /// case pins this because its ending prose names the culprit directly.
    /// </summary>
    public EntityId? PinnedIncidentCulprit { get; }

    /// <summary>Content override; leave null to let the seed choose.</summary>
    public PlayerAiArchetype? PinnedArchetype { get; }

    /// <summary>
    /// Whether the human may turn out to have been driving the Player all along.
    /// Off by default: the accusation screen cannot yet name the host, so a case
    /// generated that way would be unwinnable until the "you were the Player"
    /// ending exists.
    /// </summary>
    public bool AllowHostAsHiddenPlayer { get; }

    public float HiddenPlayerIsCulpritChance { get; }

    public float SecretChancePerCharacter { get; }

    public int AnomalyCount { get; }

    private static void ThrowIfNotProbability(float value, string parameterName)
    {
        if (!float.IsFinite(value) || value is < 0.0f or > 1.0f)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                "A probability must be between 0 and 1.");
        }
    }

    private static void ThrowIfNotOnRoster(
        EntityId? candidate,
        IReadOnlyCollection<EntityId> roster,
        string parameterName)
    {
        if (candidate is { } entity && !roster.Contains(entity))
        {
            throw new ArgumentException(
                $"'{entity}' is not part of the roster.",
                parameterName);
        }
    }
}
