using Game.Sim.Entities;

namespace Game.Sim.PlayerAi;

public sealed class PlayerAiProfile
{
    public PlayerAiProfile(
        EntityId entity,
        PlayerAiArchetype archetype,
        IEnumerable<ExplorationObjective>? explorationObjectives = null,
        IEnumerable<CompletionObjective>? completionObjectives = null)
    {
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
            PlayerAiArchetype.Roleplayer => explorations.Length == 0 && completions.Length == 0,
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
    }

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
