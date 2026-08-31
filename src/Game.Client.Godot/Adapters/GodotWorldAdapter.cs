using Game.Client.Godot.World;
using Game.Sim.Entities;
using Game.Sim.Locations;
using Godot;

namespace Game.Client.Godot.Adapters;

public sealed class GodotWorldAdapter
{
    private readonly Dictionary<LocationId, Vector3> _locationMarkers = [];
    private readonly HashSet<LocationId> _restrictedLocations = [];
    private readonly HashSet<LocationId> _accessibleLocations = [];
    private readonly Dictionary<EntityId, NpcActorNode> _actorViews = [];
    private readonly Dictionary<NpcActorNode, EntityId> _viewActors = [];
    private readonly Dictionary<EntityId, Vector3> _actorOffsets = [];
    private readonly LocationTransitionTracker _transitions = new();

    public event Action<EntityId, LocationId>? LocationConfirmed;

    public event Action<EntityId, LocationId>? NavigationFailed;

    public IReadOnlyDictionary<EntityId, LocationId> ConfirmedLocations =>
        _transitions.ConfirmedLocations;

    public IReadOnlyDictionary<LocationId, Vector3> LocationMarkers => _locationMarkers;

    public NpcActorNode? GetActorView(EntityId actor) => _actorViews.GetValueOrDefault(actor);

    public void RegisterLocation(
        LocationId location,
        Vector3 worldPosition,
        bool requiresAccess = false)
    {
        if (!_locationMarkers.TryAdd(location, worldPosition))
        {
            throw new InvalidOperationException($"Location '{location}' is already registered.");
        }

        if (requiresAccess)
        {
            _restrictedLocations.Add(location);
        }
    }

    public void RegisterActor(EntityId actor, NpcActorNode view, Vector3 offset)
    {
        ArgumentNullException.ThrowIfNull(view);
        if (!_actorViews.TryAdd(actor, view))
        {
            throw new InvalidOperationException($"Actor '{actor}' is already registered.");
        }

        _actorOffsets.Add(actor, offset);
        _viewActors.Add(view, actor);
        view.DestinationReached += OnDestinationReached;
        view.NavigationFailed += OnNavigationFailed;
    }

    public void Synchronize(
        IReadOnlyDictionary<EntityId, LocationId> logicalLocations,
        bool immediate)
    {
        ArgumentNullException.ThrowIfNull(logicalLocations);
        foreach ((EntityId actor, LocationId location) in logicalLocations)
        {
            RequestMove(actor, location, immediate);
        }
    }

    public void RequestMove(EntityId actor, LocationId location, bool immediate = false)
    {
        if (!_actorViews.TryGetValue(actor, out NpcActorNode? view))
        {
            throw new InvalidOperationException($"Actor view '{actor}' is not registered.");
        }

        if (!_locationMarkers.TryGetValue(location, out Vector3 marker))
        {
            throw new InvalidOperationException($"Location marker '{location}' is not registered.");
        }

        if (immediate)
        {
            view.MoveTo(marker + _actorOffsets[actor], immediate: true);
            _transitions.Initialize(actor, location);
            return;
        }

        _transitions.Request(actor, location);
        if (IsAccessBlocked(location))
        {
            view.Stop();
            return;
        }

        Vector3 destination = marker + _actorOffsets[actor];
        if (!view.IsNavigating && view.GlobalPosition.DistanceTo(destination) <= 0.01f)
        {
            return;
        }

        if (!view.IsNavigating || !view.Destination.IsEqualApprox(destination))
        {
            view.MoveTo(destination, immediate: false);
        }
    }

    public void SetLocationAccess(LocationId location, bool isAccessible)
    {
        if (!_locationMarkers.ContainsKey(location))
        {
            throw new InvalidOperationException($"Location marker '{location}' is not registered.");
        }

        if (!_restrictedLocations.Contains(location))
        {
            throw new InvalidOperationException($"Location '{location}' is not access-controlled.");
        }

        if (isAccessible)
        {
            _accessibleLocations.Add(location);
            ResumeRequestsFor(location);
        }
        else
        {
            _accessibleLocations.Remove(location);
        }
    }

    public bool IsInTransit(EntityId actor) =>
        _transitions.IsInTransit(actor);

    public LocationId GetRequestedLocation(EntityId actor) =>
        _transitions.GetRequestedLocation(actor);

    public void CancelMove(EntityId actor)
    {
        if (!_actorViews.TryGetValue(actor, out NpcActorNode? view))
        {
            throw new InvalidOperationException($"Actor view '{actor}' is not registered.");
        }

        view.Stop();
        _transitions.CancelRequest(actor);
    }

    public void SetMovementPaused(bool isPaused)
    {
        foreach (NpcActorNode view in _actorViews.Values)
        {
            view.SetMovementPaused(isPaused);
        }
    }

    public void SetMovementSpeed(float multiplier)
    {
        foreach (NpcActorNode view in _actorViews.Values)
        {
            view.SetSpeedMultiplier(multiplier);
        }
    }

    private bool IsAccessBlocked(LocationId location) =>
        _restrictedLocations.Contains(location) && !_accessibleLocations.Contains(location);

    private void ResumeRequestsFor(LocationId location)
    {
        foreach (EntityId actor in _actorViews.Keys)
        {
            LocationId requested = _transitions.GetRequestedLocation(actor);
            if (requested != location || !_actorViews.TryGetValue(actor, out NpcActorNode? view))
            {
                continue;
            }

            view.MoveTo(_locationMarkers[location] + _actorOffsets[actor], immediate: false);
        }
    }

    private void OnDestinationReached(NpcActorNode view, Vector3 destination)
    {
        EntityId actor = _viewActors[view];
        LocationId requested = _transitions.GetRequestedLocation(actor);

        Vector3 expected = _locationMarkers[requested] + _actorOffsets[actor];
        if (!destination.IsEqualApprox(expected))
        {
            return;
        }

        if (_transitions.Confirm(actor, requested))
        {
            LocationConfirmed?.Invoke(actor, requested);
        }
    }

    private void OnNavigationFailed(NpcActorNode view, Vector3 destination)
    {
        EntityId actor = _viewActors[view];
        LocationId requested = _transitions.GetRequestedLocation(actor);
        Vector3 expected = _locationMarkers[requested] + _actorOffsets[actor];
        if (destination.IsEqualApprox(expected))
        {
            NavigationFailed?.Invoke(actor, requested);
        }
    }
}
