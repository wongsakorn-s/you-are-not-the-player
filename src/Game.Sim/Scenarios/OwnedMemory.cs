using Game.Sim.Entities;
using Game.Sim.Memory;

namespace Game.Sim.Scenarios;

public sealed record OwnedMemory(EntityId Owner, MemoryRecord Memory);
