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
    // One tag set carrying both Pattern and Suspicious made every anomaly match
    // both suspicion rules at once, so a single sighting scored 65 impossible
    // behaviour and 50 meta behaviour - roughly double what either rule was
    // written to award, and enough to send the whole cast into a following and
    // gossiping spiral off one event. The rules are alternatives; the tags now
    // say which one an anomaly is.
    private static readonly EventTag[] RepeatedRealityTags = [
        EventTag.Pattern,
        EventTag.Secret,
    ];

    private static readonly EventTag[] ImpossibleMovementTags = [
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
            tags: RepeatedRealityTags,
            payload: new RealityAnomalyPayload(
                AnomalyKind.SaveReload,
                "A temporal rift passed through the area. Events from moments ago feel strangely repeated (Déjà Vu).",
                player));

        _eventBuffer.Publish(anomalyEvent);
        RememberForWitnesses(anomalyEvent, player, location, PerceptionChannel.Audio);
        return anomalyEvent;
    }

    /// <summary>
    /// Somebody has no memory of a conversation that just happened. To the person
    /// they were talking to it is the world skipping, not a lapse of attention.
    /// </summary>
    public WorldEvent TriggerDialogueResetAnomaly(EntityId subject, LocationId location)
    {
        WorldEvent resetEvent = _events.Create(
            subject,
            EventType.RealityAnomaly,
            location,
            tags: RepeatedRealityTags,
            payload: new RealityAnomalyPayload(
                AnomalyKind.DialogueReset,
                $"{subject.Value} repeated a greeting word for word, with no memory of having just said it.",
                subject));

        _eventBuffer.Publish(resetEvent);
        RememberForWitnesses(resetEvent, subject, location, PerceptionChannel.Visual);
        return resetEvent;
    }

    private void RememberForWitnesses(
        WorldEvent anomalyEvent,
        EntityId subject,
        LocationId location,
        PerceptionChannel channel)
    {
        foreach (EntityState witness in _world.Entities
            .Where(entity => entity.Id != subject && entity.LogicalLocation == location))
        {
            MemoryRecord? memory = _memories.Remember(new Observation(
                id: new ObservationId(anomalyEvent.Id.Value),
                sourceEvent: anomalyEvent.Id,
                observer: witness.Id,
                perceivedActor: subject,
                perceivedType: EventType.RealityAnomaly,
                location: location,
                perceivedTags: [.. anomalyEvent.Tags],
                time: _clock.Now,
                confidence: 1.0f,
                salience: 1.0f,
                channel: channel));

            if (memory is not null)
            {
                _ = _suspicion.ProcessMemory(witness.Id, memory);
            }
        }
    }

    public WorldEvent TriggerFastTravelAnomaly(EntityId actor, LocationId destination)
    {
        WorldEvent blinkEvent = _events.Create(
            actor,
            EventType.RealityAnomaly,
            destination,
            tags: ImpossibleMovementTags,
            payload: new RealityAnomalyPayload(
                AnomalyKind.TheBlink,
                $"{actor.Value} materialized abruptly out of thin air without traversing any portals.",
                actor));

        _eventBuffer.Publish(blinkEvent);
        RememberForWitnesses(blinkEvent, actor, destination, PerceptionChannel.Visual);
        return blinkEvent;
    }
}
