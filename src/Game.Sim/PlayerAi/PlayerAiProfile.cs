using Game.Sim.Entities;

namespace Game.Sim.PlayerAi;

public sealed class PlayerAiProfile
{
    public PlayerAiProfile(
        EntityId entity,
        PlayerAiArchetype archetype,
        IEnumerable<ExplorationObjective>? explorationObjectives = null,
        IEnumerable<CompletionObjective>? completionObjectives = null,
        int burstSize = 0,
        int restDecisions = 0,
        bool repeats = false)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(burstSize);
        ArgumentOutOfRangeException.ThrowIfNegative(restDecisions);
        if (entity.IsEmpty)
        {
            throw new ArgumentException("Player AI entity cannot be empty.", nameof(entity));
        }

        if (!Enum.IsDefined(archetype))
        {
            throw new ArgumentOutOfRangeException(nameof(archetype), archetype, "Unknown archetype.");
        }

        ExplorationObjective[] explorations = MaterializeObjectives(
            explorationObjectives,
            objective => objective.Id,
            nameof(explorationObjectives));
        CompletionObjective[] completions = MaterializeObjectives(
            completionObjectives,
            objective => objective.Id,
            nameof(completionObjectives));

        bool validObjectives = archetype switch
        {
            PlayerAiArchetype.Explorer => explorations.Length > 0 && completions.Length == 0,
            PlayerAiArchetype.Completionist => explorations.Length == 0 && completions.Length > 0,
            // A Roleplayer performs an ordinary night rather than breaking one, so
            // its plan is made of ordinary interactions - the tell is that the
            // performance repeats, not that it trespasses.
            PlayerAiArchetype.Roleplayer => explorations.Length == 0,
            _ => false,
        };
        if (!validObjectives)
        {
            throw new ArgumentException(
                $"Objectives do not match archetype '{archetype}'.",
                nameof(archetype));
        }

        Entity = entity;
        Archetype = archetype;
        ExplorationObjectives = Array.AsReadOnly(explorations);
        CompletionObjectives = Array.AsReadOnly(completions);
        BurstSize = burstSize;
        RestDecisions = restDecisions;
        Repeats = repeats;
    }

    /// <summary>
    /// How many objectives are pursued back to back before blending in again.
    /// Zero means the plan runs straight through.
    /// </summary>
    /// <remarks>
    /// Behaviour has to arrive in clusters to be legible. The pattern detectors
    /// look for several related acts inside a window, and a player who reads
    /// as a player is one who does something odd, acts normal for a while, then
    /// does it again - not one who empties their whole plan in the first minutes
    /// and then behaves for the rest of the night.
    /// </remarks>
    public int BurstSize { get; }

    /// <summary>Decisions spent behaving ordinarily between bursts.</summary>
    public int RestDecisions { get; }

    /// <summary>
    /// Whether the plan starts over once it runs out.
    /// </summary>
    /// <remarks>
    /// Somebody playing a game does not stop playing it at twenty to two. A
    /// finite list means the plan has to be hand-sized to the length of the
    /// night, and gets it wrong: the first version ran dry around tick 160 of
    /// 360 and the hidden player spent the back half of every night behaving
    /// exactly like staff. Going back to a door that did not open the first time
    /// is also the most player-like thing in the list.
    /// </remarks>
    public bool Repeats { get; }

    public EntityId Entity { get; }

    public PlayerAiArchetype Archetype { get; }

    public IReadOnlyList<ExplorationObjective> ExplorationObjectives { get; }

    public IReadOnlyList<CompletionObjective> CompletionObjectives { get; }

    private static TObjective[] MaterializeObjectives<TObjective>(
        IEnumerable<TObjective>? objectives,
        Func<TObjective, string> getId,
        string parameterName)
        where TObjective : class
    {
        TObjective[] supplied = objectives?.ToArray() ?? [];
        if (supplied.Any(objective => objective is null))
        {
            throw new ArgumentException("Objectives cannot contain null values.", parameterName);
        }

        TObjective[] materialized = supplied
            .OrderBy(getId, StringComparer.Ordinal)
            .ToArray();
        if (materialized.Select(getId).Distinct(StringComparer.Ordinal).Count() != materialized.Length)
        {
            throw new ArgumentException("Objective IDs must be unique.", parameterName);
        }

        return materialized;
    }
}
