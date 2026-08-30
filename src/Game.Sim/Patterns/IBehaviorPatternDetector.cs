using Game.Sim.Events;

namespace Game.Sim.Patterns;

public interface IBehaviorPatternDetector
{
    IReadOnlyList<BehaviorPatternMatch> Process(WorldEvent worldEvent);
}
