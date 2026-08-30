using Game.Sim.Events;
using Game.Sim.Time;

namespace Game.Sim.Patterns;

public sealed class BehaviorPatternSystem
{
    private static readonly EventTag[] PatternTags = [
        EventTag.Visible,
        EventTag.Pattern,
    ];

    private readonly SimClock _clock;
    private readonly IBehaviorPatternDetector _detector;
    private readonly WorldEventFactory _events;
    private readonly IWorldEventBuffer _eventBuffer;

    public BehaviorPatternSystem(
        SimClock clock,
        IBehaviorPatternDetector detector,
        WorldEventFactory events,
        IWorldEventBuffer eventBuffer)
    {
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(detector);
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(eventBuffer);
        _clock = clock;
        _detector = detector;
        _events = events;
        _eventBuffer = eventBuffer;
    }

    public IReadOnlyList<WorldEvent> Process(WorldEvent sourceEvent)
    {
        ArgumentNullException.ThrowIfNull(sourceEvent);
        if (sourceEvent.Time != _clock.Now)
        {
            throw new ArgumentException(
                "Source event time must match the current simulation clock.",
                nameof(sourceEvent));
        }

        WorldEvent[] patternEvents = _detector
            .Process(sourceEvent)
            .Select(match => _events.Create(
                match.Actor,
                EventType.BehaviorPattern,
                match.Location,
                tags: PatternTags,
                payload: new BehaviorPatternPayload(match.Pattern, match.EvidenceEvents)))
            .ToArray();
        _eventBuffer.PublishBatch(patternEvents);
        return patternEvents;
    }
}
