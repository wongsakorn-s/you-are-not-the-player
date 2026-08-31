using Game.Client.Godot.Configuration;
using Game.Sim.Locations;

namespace Game.Sim.Tests.Configuration;

public sealed class HotelWorldDefinitionParserTests
{
    [Fact]
    public void ProductionDefinition_ContainsFiveRoomConnectedHotelGraph()
    {
        string path = Path.Combine(
            AppContext.BaseDirectory,
            "Data",
            "Hotel",
            "hotel-world.json");

        HotelWorldDefinition hotel = HotelWorldDefinitionParser.Parse(File.ReadAllText(path));
        LocationRoute? route = hotel.CreateLocationGraph().FindRoute(
            new LocationId("kitchen"),
            new LocationId("basement"));

        Assert.Equal(5, hotel.Locations.Length);
        Assert.NotNull(route);
        Assert.Equal(
            ["kitchen", "hallway", "basement"],
            route.Locations.Select(location => location.Value));
        Assert.True(route.Connections[^1].RequiresAccess);
        Assert.Equal("basement-door", route.Connections[^1].PortalId);
    }

    [Fact]
    public void Parse_RejectsPortalThatReferencesUnknownLocation()
    {
        const string InvalidJson = """
            {
              "schemaVersion": 1,
              "navigation": {
                "minimumX": -1,
                "maximumX": 1,
                "minimumZ": -1,
                "maximumZ": 1,
                "height": 0.8
              },
              "locations": [
                {
                  "id": "lobby",
                  "displayName": "Lobby",
                  "marker": { "x": 0, "y": 0.8, "z": 0 },
                  "floorPosition": { "x": 0, "y": 0, "z": 0 },
                  "floorSize": { "x": 1, "y": 1, "z": 1 },
                  "color": "ffffff",
                  "restricted": false
                }
              ],
              "portals": [
                {
                  "id": "broken",
                  "from": "lobby",
                  "to": "missing",
                  "requiresAccess": false
                }
              ]
            }
            """;

        FormatException error = Assert.Throws<FormatException>(
            () => HotelWorldDefinitionParser.Parse(InvalidJson));

        Assert.Contains("unknown location", error.Message, StringComparison.OrdinalIgnoreCase);
    }
}
