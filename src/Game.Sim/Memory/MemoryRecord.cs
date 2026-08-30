using Game.Sim.Entities;
using Game.Sim.Events;
using Game.Sim.Locations;
using Game.Sim.Perception;
using Game.Sim.Time;

namespace Game.Sim.Memory;

public sealed class MemoryRecord
{
    private MemoryRecord(
        MemoryId id,
        MemoryKind kind,
        EntityId? subject,
        EventType eventType,
        LocationId? location,
        SimTime eventTime,
        SimTime createdAt,
        float initialConfidence,
        float salience,
        EntityId? informationSource,
        EventId rootEventId,
        ObservationId? sourceObservationId,
        MemoryId? sourceMemoryId)
    {
        Id = id;
        Kind = kind;
        Subject = subject;
        EventType = eventType;
        Location = location;
        EventTime = eventTime;
        CreatedAt = createdAt;
        InitialConfidence = initialConfidence;
        Salience = salience;
        InformationSource = informationSource;
        RootEventId = rootEventId;
        SourceObservationId = sourceObservationId;
        SourceMemoryId = sourceMemoryId;
    }

    public MemoryId Id { get; }

    public MemoryKind Kind { get; }

    public EntityId? Subject { get; }

    public EventType EventType { get; }

    public LocationId? Location { get; }

    public SimTime EventTime { get; }

    public SimTime CreatedAt { get; }

    public float InitialConfidence { get; }

    public float Salience { get; }

    public EntityId? InformationSource { get; }

    public EventId RootEventId { get; }

    public ObservationId? SourceObservationId { get; }

    public MemoryId? SourceMemoryId { get; }

    public static MemoryRecord FromObservation(MemoryId id, Observation observation)
    {
        ValidateId(id);
        ArgumentNullException.ThrowIfNull(observation);

        return new MemoryRecord(
            id,
            MemoryKind.Episodic,
            observation.PerceivedActor,
            observation.PerceivedType,
            observation.Location,
            observation.Time,
            observation.Time,
            observation.Confidence,
            observation.Salience,
            informationSource: null,
            observation.SourceEvent,
            observation.Id,
            sourceMemoryId: null);
    }

    public static MemoryRecord FromSharedMemory(
        MemoryId id,
        MemoryRecord sourceMemory,
        EntityId informationSource,
        SimTime createdAt,
        float initialConfidence)
    {
        ValidateId(id);
        ArgumentNullException.ThrowIfNull(sourceMemory);

        if (informationSource.IsEmpty)
        {
            throw new ArgumentException("Information source cannot be empty.", nameof(informationSource));
        }

        ValidateUnitInterval(initialConfidence, nameof(initialConfidence));

        if (createdAt < sourceMemory.CreatedAt)
        {
            throw new ArgumentOutOfRangeException(
                nameof(createdAt),
                createdAt,
                "A social memory cannot be created before the source memory existed.");
        }

        return new MemoryRecord(
            id,
            MemoryKind.Social,
            sourceMemory.Subject,
            sourceMemory.EventType,
            sourceMemory.Location,
            sourceMemory.EventTime,
            createdAt,
            initialConfidence,
            sourceMemory.Salience,
            informationSource,
            sourceMemory.RootEventId,
            sourceObservationId: null,
            sourceMemory.Id);
    }

    private static void ValidateId(MemoryId id)
    {
        if (id.IsEmpty)
        {
            throw new ArgumentException("Memory ID cannot be empty.", nameof(id));
        }
    }

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
