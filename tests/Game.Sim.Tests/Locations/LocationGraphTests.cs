using Game.Sim.Locations;

namespace Game.Sim.Tests.Locations;

public sealed class LocationGraphTests
{
    private static readonly LocationId Lobby = new("lobby");
    private static readonly LocationId Hallway = new("hallway");
    private static readonly LocationId Kitchen = new("kitchen");
    private static readonly LocationId Room201 = new("room-201");

    [Fact]
    public void FindRoute_ReturnsStableShortestMultiRoomPath()
    {
        var graph = new LocationGraph();
        graph.AddLocation(Lobby);
        graph.AddLocation(Hallway);
        graph.AddLocation(Kitchen);
        graph.AddLocation(Room201);
        graph.ConnectBidirectional(Lobby, Hallway, "lobby-arch");
        graph.ConnectBidirectional(Hallway, Kitchen, "kitchen-door");
        graph.ConnectBidirectional(Hallway, Room201, "room-201-door");

        LocationRoute? route = graph.FindRoute(Kitchen, Room201);

        Assert.NotNull(route);
        Assert.Equal([Kitchen, Hallway, Room201], route.Locations);
        Assert.Equal(
            ["kitchen-door", "room-201-door"],
            route.Connections.Select(connection => connection.PortalId));
    }

    [Fact]
    public void FindRoute_ReturnsNullForDisconnectedLocation()
    {
        var graph = new LocationGraph();
        graph.AddLocation(Lobby);
        graph.AddLocation(Room201);

        Assert.Null(graph.FindRoute(Lobby, Room201));
    }

    [Fact]
    public void ConnectBidirectional_RejectsDuplicateEdge()
    {
        var graph = new LocationGraph();
        graph.AddLocation(Lobby);
        graph.AddLocation(Hallway);
        graph.ConnectBidirectional(Lobby, Hallway, "lobby-arch");

        Assert.Throws<InvalidOperationException>(
            () => graph.ConnectBidirectional(Hallway, Lobby, "duplicate"));
    }
}
