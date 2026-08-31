using Game.Sim.Entities;
using Game.Sim.Events;
using Game.Sim.Memory;
using Game.Sim.Patterns;
using Game.Sim.Perception;
using Game.Sim.Time;
using Game.Sim.World;

namespace Game.Sim.Objects;

public sealed class ObjectActionHandler
{
    private static readonly EventTag[] StandardTags = [EventTag.Visible];
    private static readonly EventTag[] SuspiciousTags = [EventTag.Visible, EventTag.Suspicious];

    private readonly WorldState _world;
    private readonly HotelObjectRegistry _objects;
    private readonly WorldEventFactory _events;
    private readonly IWorldEventBuffer _eventBuffer;
    private readonly BehaviorPatternSystem _patterns;
    private readonly MemorySystem _memories;

    public ObjectActionHandler(
        WorldState world,
        HotelObjectRegistry objects,
        WorldEventFactory events,
        IWorldEventBuffer eventBuffer,
        BehaviorPatternSystem patterns,
        MemorySystem memories)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(objects);
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(eventBuffer);
        ArgumentNullException.ThrowIfNull(patterns);
        ArgumentNullException.ThrowIfNull(memories);

        _world = world;
        _objects = objects;
        _events = events;
        _eventBuffer = eventBuffer;
        _patterns = patterns;
        _memories = memories;
    }

    public ObjectActionResult Inspect(EntityId actor, string objectId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(objectId);

        EntityState entityState = _world.GetEntity(actor);
        InteractiveObject? obj = _objects.GetObject(objectId);
        if (obj is null)
        {
            return new ObjectActionResult(
                Succeeded: false,
                Message: $"Object '{objectId}' was not found in the hotel.");
        }

        if (obj.Location != entityState.LogicalLocation)
        {
            return new ObjectActionResult(
                Succeeded: false,
                Message: $"Cannot inspect '{obj.DisplayName}' because you are at '{entityState.LogicalLocation.Value}', but the object is at '{obj.Location.Value}'.");
        }

        if (obj.IsLocked)
        {
            return new ObjectActionResult(
                Succeeded: false,
                Message: $"{obj.DisplayName} is securely locked. (Requires: {obj.RequiredKeyId ?? "key"})");
        }

        EventTag[] tags = obj.IsSuspiciousToTamper ? SuspiciousTags : StandardTags;
        WorldEvent worldEvent = _events.Create(
            actor,
            EventType.Interaction,
            entityState.LogicalLocation,
            tags: tags,
            payload: new InteractionPayload(InteractionKind.Generic, obj.Id));

        _eventBuffer.Publish(worldEvent);
        _ = _patterns.Process(worldEvent);

        // Record episodic memory of inspecting the object
        _ = _memories.Remember(new Observation(
            id: new ObservationId(worldEvent.Id.Value),
            sourceEvent: worldEvent.Id,
            observer: actor,
            perceivedActor: actor,
            perceivedType: EventType.Interaction,
            location: entityState.LogicalLocation,
            perceivedTags: tags,
            time: worldEvent.Time,
            confidence: 1.0f,
            salience: 0.5f,
            channel: PerceptionChannel.Visual));

        return new ObjectActionResult(
            Succeeded: true,
            Message: $"You inspect {obj.DisplayName}. {obj.ClueDescription}",
            DiscoveredClue: obj.ClueDescription,
            GeneratedEvent: worldEvent);
    }

    public ObjectActionResult TamperOrUnlock(EntityId actor, string objectId, string? keyId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(objectId);

        EntityState entityState = _world.GetEntity(actor);
        InteractiveObject? obj = _objects.GetObject(objectId);
        if (obj is null)
        {
            return new ObjectActionResult(
                Succeeded: false,
                Message: $"Object '{objectId}' was not found in the hotel.");
        }

        if (obj.Location != entityState.LogicalLocation)
        {
            return new ObjectActionResult(
                Succeeded: false,
                Message: $"Cannot tamper with '{obj.DisplayName}' from a different location.");
        }

        if (obj.IsLocked)
        {
            bool unlocked = obj.Unlock(keyId);
            if (!unlocked)
            {
                return new ObjectActionResult(
                    Succeeded: false,
                    Message: $"Failed to unlock {obj.DisplayName}. Incorrect key or lock mechanism.");
            }
        }

        obj.MarkTampered();

        WorldEvent worldEvent = _events.Create(
            actor,
            EventType.Interaction,
            entityState.LogicalLocation,
            tags: SuspiciousTags,
            payload: new InteractionPayload(InteractionKind.Generic, obj.Id));

        _eventBuffer.Publish(worldEvent);
        _ = _patterns.Process(worldEvent);

        _ = _memories.Remember(new Observation(
            id: new ObservationId(worldEvent.Id.Value),
            sourceEvent: worldEvent.Id,
            observer: actor,
            perceivedActor: actor,
            perceivedType: EventType.Interaction,
            location: entityState.LogicalLocation,
            perceivedTags: SuspiciousTags,
            time: worldEvent.Time,
            confidence: 1.0f,
            salience: 0.8f,
            channel: PerceptionChannel.Visual));

        return new ObjectActionResult(
            Succeeded: true,
            Message: $"Successfully unlocked and tampered with {obj.DisplayName}. {obj.ClueDescription}",
            DiscoveredClue: obj.ClueDescription,
            GeneratedEvent: worldEvent);
    }
}
