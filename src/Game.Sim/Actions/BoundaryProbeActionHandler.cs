using Game.Sim.Events;
using Game.Sim.Patterns;
using Game.Sim.World;

namespace Game.Sim.Actions;

public sealed class BoundaryProbeActionHandler
{
    private static readonly EventTag[] ProbeTags = [EventTag.Visible];

    private readonly WorldState _world;
    private readonly WorldEventFactory _events;
    private readonly IWorldEventBuffer _eventBuffer;
    private readonly BehaviorPatternSystem _patterns;

    public BoundaryProbeActionHandler(
        WorldState world,
        WorldEventFactory events,
        IWorldEventBuffer eventBuffer,
        BehaviorPatternSystem patterns)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(eventBuffer);
        ArgumentNullException.ThrowIfNull(patterns);
        _world = world;
        _events = events;
        _eventBuffer = eventBuffer;
        _patterns = patterns;
    }

    public EventActionResult Execute(BoundaryProbeCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        var actor = _world.GetEntity(command.Actor);
        WorldEvent sourceEvent = _events.Create(
            command.Actor,
            EventType.BoundaryProbe,
            actor.LogicalLocation,
            tags: ProbeTags,
            payload: new BoundaryProbePayload(command.BoundaryId));
        _eventBuffer.Publish(sourceEvent);
        IReadOnlyList<WorldEvent> derivedEvents = _patterns.Process(sourceEvent);
        return new EventActionResult(sourceEvent, derivedEvents);
    }
}
