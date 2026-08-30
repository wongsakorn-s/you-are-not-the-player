using Game.Sim.Entities;
using Game.Sim.Events;
using Game.Sim.Locations;
using Game.Sim.Time;

namespace Game.Sim.Perception;

public sealed class Observation
{
    public Observation(
        ObservationId id,
        EventId sourceEvent,
        EntityId observer,
        EntityId? perceivedActor,
        EventType perceivedType,
        LocationId? location,
        SimTime time,
        float confidence,
        float salience,
        PerceptionChannel channel)
    {
        if (id.IsEmpty)
        {
            throw new ArgumentException("Observation ID cannot be empty.", nameof(id));
        }

        if (sourceEvent.IsEmpty)
        {
            throw new ArgumentException("Source event ID cannot be empty.", nameof(sourceEvent));
        }

        if (observer.IsEmpty)
        {
            throw new ArgumentException("Observer ID cannot be empty.", nameof(observer));
        }

        if (perceivedActor is { IsEmpty: true })
        {
            throw new ArgumentException(
                "Perceived actor ID cannot be empty when supplied.",
                nameof(perceivedActor));
        }

        if (location is { IsEmpty: true })
        {
            throw new ArgumentException("Location ID cannot be empty when supplied.", nameof(location));
        }

        if (!Enum.IsDefined(perceivedType))
        {
            throw new ArgumentOutOfRangeException(
                nameof(perceivedType),
                perceivedType,
                "Unknown perceived event type.");
        }

        ValidateUnitInterval(confidence, nameof(confidence));
        ValidateUnitInterval(salience, nameof(salience));

        if (!Enum.IsDefined(channel))
        {
            throw new ArgumentOutOfRangeException(nameof(channel), channel, "Unknown perception channel.");
        }

        Id = id;
        SourceEvent = sourceEvent;
        Observer = observer;
        PerceivedActor = perceivedActor;
        PerceivedType = perceivedType;
        Location = location;
        Time = time;
        Confidence = confidence;
        Salience = salience;
        Channel = channel;
    }

    public ObservationId Id { get; }

    public EventId SourceEvent { get; }

    public EntityId Observer { get; }

    public EntityId? PerceivedActor { get; }

    public EventType PerceivedType { get; }

    public LocationId? Location { get; }

    public SimTime Time { get; }

    public float Confidence { get; }

    public float Salience { get; }

    public PerceptionChannel Channel { get; }

    private static void ValidateUnitInterval(float value, string parameterName)
    {
        if (float.IsNaN(value) || value is < 0.0f or > 1.0f)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                "Value must be between 0 and 1 inclusive.");
        }
    }
}
