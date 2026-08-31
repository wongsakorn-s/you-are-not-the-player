using Game.Sim.Actions;
using Game.Sim.Entities;
using Game.Sim.Events;
using Game.Sim.Locations;
using Game.Sim.Time;
using Game.Sim.World;

namespace Game.Sim.Tests.Actions;

public sealed class LiveMovementCoordinatorTests
{
    private static readonly EntityId George = new("george");
    private static readonly LocationId Lobby = new("lobby");
    private static readonly LocationId Hallway = new("hallway");
    private static readonly LocationId Kitchen = new("kitchen");
    private static readonly LocationId Room201 = new("room-201");
    private static readonly LocationId Basement = new("basement");

    [Fact]
    public void Complete_CommitsLogicalLocationAndEventsOnlyAfterArrival()
    {
        MovementFixture fixture = CreateFixture();

        MovementSnapshot requested = fixture.Coordinator.Request(
            new MoveEntityCommand(George, Kitchen));

        Assert.Equal(MovementStatus.Requested, requested.Status);
        Assert.Equal([Lobby, Hallway, Kitchen], requested.Route);
        Assert.Equal(Lobby, fixture.World.GetEntity(George).LogicalLocation);
        Assert.Equal(0, fixture.Buffer.Count);

        MovementSnapshot navigating = fixture.Coordinator.AcknowledgeNavigationStarted(
            requested.RequestId);
        Assert.Equal(MovementStatus.Navigating, navigating.Status);
        Assert.Equal(Lobby, fixture.World.GetEntity(George).LogicalLocation);

        MovementSnapshot completed = fixture.Coordinator.Complete(requested.RequestId);
        IReadOnlyList<WorldEvent> events = fixture.Buffer.Drain();

        Assert.Equal(MovementStatus.Completed, completed.Status);
        Assert.Equal(Kitchen, fixture.World.GetEntity(George).LogicalLocation);
        Assert.Equal([EventType.LeaveLocation, EventType.EnterLocation], events.Select(evt => evt.Type));
        Assert.Equal([Lobby, Kitchen], events.Select(evt => evt.Location));
    }

    [Fact]
    public void Request_RejectsRouteWhenRestrictedPortalIsClosed()
    {
        MovementFixture fixture = CreateFixture();

        MovementSnapshot result = fixture.Coordinator.Request(
            new MoveEntityCommand(George, Basement));

        Assert.Equal(MovementStatus.Failed, result.Status);
        Assert.Equal(MovementFailureReason.AccessDenied, result.FailureReason);
        Assert.Equal([Lobby, Hallway, Basement], result.Route);
        Assert.Equal(Lobby, fixture.World.GetEntity(George).LogicalLocation);
        Assert.Equal(0, fixture.Buffer.Count);
    }

    [Fact]
    public void Fail_PathUnavailableLeavesLogicalWorldUnchanged()
    {
        MovementFixture fixture = CreateFixture();
        MovementSnapshot request = fixture.Coordinator.Request(
            new MoveEntityCommand(George, Kitchen));
        _ = fixture.Coordinator.AcknowledgeNavigationStarted(request.RequestId);

        MovementSnapshot failed = fixture.Coordinator.Fail(
            request.RequestId,
            MovementFailureReason.PhysicalPathUnavailable);

        Assert.Equal(MovementStatus.Failed, failed.Status);
        Assert.Equal(MovementFailureReason.PhysicalPathUnavailable, failed.FailureReason);
        Assert.Equal(Lobby, fixture.World.GetEntity(George).LogicalLocation);
        Assert.Equal(0, fixture.Buffer.Count);
    }

    [Fact]
    public void Complete_FailsWhenDoorClosesDuringNavigation()
    {
        MovementFixture fixture = CreateFixture();
        fixture.Access.SetAccess("basement-door", isAccessible: true);
        MovementSnapshot request = fixture.Coordinator.Request(
            new MoveEntityCommand(George, Basement));
        _ = fixture.Coordinator.AcknowledgeNavigationStarted(request.RequestId);
        fixture.Access.SetAccess("basement-door", isAccessible: false);

        MovementSnapshot failed = fixture.Coordinator.Complete(request.RequestId);

        Assert.Equal(MovementStatus.Failed, failed.Status);
        Assert.Equal(MovementFailureReason.AccessDenied, failed.FailureReason);
        Assert.Equal(Lobby, fixture.World.GetEntity(George).LogicalLocation);
        Assert.Equal(0, fixture.Buffer.Count);
    }

    [Fact]
    public void Request_NewDestinationCancelsActiveMovementAndReplansFromWorldState()
    {
        MovementFixture fixture = CreateFixture();
        fixture.Access.SetAccess("basement-door", isAccessible: true);
        MovementSnapshot first = fixture.Coordinator.Request(
            new MoveEntityCommand(George, Kitchen));
        _ = fixture.Coordinator.AcknowledgeNavigationStarted(first.RequestId);

        MovementSnapshot replacement = fixture.Coordinator.Request(
            new MoveEntityCommand(George, Basement));

        Assert.Equal(MovementStatus.Cancelled, fixture.Coordinator.Get(first.RequestId).Status);
        Assert.Equal(MovementStatus.Requested, replacement.Status);
        Assert.Equal([Lobby, Hallway, Basement], replacement.Route);

        _ = fixture.Coordinator.AcknowledgeNavigationStarted(replacement.RequestId);
        MovementSnapshot completed = fixture.Coordinator.Complete(replacement.RequestId);

        Assert.Equal(MovementStatus.Completed, completed.Status);
        Assert.Equal(Basement, fixture.World.GetEntity(George).LogicalLocation);
    }

    [Fact]
    public void Cancel_IsTerminalAndCannotCommitLaterArrival()
    {
        MovementFixture fixture = CreateFixture();
        MovementSnapshot request = fixture.Coordinator.Request(
            new MoveEntityCommand(George, Kitchen));
        _ = fixture.Coordinator.AcknowledgeNavigationStarted(request.RequestId);

        MovementSnapshot cancelled = fixture.Coordinator.Cancel(request.RequestId);

        Assert.Equal(MovementStatus.Cancelled, cancelled.Status);
        Assert.Throws<InvalidOperationException>(
            () => fixture.Coordinator.Complete(request.RequestId));
        Assert.Equal(Lobby, fixture.World.GetEntity(George).LogicalLocation);
        Assert.Equal(0, fixture.Buffer.Count);
    }

    [Fact]
    public void Request_ReturnsRouteUnavailableForDisconnectedDestination()
    {
        MovementFixture fixture = CreateFixture();

        MovementSnapshot result = fixture.Coordinator.Request(
            new MoveEntityCommand(George, Room201));

        Assert.Equal(MovementStatus.Failed, result.Status);
        Assert.Equal(MovementFailureReason.RouteUnavailable, result.FailureReason);
    }

    private static MovementFixture CreateFixture()
    {
        var world = new WorldState();
        world.AddLocation(new LocationState(Lobby));
        world.AddLocation(new LocationState(Hallway));
        world.AddLocation(new LocationState(Kitchen));
        world.AddLocation(new LocationState(Room201));
        world.AddLocation(new LocationState(Basement, isRestricted: true));
        world.AddEntity(new EntityState(George, Lobby));

        var graph = new LocationGraph();
        graph.AddLocation(Lobby);
        graph.AddLocation(Hallway);
        graph.AddLocation(Kitchen);
        graph.AddLocation(Room201);
        graph.AddLocation(Basement);
        graph.ConnectBidirectional(Lobby, Hallway, "lobby-arch");
        graph.ConnectBidirectional(Hallway, Kitchen, "kitchen-door");
        graph.ConnectBidirectional(
            Hallway,
            Basement,
            "basement-door",
            requiresAccess: true);

        var clock = new SimClock();
        var buffer = new WorldEventBuffer();
        var eventFactory = new WorldEventFactory(clock, new SequentialEventIdGenerator());
        var movement = new MoveEntityActionHandler(world, eventFactory, buffer);
        var access = new PortalAccessPolicy();
        var coordinator = new LiveMovementCoordinator(world, graph, access, movement);
        return new MovementFixture(world, buffer, access, coordinator);
    }

    private sealed record MovementFixture(
        WorldState World,
        WorldEventBuffer Buffer,
        PortalAccessPolicy Access,
        LiveMovementCoordinator Coordinator);
}
