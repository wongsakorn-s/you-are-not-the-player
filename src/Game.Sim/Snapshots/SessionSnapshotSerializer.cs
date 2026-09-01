using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Sim.Actions;
using Game.Sim.Anomalies;
using Game.Sim.Entities;
using Game.Sim.Events;
using Game.Sim.Locations;
using Game.Sim.Memory;
using Game.Sim.Perception;
using Game.Sim.Suspicion;
using Game.Sim.Time;

namespace Game.Sim.Snapshots;

public static class SessionSnapshotSerializer
{
    private static readonly JsonSerializerOptions IndentedJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static readonly JsonSerializerOptions CompactJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static string ToJson(SessionSnapshot snapshot, bool indented = true)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        SessionSnapshotValidator.Validate(snapshot);
        JsonSerializerOptions options = indented ? IndentedJsonOptions : CompactJsonOptions;
        return JsonSerializer.Serialize(snapshot, options);
    }

    public static SessionSnapshot FromJson(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        SessionSnapshot snapshot = JsonSerializer.Deserialize<SessionSnapshot>(json, IndentedJsonOptions)
            ?? throw new InvalidDataException("Failed to deserialize session snapshot.");
        SessionSnapshotValidator.Validate(snapshot);
        return snapshot;
    }

    public static void SaveToFile(SessionSnapshot snapshot, string filePath)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        SessionSnapshotValidator.Validate(snapshot);
        string fullPath = Path.GetFullPath(filePath);
        string directory = Path.GetDirectoryName(fullPath)
            ?? throw new InvalidOperationException("Snapshot path must have a parent directory.");
        _ = Directory.CreateDirectory(directory);
        string json = ToJson(snapshot, indented: true);
        string temporaryPath = Path.Combine(
            directory,
            $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");

        try
        {
            File.WriteAllText(temporaryPath, json);
            File.Move(temporaryPath, fullPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    public static SessionSnapshot LoadFromFile(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("Snapshot file was not found.", filePath);
        }

        string json = File.ReadAllText(filePath);
        return FromJson(json);
    }

    public static WorldEventSnapshot ConvertEventToSnapshot(WorldEvent worldEvent)
    {
        ArgumentNullException.ThrowIfNull(worldEvent);
        return new WorldEventSnapshot(
            Id: worldEvent.Id.Value,
            Tick: worldEvent.Time.Tick,
            Actor: worldEvent.Actor.Value,
            Type: worldEvent.Type.ToString(),
            Location: worldEvent.Location.Value,
            Target: worldEvent.Target?.Value,
            Tags: worldEvent.Tags.Select(tag => tag.ToString()).ToArray(),
            Payload: ConvertPayloadToSnapshot(worldEvent.Payload));
    }

    public static WorldEvent ConvertSnapshotToEvent(WorldEventSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return new WorldEvent(
            id: new EventId(snapshot.Id),
            time: new SimTime(snapshot.Tick),
            actor: new EntityId(snapshot.Actor),
            type: Enum.Parse<EventType>(snapshot.Type, ignoreCase: true),
            location: new LocationId(snapshot.Location),
            target: string.IsNullOrEmpty(snapshot.Target) ? null : new EntityId(snapshot.Target),
            tags: snapshot.Tags?.Select(tag => Enum.Parse<EventTag>(tag, ignoreCase: true)),
            payload: ConvertSnapshotToPayload(snapshot.Payload));
    }

    public static EventPayloadSnapshot ConvertPayloadToSnapshot(EventPayload payload) => payload switch
    {
        EmptyEventPayload => new EventPayloadSnapshot("empty"),
        LocationTransitionPayload transition => new EventPayloadSnapshot(
            Type: "locationTransition",
            Origin: transition.Origin.Value,
            Destination: transition.Destination.Value),
        SecretActivityPayload secret => new EventPayloadSnapshot(
            Type: "secretActivity",
            PlanId: secret.PlanId),
        InteractionPayload interaction => new EventPayloadSnapshot(
            Type: "interaction",
            InteractionKind: interaction.Kind.ToString(),
            InteractionId: interaction.InteractionId),
        RoleDutyPayload duty => new EventPayloadSnapshot(
            Type: "roleDuty",
            DutyId: duty.DutyId),
        BoundaryProbePayload probe => new EventPayloadSnapshot(
            Type: "boundaryProbe",
            BoundaryId: probe.BoundaryId),
        BehaviorPatternPayload pattern => new EventPayloadSnapshot(
            Type: "behaviorPattern",
            Pattern: pattern.Pattern.ToString(),
            EvidenceEvents: pattern.EvidenceEvents.Select(e => e.Value).ToArray()),
        InformationExchangePayload info => new EventPayloadSnapshot(
            Type: "informationExchange",
            Subject: info.Subject.Value,
            RootEventId: info.RootEventId.Value),
        RealityAnomalyPayload anomaly => new EventPayloadSnapshot(
            Type: "realityAnomaly",
            Anomaly: anomaly.Anomaly.ToString(),
            Description: anomaly.Description,
            Subject: anomaly.TargetActor?.Value),
        _ => new EventPayloadSnapshot("empty"),
    };

    public static EventPayload ConvertSnapshotToPayload(EventPayloadSnapshot snapshot)
    {
        if (snapshot is null || string.Equals(snapshot.Type, "empty", StringComparison.OrdinalIgnoreCase))
        {
            return EmptyEventPayload.Instance;
        }

        return snapshot.Type.ToLowerInvariant() switch
        {
            "locationtransition" => new LocationTransitionPayload(
                new LocationId(snapshot.Origin ?? throw new InvalidOperationException("Missing origin")),
                new LocationId(snapshot.Destination ?? throw new InvalidOperationException("Missing destination"))),
            "secretactivity" => new SecretActivityPayload(
                snapshot.PlanId ?? throw new InvalidOperationException("Missing planId")),
            "interaction" => new InteractionPayload(
                Enum.Parse<InteractionKind>(snapshot.InteractionKind ?? "Generic", ignoreCase: true),
                snapshot.InteractionId ?? throw new InvalidOperationException("Missing interactionId")),
            "roleduty" => new RoleDutyPayload(
                snapshot.DutyId ?? throw new InvalidOperationException("Missing dutyId")),
            "boundaryprobe" => new BoundaryProbePayload(
                snapshot.BoundaryId ?? throw new InvalidOperationException("Missing boundaryId")),
            "behaviorpattern" => new BehaviorPatternPayload(
                Enum.Parse<BehaviorPatternKind>(snapshot.Pattern ?? throw new InvalidOperationException("Missing pattern"), ignoreCase: true),
                snapshot.EvidenceEvents?.Select(id => new EventId(id)) ?? []),
            "informationexchange" => new InformationExchangePayload(
                new EntityId(snapshot.Subject ?? throw new InvalidOperationException("Missing subject")),
                new EventId(snapshot.RootEventId ?? throw new InvalidOperationException("Missing rootEventId"))),
            "realityanomaly" => new RealityAnomalyPayload(
                Enum.Parse<AnomalyKind>(snapshot.Anomaly ?? "SaveReload", ignoreCase: true),
                snapshot.Description ?? "Reality anomaly occurred",
                string.IsNullOrEmpty(snapshot.Subject) ? null : new EntityId(snapshot.Subject)),
            _ => EmptyEventPayload.Instance,
        };
    }

    public static MemoryRecordSnapshot ConvertMemoryToSnapshot(MemoryRecord memory)
    {
        ArgumentNullException.ThrowIfNull(memory);
        return new MemoryRecordSnapshot(
            Id: memory.Id.Value,
            Kind: memory.Kind.ToString(),
            Subject: memory.Subject?.Value,
            EventType: memory.EventType.ToString(),
            Location: memory.Location?.Value,
            Tags: memory.Tags.Select(tag => tag.ToString()).ToArray(),
            EventTick: memory.EventTime.Tick,
            CreatedAtTick: memory.CreatedAt.Tick,
            InitialConfidence: memory.InitialConfidence,
            Salience: memory.Salience,
            InformationSource: memory.InformationSource?.Value,
            RootEventId: memory.RootEventId.Value,
            SourceObservationId: memory.SourceObservationId?.Value,
            SourceMemoryId: memory.SourceMemoryId?.Value,
            BehaviorPattern: memory.BehaviorPattern?.ToString());
    }

    public static MemoryRecord ConvertSnapshotToMemory(MemoryRecordSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return MemoryRecord.Restore(
            id: new MemoryId(snapshot.Id),
            kind: Enum.Parse<MemoryKind>(snapshot.Kind, ignoreCase: true),
            subject: string.IsNullOrEmpty(snapshot.Subject) ? null : new EntityId(snapshot.Subject),
            eventType: Enum.Parse<EventType>(snapshot.EventType, ignoreCase: true),
            location: string.IsNullOrEmpty(snapshot.Location) ? null : new LocationId(snapshot.Location),
            tags: snapshot.Tags?.Select(tag => Enum.Parse<EventTag>(tag, ignoreCase: true)) ?? [],
            eventTime: new SimTime(snapshot.EventTick),
            createdAt: new SimTime(snapshot.CreatedAtTick),
            initialConfidence: snapshot.InitialConfidence,
            salience: snapshot.Salience,
            informationSource: string.IsNullOrEmpty(snapshot.InformationSource) ? null : new EntityId(snapshot.InformationSource),
            rootEventId: new EventId(snapshot.RootEventId),
            sourceObservationId: snapshot.SourceObservationId.HasValue ? new ObservationId(snapshot.SourceObservationId.Value) : null,
            sourceMemoryId: snapshot.SourceMemoryId.HasValue ? new MemoryId(snapshot.SourceMemoryId.Value) : null,
            behaviorPattern: !string.IsNullOrEmpty(snapshot.BehaviorPattern) ? Enum.Parse<BehaviorPatternKind>(snapshot.BehaviorPattern, ignoreCase: true) : null);
    }
}
