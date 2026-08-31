using Game.Sim.Entities;
using Game.Sim.Events;
using Game.Sim.Locations;
using Game.Sim.Memory;
using Game.Sim.Perception;
using Game.Sim.Suspicion;
using Game.Sim.Time;
using Game.Sim.World;

namespace Game.Sim.Anomalies;

public sealed class RealityAnomalySystem
{
    private static readonly EventTag[] AnomalyTags = [
        EventTag.Pattern,
        EventTag.Suspicious,
        EventTag.Secret,
    ];

    private readonly WorldState _world;
    private readonly SimClock _clock;
    private readonly WorldEventFactory _events;
    private readonly IWorldEventBuffer _eventBuffer;
    private readonly MemorySystem _memories;
    private readonly SuspicionSystem _suspicion;

    public RealityAnomalySystem(
        WorldState world,
        SimClock clock,
        WorldEventFactory events,
        IWorldEventBuffer eventBuffer,
        MemorySystem memories,
        SuspicionSystem suspicion)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(eventBuffer);
        ArgumentNullException.ThrowIfNull(memories);
        ArgumentNullException.ThrowIfNull(suspicion);

        _world = world;
        _clock = clock;
        _events = events;
        _eventBuffer = eventBuffer;
        _memories = memories;
        _suspicion = suspicion;
    }

    public WorldEvent TriggerSaveReloadAnomaly(EntityId player, LocationId location)
    {
        WorldEvent anomalyEvent = _events.Create(
            player,
            EventType.RealityAnomaly,
            location,
            tags: AnomalyTags,
            payload: new RealityAnomalyPayload(
                AnomalyKind.SaveReload,
                "A temporal rift passed through the area. Events from moments ago feel strangely repeated (Déjà Vu).",
                player));

        _eventBuffer.Publish(anomalyEvent);

        // All other entities in the location perceive this unnatural temporal sensation
        foreach (EntityState other in _world.Entities.Where(e => e.Id != player && e.LogicalLocation == location))
        {
            MemoryRecord? memory = _memories.Remember(new Observation(
                id: new ObservationId(anomalyEvent.Id.Value),
                sourceEvent: anomalyEvent.Id,
                observer: other.Id,
                perceivedActor: player,
                perceivedType: EventType.RealityAnomaly,
                location: location,
                perceivedTags: AnomalyTags,
                time: _clock.Now,
                confidence: 1.0f,
                salience: 1.0f,
                channel: PerceptionChannel.Audio));

            if (memory is not null)
            {
                _ = _suspicion.ProcessMemory(other.Id, memory);
            }
        }

        return anomalyEvent;
    }

    public WorldEvent TriggerFastTravelAnomaly(EntityId actor, LocationId destination)
    {
        WorldEvent blinkEvent = _events.Create(
            actor,
            EventType.RealityAnomaly,
            destination,
            tags: AnomalyTags,
            payload: new RealityAnomalyPayload(
                AnomalyKind.TheBlink,
                $"{actor.Value} materialized abruptly out of thin air without traversing any portals.",
                actor));

        _eventBuffer.Publish(blinkEvent);

        foreach (EntityState witness in _world.Entities.Where(e => e.Id != actor && e.LogicalLocation == destination))
        {
            MemoryRecord? memory = _memories.Remember(new Observation(
                id: new ObservationId(blinkEvent.Id.Value),
                sourceEvent: blinkEvent.Id,
                observer: witness.Id,
                perceivedActor: actor,
                perceivedType: EventType.RealityAnomaly,
                location: destination,
                perceivedTags: AnomalyTags,
                time: _clock.Now,
                confidence: 1.0f,
                salience: 1.0f,
                channel: PerceptionChannel.Visual));

            if (memory is not null)
            {
                _ = _suspicion.ProcessMemory(witness.Id, memory);
            }
        }

        return blinkEvent;
    }
}
