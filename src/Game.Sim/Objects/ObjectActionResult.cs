using Game.Sim.Events;

namespace Game.Sim.Objects;

public sealed record ObjectActionResult(
    bool Succeeded,
    string Message,
    string? DiscoveredClue = null,
    WorldEvent? GeneratedEvent = null);
