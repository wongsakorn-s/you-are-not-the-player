using Game.Sim.Entities;
using Game.Sim.Locations;
using Game.Sim.Roles;
using Game.Sim.Suspicion;
using Game.Sim.Time;

namespace Game.Sim.Player;

public sealed class PlayerJournal
{
    public PlayerJournal(
        EntityId playerEntity,
        LocationId currentLocation,
        SimTime currentTime,
        IReadOnlyList<PlayerJournalEntry> entries,
        IReadOnlyList<SuspicionSnapshot> suspicionSnapshots,
        IReadOnlyList<EntityId> presentEntities,
        IReadOnlyList<LocationId> adjacentLocations,
        RoleId? role = null)
    {
        PlayerEntity = playerEntity;
        CurrentLocation = currentLocation;
        CurrentTime = currentTime;
        Entries = entries ?? [];
        SuspicionSnapshots = suspicionSnapshots ?? [];
        PresentEntities = presentEntities ?? [];
        AdjacentLocations = adjacentLocations ?? [];
        Role = role;
    }

    public EntityId PlayerEntity { get; }

    public RoleId? Role { get; }

    public LocationId CurrentLocation { get; }

    public SimTime CurrentTime { get; }

    public IReadOnlyList<PlayerJournalEntry> Entries { get; }

    public IReadOnlyList<SuspicionSnapshot> SuspicionSnapshots { get; }

    public IReadOnlyList<EntityId> PresentEntities { get; }

    public IReadOnlyList<LocationId> AdjacentLocations { get; }
}
