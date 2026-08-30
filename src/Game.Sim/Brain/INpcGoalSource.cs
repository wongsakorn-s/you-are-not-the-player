namespace Game.Sim.Brain;

public interface INpcGoalSource
{
    IReadOnlyList<GoalCandidate> Generate(NpcDecisionContext context);
}
