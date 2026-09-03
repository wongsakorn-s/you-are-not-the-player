using Game.Sim.Actions;
using Game.Sim.Entities;
using Game.Sim.Events;
using Game.Sim.Locations;
using Game.Sim.Routines;
using Game.Sim.Time;
using Game.Sim.World;

namespace Game.Sim.Tests.Routines;

public sealed class CoordinatedNpcMovementExecutorTests
{
    private static readonly EntityId George = new("george");
    private static readonly LocationId Lobby = new("lobby");
    private static readonly LocationId Hallway = new("hallway");
    private static readonly LocationId Kitchen = new("kitchen");
    private static readonly LocationId Room201 = new("room-201");

    [Fact]
    public void Execute_PreemptingInFlightMoveWithCurrentLocationReleasesTheActor()
    {
        ExecutorFixture fixture = CreateFixture();
        NpcMovementExecution pending = fixture.Executor.Execute(
            new MoveEntityCommand(George, Kitchen));
        Assert.Equal(NpcMovementExecutionStatus.Pending, pending.Status);
        Assert.True(fixture.Executor.IsBusy(George));

        // The actor has not arrived yet, so their logical location is still the origin
        // and this second request resolves as NoMovement.
        NpcMovementExecution noMovement = fixture.Executor.Execute(
            new MoveEntityCommand(George, Lobby));

        Assert.Equal(NpcMovementExecutionStatus.NoMovement, noMovement.Status);
        Assert.False(fixture.Executor.IsBusy(George));
        Assert.Empty(fixture.Executor.PendingMovements);
        Assert.Null(fixture.Executor.GetPending(George));
    }

    [Fact]
    public void Execute_PreemptingInFlightMoveWithUnreachableDestinationReleasesTheActor()
    {
        ExecutorFixture fixture = CreateFixture();
        _ = fixture.Executor.Execute(new MoveEntityCommand(George, Kitchen));

        NpcMovementExecution failed = fixture.Executor.Execute(
            new MoveEntityCommand(George, Room201));

        Assert.Equal(NpcMovementExecutionStatus.Failed, failed.Status);
        Assert.False(fixture.Executor.IsBusy(George));
        Assert.Empty(fixture.Executor.PendingMovements);
        Assert.Null(fixture.Executor.GetPending(George));
    }

    [Fact]
    public void PendingMovements_NeverExposesACancelledRequest()
    {
        ExecutorFixture fixture = CreateFixture();
        MovementRequestId firstId = RequireRequestId(
            fixture.Executor.Execute(new MoveEntityCommand(George, Kitchen)));
        _ = fixture.Executor.Execute(new MoveEntityCommand(George, Room201));

        Assert.Equal(MovementStatus.Cancelled, fixture.Coordinator.Get(firstId).Status);
        Assert.DoesNotContain(
            fixture.Executor.PendingMovements,
            movement => movement.RequestId == firstId);
    }

    [Fact]
    public void Execute_PreemptingInFlightMoveTracksTheReplacementRequest()
    {
        ExecutorFixture fixture = CreateFixture();
        MovementRequestId firstId = RequireRequestId(
            fixture.Executor.Execute(new MoveEntityCommand(George, Kitchen)));

        NpcMovementExecution replacement = fixture.Executor.Execute(
            new MoveEntityCommand(George, Hallway));
        MovementRequestId replacementId = RequireRequestId(replacement);

        Assert.Equal(NpcMovementExecutionStatus.Pending, replacement.Status);
        Assert.True(fixture.Executor.IsBusy(George));
        Assert.NotEqual(firstId, replacementId);
        Assert.Equal(replacementId, fixture.Executor.GetPending(George)?.RequestId);

        MovementSnapshot completed = fixture.Executor.Complete(replacementId);

        Assert.Equal(MovementStatus.Completed, completed.Status);
        Assert.Equal(Hallway, fixture.World.GetEntity(George).LogicalLocation);
        Assert.False(fixture.Executor.IsBusy(George));
    }

    private static MovementRequestId RequireRequestId(NpcMovementExecution execution)
    {
        Assert.NotNull(execution.Movement);
        return execution.Movement.RequestId;
    }

    private static ExecutorFixture CreateFixture()
    {
        var world = new WorldState();
        world.AddLocation(new LocationState(Lobby));
        world.AddLocation(new LocationState(Hallway));
        world.AddLocation(new LocationState(Kitchen));
        world.AddLocation(new LocationState(Room201));
        world.AddEntity(new EntityState(George, Lobby));

        var graph = new LocationGraph();
        graph.AddLocation(Lobby);
        graph.AddLocation(Hallway);
        graph.AddLocation(Kitchen);
        graph.AddLocation(Room201);
        graph.ConnectBidirectional(Lobby, Hallway, "lobby-arch");
        graph.ConnectBidirectional(Hallway, Kitchen, "kitchen-door");

        var clock = new SimClock();
        var buffer = new WorldEventBuffer();
        var eventFactory = new WorldEventFactory(clock, new SequentialEventIdGenerator());
        var movement = new MoveEntityActionHandler(world, eventFactory, buffer);
        var coordinator = new LiveMovementCoordinator(
            world,
            graph,
            new PortalAccessPolicy(),
            movement);
        return new ExecutorFixture(
            world,
            coordinator,
            new CoordinatedNpcMovementExecutor(coordinator));
    }

    private sealed record ExecutorFixture(
        WorldState World,
        LiveMovementCoordinator Coordinator,
        CoordinatedNpcMovementExecutor Executor);
}
