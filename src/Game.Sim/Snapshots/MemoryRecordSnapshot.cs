namespace Game.Sim.Snapshots;

public sealed record MemoryRecordSnapshot(
    long Id,
    string Kind,
    string? Subject,
    string EventType,
    string? Location,
    string[] Tags,
    long EventTick,
    long CreatedAtTick,
    float InitialConfidence,
    float Salience,
    string? InformationSource,
    long RootEventId,
    long? SourceObservationId = null,
    long? SourceMemoryId = null,
    string? BehaviorPattern = null);
