using Game.Sim.Entities;
using Game.Sim.Events;
using Game.Sim.Locations;
using Game.Sim.World;

namespace Game.Sim.Perception;

public sealed class LogicalPerceptionResolver : IPerceptionResolver
{
    private const float SameLocationVisualConfidence = 0.95f;
    private const float SameLocationAudioConfidence = 0.75f;
    private const float AdjacentAudioConfidence = 0.40f;
    private const float NormalSalience = 0.50f;
    private const float RestrictedSalience = 0.90f;

    private readonly IObservationIdGenerator _ids;

    public LogicalPerceptionResolver(IObservationIdGenerator ids)
    {
        ArgumentNullException.ThrowIfNull(ids);
        _ids = ids;
    }

    public IReadOnlyList<Observation> Observe(
        EntityState observer,
        WorldEvent worldEvent,
        WorldState world)
    {
        ArgumentNullException.ThrowIfNull(observer);
        ArgumentNullException.ThrowIfNull(worldEvent);
        ArgumentNullException.ThrowIfNull(world);

        EntityState registeredObserver = world.GetEntity(observer.Id);
        if (!ReferenceEquals(observer, registeredObserver))
        {
            throw new ArgumentException(
                "Observer must be the entity instance registered in the supplied world.",
                nameof(observer));
        }

        if (observer.Id == worldEvent.Actor)
        {
            return [];
        }

        float salience = worldEvent.Tags.Contains(EventTag.Restricted)
            ? RestrictedSalience
            : NormalSalience;

        if (observer.LogicalLocation == worldEvent.Location)
        {
            if (worldEvent.Tags.Contains(EventTag.Visible))
            {
                return [CreateObservation(
                    observer.Id,
                    worldEvent,
                    worldEvent.Actor,
                    SameLocationVisualConfidence,
                    salience,
                    PerceptionChannel.Visual)];
            }

            if (worldEvent.Tags.Contains(EventTag.Audible))
            {
                return [CreateObservation(
                    observer.Id,
                    worldEvent,
                    perceivedActor: null,
                    SameLocationAudioConfidence,
                    salience,
                    PerceptionChannel.Audio)];
            }

            return [];
        }

        if (!worldEvent.Tags.Contains(EventTag.Audible))
        {
            return [];
        }

        float? transmission = world.GetAudioTransmission(
            observer.LogicalLocation,
            worldEvent.Location);
        if (transmission is null or <= 0.0f)
        {
            return [];
        }

        return [CreateObservation(
            observer.Id,
            worldEvent,
            perceivedActor: null,
            AdjacentAudioConfidence * transmission.Value,
            salience,
            PerceptionChannel.Audio)];
    }

    private Observation CreateObservation(
        EntityId observer,
        WorldEvent worldEvent,
        EntityId? perceivedActor,
        float confidence,
        float salience,
        PerceptionChannel channel) =>
        new(
            _ids.NextId(),
            worldEvent.Id,
            observer,
            perceivedActor,
            worldEvent.Type,
            worldEvent.Location,
            confidence,
            salience,
            channel);
}
