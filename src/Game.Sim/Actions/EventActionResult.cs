using Game.Sim.Events;

namespace Game.Sim.Actions;

public sealed record EventActionResult(
    WorldEvent SourceEvent,
    IReadOnlyList<WorldEvent> DerivedEvents);
