using Game.Sim.Entities;
using Game.Sim.Events;
using Game.Sim.World;

namespace Game.Sim.Perception;

public sealed class PerceptionSystem
{
    private readonly IPerceptionResolver _resolver;

    public PerceptionSystem(IPerceptionResolver resolver)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        _resolver = resolver;
    }

    public IReadOnlyList<Observation> Process(WorldEvent worldEvent, WorldState world)
    {
        ArgumentNullException.ThrowIfNull(worldEvent);
        ArgumentNullException.ThrowIfNull(world);

        var observations = new List<Observation>();
        foreach (EntityState observer in world.Entities)
        {
            observations.AddRange(_resolver.Observe(observer, worldEvent, world));
        }

        return observations;
    }
}
