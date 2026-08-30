using Game.Sim.Locations;

namespace Game.Sim.Entities;

public sealed class EntityState
{
    public EntityState(EntityId id, LocationId logicalLocation)
    {
        if (id.IsEmpty)
        {
            throw new ArgumentException("Entity ID cannot be empty.", nameof(id));
        }

        if (logicalLocation.IsEmpty)
        {
            throw new ArgumentException("Logical location cannot be empty.", nameof(logicalLocation));
        }

        Id = id;
        LogicalLocation = logicalLocation;
    }

    public EntityId Id { get; }

    public LocationId LogicalLocation { get; private set; }

    internal void MoveTo(LocationId destination) => LogicalLocation = destination;
}
