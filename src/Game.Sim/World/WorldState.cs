using Game.Sim.Entities;
using Game.Sim.Locations;

namespace Game.Sim.World;

public sealed class WorldState
{
    private readonly Dictionary<EntityId, EntityState> _entities = [];
    private readonly Dictionary<LocationId, LocationState> _locations = [];

    public IReadOnlyList<EntityState> Entities => _entities.Values
        .OrderBy(entity => entity.Id.Value, StringComparer.Ordinal)
        .ToArray();

    public IReadOnlyList<LocationState> Locations => _locations.Values
        .OrderBy(location => location.Id.Value, StringComparer.Ordinal)
        .ToArray();

    public void AddLocation(LocationState location)
    {
        ArgumentNullException.ThrowIfNull(location);

        if (!_locations.TryAdd(location.Id, location))
        {
            throw new InvalidOperationException($"Location '{location.Id}' already exists.");
        }
    }

    public void AddEntity(EntityState entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        if (!_locations.ContainsKey(entity.LogicalLocation))
        {
            throw new InvalidOperationException(
                $"Cannot add entity '{entity.Id}' to unknown location '{entity.LogicalLocation}'.");
        }

        if (!_entities.TryAdd(entity.Id, entity))
        {
            throw new InvalidOperationException($"Entity '{entity.Id}' already exists.");
        }
    }

    public EntityState GetEntity(EntityId id) =>
        _entities.TryGetValue(id, out EntityState? entity)
            ? entity
            : throw new KeyNotFoundException($"Entity '{id}' does not exist.");

    public LocationState GetLocation(LocationId id) =>
        _locations.TryGetValue(id, out LocationState? location)
            ? location
            : throw new KeyNotFoundException($"Location '{id}' does not exist.");

    public void MoveEntity(EntityId entityId, LocationId destination)
    {
        EntityState entity = GetEntity(entityId);
        _ = GetLocation(destination);
        entity.MoveTo(destination);
    }
}
