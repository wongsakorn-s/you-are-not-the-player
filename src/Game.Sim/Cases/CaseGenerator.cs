using Game.Sim.Anomalies;
using Game.Sim.Entities;
using Game.Sim.PlayerAi;
using Game.Sim.Random;
using Game.Sim.Secrets;

namespace Game.Sim.Cases;

/// <summary>
/// Turns a seed into the hidden truth of one run.
/// </summary>
/// <remarks>
/// Two rules govern everything here. The same seed and options must always
/// produce the same <see cref="SessionTruth"/>, so every draw comes from one
/// PCG32 stream consumed in a fixed order and every candidate list is sorted
/// ordinally before it is indexed. And a different seed has to move something a
/// player would notice, so the hidden player is drawn first from the widest pool
/// available rather than nudged from a default.
/// </remarks>
public static class CaseGenerator
{
    /// <summary>
    /// A stream of its own, so adding a draw here can never shift the numbers
    /// the simulation itself pulls from the same seed.
    /// </summary>
    public const ulong RandomSequence = 7717UL;

    private static readonly PlayerAiArchetype[] Archetypes =
        Enum.GetValues<PlayerAiArchetype>().OrderBy(archetype => (int)archetype).ToArray();

    private static readonly AnomalyKind[] AnomalyKinds =
        Enum.GetValues<AnomalyKind>().OrderBy(kind => (int)kind).ToArray();

    private static readonly SecretBehaviorKind[] SecretBehaviors =
        Enum.GetValues<SecretBehaviorKind>().OrderBy(behavior => (int)behavior).ToArray();

    public static SessionTruth Generate(ulong seed, CaseGenerationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var random = new Pcg32SimRandom(seed, RandomSequence);

        EntityId hiddenPlayer = options.PinnedHiddenPlayer ?? PickHiddenPlayer(random, options);
        PlayerAiArchetype archetype = options.PinnedArchetype ?? Pick(random, Archetypes);
        EntityId culprit = options.PinnedIncidentCulprit
            ?? PickCulprit(random, options, hiddenPlayer);
        List<SecretAssignment> secrets = PickSecrets(random, options, hiddenPlayer);
        List<AnomalyBeat> anomalies = PickAnomalies(random, options, hiddenPlayer);

        return new SessionTruth(
            seed,
            options.HumanHost,
            hiddenPlayer,
            archetype,
            culprit,
            secrets,
            anomalies);
    }

    private static EntityId PickHiddenPlayer(Pcg32SimRandom random, CaseGenerationOptions options)
    {
        EntityId[] candidates = options.AllowHostAsHiddenPlayer
            ? [.. options.Roster]
            : [.. options.Roster.Where(entity => entity != options.HumanHost)];
        return Pick(random, candidates);
    }

    private static EntityId PickCulprit(
        Pcg32SimRandom random,
        CaseGenerationOptions options,
        EntityId hiddenPlayer)
    {
        // Drawn unconditionally so the stream position does not depend on the
        // branch taken; otherwise every downstream draw would shift.
        bool playerDidIt = random.Chance(options.HiddenPlayerIsCulpritChance);
        EntityId[] others = [.. options.Roster.Where(entity => entity != hiddenPlayer)];
        EntityId fallback = others.Length == 0 ? hiddenPlayer : Pick(random, others);
        return playerDidIt ? hiddenPlayer : fallback;
    }

    private static List<SecretAssignment> PickSecrets(
        Pcg32SimRandom random,
        CaseGenerationOptions options,
        EntityId hiddenPlayer)
    {
        // The hidden player is excluded on purpose. Player-like behaviour has to
        // stand on its own; handing that character an NPC secret as well would
        // blur the very distinction the suspicion vector exists to draw.
        EntityId[] candidates = [.. options.Roster
            .Where(entity => entity != hiddenPlayer && entity != options.HumanHost)];
        var assignments = new List<SecretAssignment>();
        foreach (EntityId owner in candidates)
        {
            if (!random.Chance(options.SecretChancePerCharacter))
            {
                continue;
            }

            SecretBehaviorKind behavior = Pick(random, SecretBehaviors);
            if (behavior != SecretBehaviorKind.SecretMeeting)
            {
                assignments.Add(new SecretAssignment(owner, behavior));
                continue;
            }

            EntityId[] accomplices = [.. options.Roster.Where(entity => entity != owner)];
            assignments.Add(new SecretAssignment(owner, behavior, Pick(random, accomplices)));
        }

        return assignments;
    }

    private static List<AnomalyBeat> PickAnomalies(
        Pcg32SimRandom random,
        CaseGenerationOptions options,
        EntityId hiddenPlayer)
    {
        var beats = new List<AnomalyBeat>(options.AnomalyCount);
        for (int index = 0; index < options.AnomalyCount; index++)
        {
            // Spread one beat per slice of the shift so the schedule stays legible
            // instead of clumping every anomaly into the same few minutes.
            long sliceStart = options.ShiftTicks * index / options.AnomalyCount;
            long sliceEnd = options.ShiftTicks * (index + 1) / options.AnomalyCount;
            long tick = sliceEnd <= sliceStart
                ? sliceStart
                : sliceStart + random.NextInt(0, (int)Math.Min(int.MaxValue, sliceEnd - sliceStart));
            AnomalyKind kind = Pick(random, AnomalyKinds);

            // Anomalies read as evidence about whoever they land on, so most of
            // them point at the hidden player and the rest are decoys.
            EntityId[] others = [.. options.Roster.Where(entity => entity != hiddenPlayer)];
            EntityId decoy = others.Length == 0 ? hiddenPlayer : Pick(random, others);
            EntityId subject = random.Chance(0.6f) ? hiddenPlayer : decoy;
            beats.Add(new AnomalyBeat(tick, kind, subject));
        }

        return beats;
    }

    private static T Pick<T>(Pcg32SimRandom random, IReadOnlyList<T> candidates) =>
        candidates.Count == 1
            ? candidates[0]
            : candidates[random.NextInt(0, candidates.Count)];
}
