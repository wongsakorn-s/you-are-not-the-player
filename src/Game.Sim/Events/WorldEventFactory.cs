using Game.Sim.Entities;
using Game.Sim.Locations;
using Game.Sim.Time;

namespace Game.Sim.Events;

public sealed class WorldEventFactory
{
    private readonly SimClock _clock;
    private readonly IEventIdGenerator _ids;

    public WorldEventFactory(SimClock clock, IEventIdGenerator ids)
    {
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(ids);
        _clock = clock;
        _ids = ids;
    }

    public WorldEvent Create(
        EntityId actor,
        EventType type,
        LocationId location,
        EntityId? target = null,
        IEnumerable<EventTag>? tags = null,
        EventPayload? payload = null) =>
        new(
            _ids.NextId(),
            _clock.Now,
            actor,
            type,
            location,
            target,
            tags,
            payload);
}
