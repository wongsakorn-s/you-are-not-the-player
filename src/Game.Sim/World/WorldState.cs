using Game.Sim.Entities;
using Game.Sim.Locations;

namespace Game.Sim.World;

public sealed class WorldState
{
    private readonly Dictionary<EntityId, EntityState> _entities = [];
    private readonly Dictionary<LocationId, LocationState> _locations = [];
    private readonly Dictionary<LocationPair, float> _audioConnections = [];

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

    public void ConnectLocations(
        LocationId first,
        LocationId second,
        float audioTransmission = 1.0f)
    {
        _ = GetLocation(first);
        _ = GetLocation(second);

        if (first == second)
        {
            throw new ArgumentException("A location cannot be connected to itself.", nameof(second));
        }

        if (float.IsNaN(audioTransmission) || audioTransmission is < 0.0f or > 1.0f)
        {
            throw new ArgumentOutOfRangeException(
                nameof(audioTransmission),
                audioTransmission,
                "Audio transmission must be between 0 and 1 inclusive.");
        }

        LocationPair pair = LocationPair.Create(first, second);
        if (!_audioConnections.TryAdd(pair, audioTransmission))
        {
            throw new InvalidOperationException(
                $"Locations '{pair.First}' and '{pair.Second}' are already connected.");
        }
    }

    public float? GetAudioTransmission(LocationId first, LocationId second)
    {
        _ = GetLocation(first);
        _ = GetLocation(second);

        if (first == second)
        {
            return 1.0f;
        }

        return _audioConnections.TryGetValue(LocationPair.Create(first, second), out float transmission)
            ? transmission
            : null;
    }

    internal void RelocateEntity(EntityId entityId, LocationId destination)
    {
        EntityState entity = GetEntity(entityId);
        _ = GetLocation(destination);
        entity.MoveTo(destination);
    }

    private readonly record struct LocationPair(LocationId First, LocationId Second)
    {
        public static LocationPair Create(LocationId first, LocationId second) =>
            string.CompareOrdinal(first.Value, second.Value) <= 0
                ? new LocationPair(first, second)
                : new LocationPair(second, first);
    }
}
