namespace Game.Sim.Suspicion;

public interface ISuspicionRuleRepository
{
    IReadOnlyList<SuspicionRule> Rules { get; }
}
