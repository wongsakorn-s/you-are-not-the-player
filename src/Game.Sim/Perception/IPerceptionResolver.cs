using Game.Sim.Entities;
using Game.Sim.Events;
using Game.Sim.World;

namespace Game.Sim.Perception;

public interface IPerceptionResolver
{
    IReadOnlyList<Observation> Observe(
        EntityState observer,
        WorldEvent worldEvent,
        WorldState world);
}
