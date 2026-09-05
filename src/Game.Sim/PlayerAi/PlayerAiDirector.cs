using Game.Sim.Actions;
using Game.Sim.Brain;
using Game.Sim.Entities;
using Game.Sim.Routines;

namespace Game.Sim.PlayerAi;

public sealed class PlayerAiDirector : INpcGoalSource, INpcRoutineDecisionObserver
{
    private const float ArchetypeUtility = 100.0f;

    private readonly InteractionActionHandler _interactions;
    private readonly BoundaryProbeActionHandler _boundaryProbes;
    private readonly Dictionary<EntityId, AgentState> _agents;

    public PlayerAiDirector(
        IEnumerable<PlayerAiProfile> profiles,
        InteractionActionHandler interactions,
        BoundaryProbeActionHandler boundaryProbes)
    {
        ArgumentNullException.ThrowIfNull(profiles);
        ArgumentNullException.ThrowIfNull(interactions);
        ArgumentNullException.ThrowIfNull(boundaryProbes);
        PlayerAiProfile[] suppliedProfiles = profiles.ToArray();
        if (suppliedProfiles.Any(profile => profile is null))
        {
            throw new ArgumentException("Player AI profiles cannot contain null values.", nameof(profiles));
        }

        if (suppliedProfiles.Select(profile => profile.Entity).Distinct().Count() !=
            suppliedProfiles.Length)
        {
            throw new ArgumentException("Each entity can have only one Player AI profile.", nameof(profiles));
        }

        _interactions = interactions;
        _boundaryProbes = boundaryProbes;
        _agents = suppliedProfiles.ToDictionary(
            profile => profile.Entity,
            profile => new AgentState(profile));
    }

    public IReadOnlyList<GoalCandidate> Generate(NpcDecisionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!_agents.TryGetValue(context.Entity.Id, out AgentState? state))
        {
            return [];
        }

        // Blending in is part of the plan, not the absence of one.
        if (state.Rest > 0)
        {
            state.Rest--;
            return [];
        }

        return state.Profile.Archetype switch
        {
            PlayerAiArchetype.Explorer => CreateExplorerGoal(state),
            PlayerAiArchetype.Completionist or PlayerAiArchetype.Roleplayer =>
                CreateCompletionistGoal(state),
            _ => throw new InvalidOperationException(
                $"Unsupported Player AI archetype '{state.Profile.Archetype}'."),
        };
    }

    public void Observe(NpcRoutineDecision decision)
    {
        ArgumentNullException.ThrowIfNull(decision);
        if (!_agents.TryGetValue(decision.Entity, out AgentState? state))
        {
            return;
        }

        switch (state.Profile.Archetype)
        {
            case PlayerAiArchetype.Explorer:
                CompleteExploration(state, decision);
                break;
            case PlayerAiArchetype.Completionist:
            case PlayerAiArchetype.Roleplayer:
                CompleteInteraction(state, decision);
                break;
            default:
                throw new InvalidOperationException(
                    $"Unsupported Player AI archetype '{state.Profile.Archetype}'.");
        }
    }

    private static IReadOnlyList<GoalCandidate> CreateExplorerGoal(AgentState state)
    {
        if (state.ExplorationIndex >= state.Profile.ExplorationObjectives.Count)
        {
            if (!state.Profile.Repeats || state.Profile.ExplorationObjectives.Count == 0)
            {
                return [];
            }

            state.ExplorationIndex = 0;
        }

        ExplorationObjective objective = state.Profile.ExplorationObjectives[state.ExplorationIndex];
        return [new GoalCandidate(
            GoalType.ExploreBoundary,
            objective.Location,
            ArchetypeUtility,
            [new UtilityReason("archetype:explorer", 0.0f)],
            objective.IgnoresRolePermissions,
            GetIntentId(PlayerAiArchetype.Explorer, objective.Id))];
    }

    private static IReadOnlyList<GoalCandidate> CreateCompletionistGoal(AgentState state)
    {
        if (state.CompletionIndex >= state.Profile.CompletionObjectives.Count)
        {
            if (!state.Profile.Repeats || state.Profile.CompletionObjectives.Count == 0)
            {
                return [];
            }

            state.CompletionIndex = 0;
        }

        CompletionObjective objective = state.Profile.CompletionObjectives[state.CompletionIndex];
        return [new GoalCandidate(
            GoalType.CompleteInteraction,
            objective.Location,
            ArchetypeUtility,
            [new UtilityReason($"archetype:{state.Profile.Archetype}", 0.0f)],
            objective.IgnoresRolePermissions,
            GetIntentId(state.Profile.Archetype, objective.Id))];
    }

    private void CompleteExploration(AgentState state, NpcRoutineDecision decision)
    {
        if (state.ExplorationIndex >= state.Profile.ExplorationObjectives.Count)
        {
            return;
        }

        ExplorationObjective objective = state.Profile.ExplorationObjectives[state.ExplorationIndex];
        if (!Matches(decision, GoalType.ExploreBoundary, objective.Location,
            GetIntentId(PlayerAiArchetype.Explorer, objective.Id)))
        {
            return;
        }

        _ = _boundaryProbes.Execute(new BoundaryProbeCommand(
            decision.Entity,
            objective.BoundaryId));
        state.ExplorationIndex++;
        StartRestIfBurstFinished(state);
    }

    private void CompleteInteraction(AgentState state, NpcRoutineDecision decision)
    {
        if (state.CompletionIndex >= state.Profile.CompletionObjectives.Count)
        {
            return;
        }

        CompletionObjective objective = state.Profile.CompletionObjectives[state.CompletionIndex];
        if (!Matches(decision, GoalType.CompleteInteraction, objective.Location,
            GetIntentId(state.Profile.Archetype, objective.Id)))
        {
            return;
        }

        _ = _interactions.Execute(new InteractionCommand(
            decision.Entity,
            objective.InteractionKind,
            objective.InteractionId));
        state.CompletionIndex++;
        StartRestIfBurstFinished(state);
    }

    private static void StartRestIfBurstFinished(AgentState state)
    {
        if (state.Profile.BurstSize <= 0)
        {
            return;
        }

        state.CompletedInBurst++;
        if (state.CompletedInBurst < state.Profile.BurstSize)
        {
            return;
        }

        state.CompletedInBurst = 0;
        state.Rest = state.Profile.RestDecisions;
    }

    private static bool Matches(
        NpcRoutineDecision decision,
        GoalType expectedGoal,
        Locations.LocationId expectedLocation,
        string expectedIntent) =>
        decision.Goal.Type == expectedGoal &&
        decision.Goal.Destination == expectedLocation &&
        string.Equals(decision.Goal.IntentId, expectedIntent, StringComparison.Ordinal);

    private static string GetIntentId(PlayerAiArchetype archetype, string objectiveId) =>
        $"player-ai:{archetype}:{objectiveId}";

    private sealed class AgentState(PlayerAiProfile profile)
    {
        public PlayerAiProfile Profile { get; } = profile;

        public int ExplorationIndex { get; set; }

        public int CompletionIndex { get; set; }

        public int CompletedInBurst { get; set; }

        public int Rest { get; set; }
    }
}
