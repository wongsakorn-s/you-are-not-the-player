using Game.Sim.Events;
using Game.Sim.Patterns;
using Game.Sim.World;

namespace Game.Sim.Actions;

public sealed class InteractionActionHandler
{
    private static readonly EventTag[] VisibleTags = [EventTag.Visible];
    private static readonly EventTag[] DialogueTags = [EventTag.Visible, EventTag.Audible];

    private readonly WorldState _world;
    private readonly WorldEventFactory _events;
    private readonly IWorldEventBuffer _eventBuffer;
    private readonly BehaviorPatternSystem _patterns;

    public InteractionActionHandler(
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

    public EventActionResult Execute(InteractionCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        var actor = _world.GetEntity(command.Actor);
        EventTag[] tags = command.Kind == InteractionKind.Dialogue
            ? DialogueTags
            : VisibleTags;
        WorldEvent sourceEvent = _events.Create(
            command.Actor,
            EventType.Interaction,
            actor.LogicalLocation,
            tags: tags,
            payload: new InteractionPayload(command.Kind, command.InteractionId));
        _eventBuffer.Publish(sourceEvent);
        IReadOnlyList<WorldEvent> derivedEvents = _patterns.Process(sourceEvent);
        return new EventActionResult(sourceEvent, derivedEvents);
    }
}
