using Game.Sim.Entities;
using Game.Sim.Locations;

namespace Game.Client.Godot.Adapters;

public sealed class LocationTransitionTracker
{
    private readonly Dictionary<EntityId, LocationId> _requested = [];
    private readonly Dictionary<EntityId, LocationId> _confirmed = [];

    public IReadOnlyDictionary<EntityId, LocationId> ConfirmedLocations => _confirmed;

    public void Initialize(EntityId actor, LocationId location)
    {
        Validate(actor, location);
        _requested[actor] = location;
        _confirmed[actor] = location;
    }

    public void Request(EntityId actor, LocationId location)
    {
        Validate(actor, location);
        if (!_confirmed.ContainsKey(actor))
        {
            throw new InvalidOperationException($"Actor '{actor}' has not been initialized.");
        }

        _requested[actor] = location;
    }

    public bool Confirm(EntityId actor, LocationId location)
    {
        Validate(actor, location);
        if (!_requested.TryGetValue(actor, out LocationId requested) || requested != location)
        {
            return false;
        }

        _confirmed[actor] = location;
        return true;
    }

    public void CancelRequest(EntityId actor)
    {
        if (!_confirmed.TryGetValue(actor, out LocationId confirmed))
        {
            throw new InvalidOperationException($"Actor '{actor}' has not been initialized.");
        }

        _requested[actor] = confirmed;
    }

    public bool IsInTransit(EntityId actor) =>
        _requested.TryGetValue(actor, out LocationId requested) &&
        (!_confirmed.TryGetValue(actor, out LocationId confirmed) || requested != confirmed);

    public LocationId GetRequestedLocation(EntityId actor) =>
        _requested.TryGetValue(actor, out LocationId location)
            ? location
            : throw new KeyNotFoundException($"Actor '{actor}' has no requested location.");

    private static void Validate(EntityId actor, LocationId location)
    {
        if (actor.IsEmpty)
        {
            throw new ArgumentException("Actor ID cannot be empty.", nameof(actor));
        }

        if (location.IsEmpty)
        {
            throw new ArgumentException("Location ID cannot be empty.", nameof(location));
        }
    }
}
