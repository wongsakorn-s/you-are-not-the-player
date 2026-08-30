using Game.Sim.Entities;
using Game.Sim.Locations;
using Game.Sim.World;

namespace Game.Sim.Tests.World;

public sealed class WorldStateTests
{
    private static readonly LocationId Lobby = new("lobby");
    private static readonly LocationId Basement = new("basement");
    private static readonly EntityId Anna = new("anna");

    [Fact]
    public void AddEntity_StoresEntityInKnownLocation()
    {
        var world = new WorldState();
        world.AddLocation(new LocationState(Lobby));

        world.AddEntity(new EntityState(Anna, Lobby));

        Assert.Equal(Lobby, world.GetEntity(Anna).LogicalLocation);
    }

    [Fact]
    public void AddEntity_RejectsUnknownLocation()
    {
        var world = new WorldState();

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => world.AddEntity(new EntityState(Anna, Lobby)));

        Assert.Contains("unknown location", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DuplicateIdentifiers_AreRejected()
    {
        var world = new WorldState();
        world.AddLocation(new LocationState(Lobby));
        world.AddEntity(new EntityState(Anna, Lobby));

        Assert.Throws<InvalidOperationException>(() => world.AddLocation(new LocationState(Lobby)));
        Assert.Throws<InvalidOperationException>(() => world.AddEntity(new EntityState(Anna, Lobby)));
    }

    [Fact]
    public void Collections_AreReturnedInStableIdentifierOrder()
    {
        var world = new WorldState();
        var kitchen = new LocationId("kitchen");
        var bob = new EntityId("bob");
        world.AddLocation(new LocationState(kitchen));
        world.AddLocation(new LocationState(Basement));
        world.AddLocation(new LocationState(Lobby));
        world.AddEntity(new EntityState(bob, Lobby));
        world.AddEntity(new EntityState(Anna, Lobby));

        Assert.Equal([Basement, kitchen, Lobby], world.Locations.Select(location => location.Id));
        Assert.Equal([Anna, bob], world.Entities.Select(entity => entity.Id));
    }

    [Fact]
    public void ConnectLocations_CreatesSymmetricAudioConnection()
    {
        var world = new WorldState();
        world.AddLocation(new LocationState(Lobby));
        world.AddLocation(new LocationState(Basement));

        world.ConnectLocations(Lobby, Basement, audioTransmission: 0.6f);

        Assert.Equal(0.6f, world.GetAudioTransmission(Lobby, Basement));
        Assert.Equal(0.6f, world.GetAudioTransmission(Basement, Lobby));
        Assert.Equal(1.0f, world.GetAudioTransmission(Lobby, Lobby));
    }

    [Fact]
    public void ConnectLocations_RejectsDuplicateAndInvalidConnections()
    {
        var world = new WorldState();
        world.AddLocation(new LocationState(Lobby));
        world.AddLocation(new LocationState(Basement));
        world.ConnectLocations(Lobby, Basement);

        Assert.Throws<InvalidOperationException>(() => world.ConnectLocations(Basement, Lobby));
        Assert.Throws<ArgumentException>(() => world.ConnectLocations(Lobby, Lobby));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => world.ConnectLocations(Lobby, Basement, 1.1f));
        Assert.Throws<KeyNotFoundException>(
            () => world.GetAudioTransmission(Lobby, new LocationId("unknown")));
    }
}
