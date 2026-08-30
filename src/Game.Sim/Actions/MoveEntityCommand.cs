using Game.Sim.Entities;
using Game.Sim.Locations;

namespace Game.Sim.Actions;

public sealed record MoveEntityCommand
{
    public MoveEntityCommand(EntityId actor, LocationId destination)
    {
        if (actor.IsEmpty)
        {
            throw new ArgumentException("Actor ID cannot be empty.", nameof(actor));
        }

        if (destination.IsEmpty)
        {
            throw new ArgumentException("Destination cannot be empty.", nameof(destination));
        }

        Actor = actor;
        Destination = destination;
    }

    public EntityId Actor { get; }

    public LocationId Destination { get; }
}
