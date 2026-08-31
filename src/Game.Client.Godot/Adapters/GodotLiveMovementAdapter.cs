using Game.Client.Godot.Configuration;
using Game.Sim.Actions;
using Game.Sim.Entities;
using Game.Sim.Events;
using Game.Sim.Locations;
using Game.Sim.Scenarios;
using Game.Sim.Time;
using Game.Sim.World;

namespace Game.Client.Godot.Adapters;

public sealed class GodotLiveMovementAdapter
{
    private readonly SimClock _clock = new(ticksPerSecond: 1);
    private readonly WorldState _world = new();
    private readonly WorldEventBuffer _buffer = new();
    private readonly PortalAccessPolicy _access = new();
    private readonly LiveMovementCoordinator _coordinator;
    private readonly Dictionary<EntityId, MovementRequestId> _activeRequests = [];
    private readonly Dictionary<EntityId, MovementSnapshot> _lastMovements = [];

    public GodotLiveMovementAdapter(
        BasementScenarioResult result,
        HotelWorldDefinition hotel,
        IEventIdGenerator eventIds)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(hotel);
        ArgumentNullException.ThrowIfNull(eventIds);

        foreach (HotelLocationDefinition location in hotel.Locations)
        {
            _world.AddLocation(new LocationState(
                new LocationId(location.Id),
                location.Restricted));
        }

        foreach (ScenarioActorSnapshot actor in result.Actors)
        {
            _world.AddEntity(new EntityState(actor.Entity, BasementScenario.Lobby));
        }

        var events = new WorldEventFactory(_clock, eventIds);
        var movement = new MoveEntityActionHandler(_world, events, _buffer);
        _coordinator = new LiveMovementCoordinator(
            _world,
            hotel.CreateLocationGraph(),
            _access,
            movement);
    }

    public IReadOnlyDictionary<EntityId, LocationId> LogicalLocations =>
        _world.Entities.ToDictionary(entity => entity.Id, entity => entity.LogicalLocation);

    public MovementSnapshot RequestMove(EntityId actor, LocationId destination, long tick)
    {
        AdvanceTo(tick);
        _activeRequests.Remove(actor);
        MovementSnapshot snapshot = _coordinator.Request(new MoveEntityCommand(actor, destination));
        if (snapshot.Status == MovementStatus.Requested)
        {
            snapshot = _coordinator.AcknowledgeNavigationStarted(snapshot.RequestId);
            if (snapshot.Status == MovementStatus.Navigating)
            {
                _activeRequests[actor] = snapshot.RequestId;
            }
        }

        _lastMovements[actor] = snapshot;
        return snapshot;
    }

    public IReadOnlyList<WorldEvent> CompleteMove(
        EntityId actor,
        LocationId destination,
        long tick)
    {
        AdvanceTo(tick);
        if (!_activeRequests.TryGetValue(actor, out MovementRequestId requestId))
        {
            return [];
        }

        MovementSnapshot active = _coordinator.Get(requestId);
        if (active.Destination != destination)
        {
            return [];
        }

        MovementSnapshot completed = _coordinator.Complete(requestId);
        _activeRequests.Remove(actor);
        _lastMovements[actor] = completed;
        return _buffer.Drain();
    }

    public void FailMove(EntityId actor, LocationId destination, long tick)
    {
        AdvanceTo(tick);
        if (!_activeRequests.TryGetValue(actor, out MovementRequestId requestId))
        {
            return;
        }

        MovementSnapshot active = _coordinator.Get(requestId);
        if (active.Destination != destination)
        {
            return;
        }

        MovementSnapshot failed = _coordinator.Fail(
            requestId,
            MovementFailureReason.PhysicalPathUnavailable);
        _activeRequests.Remove(actor);
        _lastMovements[actor] = failed;
    }

    public void SetPortalAccess(string portalId, bool isAccessible) =>
        _access.SetAccess(portalId, isAccessible);

    public MovementSnapshot? GetLastMovement(EntityId actor) =>
        _lastMovements.TryGetValue(actor, out MovementSnapshot? movement)
            ? movement
            : null;

    public LocationId GetLogicalLocation(EntityId actor) =>
        _world.GetEntity(actor).LogicalLocation;

    private void AdvanceTo(long tick)
    {
        if (tick < _clock.Now.Tick)
        {
            throw new ArgumentOutOfRangeException(
                nameof(tick),
                tick,
                "Movement acknowledgement tick cannot move backwards.");
        }

        if (tick > _clock.Now.Tick)
        {
            _clock.Advance(new SimDelta(tick - _clock.Now.Tick));
        }
    }
}
