using Game.Client.Godot.World;
using Game.Sim.Entities;
using Game.Sim.Locations;
using Godot;

namespace Game.Client.Godot.Adapters;

public sealed class Godot2DWorldAdapter
{
    private readonly Dictionary<LocationId, Vector2> _locationMarkers = [];
    private readonly Dictionary<EntityId, CharacterToken2D> _actorViews = [];
    private readonly Dictionary<CharacterToken2D, EntityId> _viewActors = [];
    private readonly Dictionary<EntityId, Vector2> _actorOffsets = [];
    private readonly LocationTransitionTracker _transitions = new();

    public event Action<EntityId, LocationId>? LocationConfirmed;

    public void RegisterLocation(LocationId location, Vector2 screenPosition)
    {
        if (!_locationMarkers.TryAdd(location, screenPosition))
        {
            throw new InvalidOperationException($"Location '{location}' is already registered.");
        }
    }

    public void RegisterActor(EntityId actor, CharacterToken2D view, Vector2 offset)
    {
        ArgumentNullException.ThrowIfNull(view);
        if (!_actorViews.TryAdd(actor, view))
        {
            throw new InvalidOperationException($"Actor '{actor}' is already registered.");
        }

        _actorOffsets.Add(actor, offset);
        _viewActors.Add(view, actor);
        view.DestinationReached += OnDestinationReached;
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
        if (!_actorViews.TryGetValue(actor, out CharacterToken2D? view))
        {
            throw new InvalidOperationException($"Actor view '{actor}' is not registered.");
        }

        if (!_locationMarkers.TryGetValue(location, out Vector2 marker))
        {
            throw new InvalidOperationException($"Location marker '{location}' is not registered.");
        }

        Vector2 destination = marker + _actorOffsets[actor];
        if (immediate)
        {
            view.MoveTo(destination, immediate: true);
            _transitions.Initialize(actor, location);
            return;
        }

        _transitions.Request(actor, location);
        view.MoveTo(destination, immediate: false);
    }

    public void CancelMove(EntityId actor)
    {
        if (!_actorViews.TryGetValue(actor, out CharacterToken2D? view))
        {
            throw new InvalidOperationException($"Actor view '{actor}' is not registered.");
        }

        view.Stop();
        _transitions.CancelRequest(actor);
    }

    public void SetMovementPaused(bool isPaused)
    {
        foreach (CharacterToken2D view in _actorViews.Values)
        {
            view.SetMovementPaused(isPaused);
        }
    }

    public void SetMovementSpeed(float multiplier)
    {
        foreach (CharacterToken2D view in _actorViews.Values)
        {
            view.SetSpeedMultiplier(multiplier);
        }
    }

    private void OnDestinationReached(CharacterToken2D view, Vector2 destination)
    {
        EntityId actor = _viewActors[view];
        LocationId requested = _transitions.GetRequestedLocation(actor);
        Vector2 expected = _locationMarkers[requested] + _actorOffsets[actor];
        if (!destination.IsEqualApprox(expected))
        {
            return;
        }

        if (_transitions.Confirm(actor, requested))
        {
            LocationConfirmed?.Invoke(actor, requested);
        }
    }
}
