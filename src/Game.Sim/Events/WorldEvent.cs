using Game.Sim.Entities;
using Game.Sim.Locations;
using Game.Sim.Time;

namespace Game.Sim.Events;

public sealed class WorldEvent
{
    public WorldEvent(
        EventId id,
        SimTime time,
        EntityId actor,
        EventType type,
        LocationId location,
        EntityId? target = null,
        IEnumerable<EventTag>? tags = null,
        EventPayload? payload = null)
    {
        if (id.IsEmpty)
        {
            throw new ArgumentException("Event ID cannot be empty.", nameof(id));
        }

        if (actor.IsEmpty)
        {
            throw new ArgumentException("Actor ID cannot be empty.", nameof(actor));
        }

        if (location.IsEmpty)
        {
            throw new ArgumentException("Location ID cannot be empty.", nameof(location));
        }

        if (target is { IsEmpty: true })
        {
            throw new ArgumentException("Target ID cannot be empty when supplied.", nameof(target));
        }

        if (!Enum.IsDefined(type))
        {
            throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown event type.");
        }

        EventTag[] materializedTags = tags?
            .Distinct()
            .Order()
            .ToArray() ?? [];
        if (materializedTags.Any(tag => !Enum.IsDefined(tag)))
        {
            throw new ArgumentOutOfRangeException(nameof(tags), "Event tags contain an unknown value.");
        }

        Id = id;
        Time = time;
        Actor = actor;
        Type = type;
        Target = target;
        Location = location;
        Tags = Array.AsReadOnly(materializedTags);
        Payload = payload ?? EmptyEventPayload.Instance;
    }

    public EventId Id { get; }

    public SimTime Time { get; }

    public EntityId Actor { get; }

    public EventType Type { get; }

    public EntityId? Target { get; }

    public LocationId Location { get; }

    public IReadOnlyList<EventTag> Tags { get; }

    public EventPayload Payload { get; }
}
