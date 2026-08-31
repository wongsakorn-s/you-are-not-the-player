using Game.Sim.Entities;
using Game.Sim.Locations;

namespace Game.Sim.Scenarios;

public sealed record ScenarioActorSnapshot(EntityId Entity, LocationId LogicalLocation);
