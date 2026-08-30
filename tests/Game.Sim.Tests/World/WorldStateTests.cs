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
    public void MoveEntity_ChangesLogicalLocation()
    {
        var world = new WorldState();
        world.AddLocation(new LocationState(Lobby));
        world.AddLocation(new LocationState(Basement));
        world.AddEntity(new EntityState(Anna, Lobby));

        world.MoveEntity(Anna, Basement);

        Assert.Equal(Basement, world.GetEntity(Anna).LogicalLocation);
    }

    [Fact]
    public void MoveEntity_RejectsUnknownDestination()
    {
        var world = new WorldState();
        world.AddLocation(new LocationState(Lobby));
        world.AddEntity(new EntityState(Anna, Lobby));

        Assert.Throws<KeyNotFoundException>(() => world.MoveEntity(Anna, Basement));
        Assert.Equal(Lobby, world.GetEntity(Anna).LogicalLocation);
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
}
