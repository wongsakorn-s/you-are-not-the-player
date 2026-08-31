using Game.Sim.Entities;
using Game.Sim.Locations;

namespace Game.Sim.Actions;

public sealed record MovementSnapshot(
    MovementRequestId RequestId,
    EntityId Actor,
    LocationId Origin,
    LocationId Destination,
    IReadOnlyList<LocationId> Route,
    MovementStatus Status,
    MovementFailureReason FailureReason);
