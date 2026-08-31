using Game.Sim.Entities;
using Game.Sim.Locations;
using Game.Sim.World;

namespace Game.Sim.Actions;

public sealed class LiveMovementCoordinator
{
    private readonly WorldState _world;
    private readonly LocationGraph _graph;
    private readonly ILocationAccessPolicy _access;
    private readonly MoveEntityActionHandler _movement;
    private readonly IMovementRequestIdGenerator _ids;
    private readonly Dictionary<MovementRequestId, MovementSession> _sessions = [];
    private readonly Dictionary<EntityId, MovementRequestId> _activeByActor = [];

    public LiveMovementCoordinator(
        WorldState world,
        LocationGraph graph,
        ILocationAccessPolicy access,
        MoveEntityActionHandler movement,
        IMovementRequestIdGenerator? ids = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(access);
        ArgumentNullException.ThrowIfNull(movement);
        _world = world;
        _graph = graph;
        _access = access;
        _movement = movement;
        _ids = ids ?? new SequentialMovementRequestIdGenerator();
    }

    public MovementSnapshot Request(MoveEntityCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        EntityState actor = _world.GetEntity(command.Actor);
        _ = _world.GetLocation(command.Destination);
        CancelActiveRequest(command.Actor);

        MovementRequestId id = _ids.NextId();
        LocationRoute? route = _graph.FindRoute(actor.LogicalLocation, command.Destination);
        var session = new MovementSession(
            id,
            command.Actor,
            actor.LogicalLocation,
            command.Destination,
            route);
        _sessions.Add(id, session);

        if (route is null)
        {
            session.Fail(MovementFailureReason.RouteUnavailable);
        }
        else if (!HasRouteAccess(session))
        {
            session.Fail(MovementFailureReason.AccessDenied);
        }
        else if (actor.LogicalLocation == command.Destination)
        {
            session.Complete();
        }
        else
        {
            _activeByActor.Add(command.Actor, id);
        }

        return session.Snapshot();
    }

    public MovementSnapshot AcknowledgeNavigationStarted(MovementRequestId requestId)
    {
        MovementSession session = GetActive(requestId);
        if (!HasRouteAccess(session))
        {
            session.Fail(MovementFailureReason.AccessDenied);
            _activeByActor.Remove(session.Actor);
        }
        else
        {
            session.Start();
        }

        return session.Snapshot();
    }

    public MovementSnapshot Complete(MovementRequestId requestId)
    {
        MovementSession session = GetActive(requestId);
        session.RequireStatus(MovementStatus.Navigating);
        if (_world.GetEntity(session.Actor).LogicalLocation != session.Origin)
        {
            session.Fail(MovementFailureReason.StaleWorldState);
        }
        else if (!HasRouteAccess(session))
        {
            session.Fail(MovementFailureReason.AccessDenied);
        }
        else
        {
            _ = _movement.Execute(new MoveEntityCommand(session.Actor, session.Destination));
            session.Complete();
        }

        _activeByActor.Remove(session.Actor);
        return session.Snapshot();
    }

    public MovementSnapshot Fail(
        MovementRequestId requestId,
        MovementFailureReason failureReason)
    {
        if (failureReason == MovementFailureReason.None)
        {
            throw new ArgumentException("A failed movement requires a failure reason.", nameof(failureReason));
        }

        MovementSession session = GetActive(requestId);
        session.Fail(failureReason);
        _activeByActor.Remove(session.Actor);
        return session.Snapshot();
    }

    public MovementSnapshot Cancel(MovementRequestId requestId)
    {
        MovementSession session = GetActive(requestId);
        session.Cancel();
        _activeByActor.Remove(session.Actor);
        return session.Snapshot();
    }

    public MovementSnapshot Get(MovementRequestId requestId) =>
        _sessions.TryGetValue(requestId, out MovementSession? session)
            ? session.Snapshot()
            : throw new KeyNotFoundException($"Movement request '{requestId}' does not exist.");

    public bool HasActiveRequest(EntityId actor) =>
        _activeByActor.ContainsKey(actor);

    public MovementSnapshot? GetActiveRequest(EntityId actor) =>
        _activeByActor.TryGetValue(actor, out MovementRequestId requestId) &&
        _sessions.TryGetValue(requestId, out MovementSession? session)
            ? session.Snapshot()
            : null;

    private bool HasRouteAccess(MovementSession session) =>
        session.Route is not null &&
        session.Route.Connections.All(connection => _access.CanTraverse(session.Actor, connection));

    private MovementSession GetActive(MovementRequestId requestId)
    {
        if (!_sessions.TryGetValue(requestId, out MovementSession? session))
        {
            throw new KeyNotFoundException($"Movement request '{requestId}' does not exist.");
        }

        session.RequireActive();
        return session;
    }

    private void CancelActiveRequest(EntityId actor)
    {
        if (!_activeByActor.Remove(actor, out MovementRequestId requestId))
        {
            return;
        }

        _sessions[requestId].Cancel();
    }

    private sealed class MovementSession
    {
        public MovementSession(
            MovementRequestId id,
            EntityId actor,
            LocationId origin,
            LocationId destination,
            LocationRoute? route)
        {
            Id = id;
            Actor = actor;
            Origin = origin;
            Destination = destination;
            Route = route;
        }

        public MovementRequestId Id { get; }

        public EntityId Actor { get; }

        public LocationId Origin { get; }

        public LocationId Destination { get; }

        public LocationRoute? Route { get; }

        public MovementStatus Status { get; private set; } = MovementStatus.Requested;

        public MovementFailureReason FailureReason { get; private set; }

        public void Start()
        {
            RequireStatus(MovementStatus.Requested);
            Status = MovementStatus.Navigating;
        }

        public void Complete()
        {
            if (Status is not (MovementStatus.Requested or MovementStatus.Navigating))
            {
                throw InvalidTransition(MovementStatus.Completed);
            }

            Status = MovementStatus.Completed;
        }

        public void Fail(MovementFailureReason reason)
        {
            RequireActive();
            FailureReason = reason;
            Status = MovementStatus.Failed;
        }

        public void Cancel()
        {
            RequireActive();
            Status = MovementStatus.Cancelled;
        }

        public void RequireActive()
        {
            if (Status is not (MovementStatus.Requested or MovementStatus.Navigating))
            {
                throw new InvalidOperationException(
                    $"Movement request '{Id}' is already terminal with status '{Status}'.");
            }
        }

        public void RequireStatus(MovementStatus expected)
        {
            if (Status != expected)
            {
                throw InvalidTransition(expected);
            }
        }

        public MovementSnapshot Snapshot() => new(
            Id,
            Actor,
            Origin,
            Destination,
            Route?.Locations ?? [Origin],
            Status,
            FailureReason);

        private InvalidOperationException InvalidTransition(MovementStatus expected) => new(
            $"Movement request '{Id}' cannot transition from '{Status}' to '{expected}'.");
    }
}
