using Game.Sim.Conspiracy;
using Game.Sim.Scenarios;

namespace Game.Sim.Snapshots;

public static class SessionSnapshotValidator
{
    public static void Validate(SessionSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        SnapshotMetadata metadata = snapshot.Metadata
            ?? throw new InvalidDataException("Snapshot metadata is required.");
        IReadOnlyList<EntityStateSnapshot> entities = snapshot.Entities
            ?? throw new InvalidDataException("Snapshot entities are required.");
        IReadOnlyList<WorldEventSnapshot> events = snapshot.Events
            ?? throw new InvalidDataException("Snapshot events are required.");
        IReadOnlyList<EntityMemoryStoreSnapshot> memories = snapshot.Memories
            ?? throw new InvalidDataException("Snapshot memories are required.");
        IReadOnlyList<SuspicionCaseSnapshot> suspicions = snapshot.Suspicions
            ?? throw new InvalidDataException("Snapshot suspicions are required.");
        IReadOnlyList<MovementRequestSnapshot> movements = snapshot.PendingMovements
            ?? throw new InvalidDataException("Snapshot pending movements are required.");

        if (metadata.CurrentTick < 0)
        {
            throw new InvalidDataException("Snapshot current tick cannot be negative.");
        }

        if (metadata.MinimumTicks <= 0)
        {
            throw new InvalidDataException("Snapshot minimum ticks must be positive.");
        }

        RequireText(metadata.Scenario, "Snapshot scenario is required.");
        if (!Enum.TryParse(metadata.Phase, ignoreCase: true, out BasementSessionPhase _))
        {
            throw new InvalidDataException($"Snapshot phase '{metadata.Phase}' is invalid.");
        }

        if (entities.Count == 0)
        {
            throw new InvalidDataException("Snapshot must contain at least one entity.");
        }

        var entityIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (EntityStateSnapshot entity in entities)
        {
            RequireText(entity.EntityId, "Snapshot entity id is required.");
            RequireText(entity.LocationId, $"Snapshot location is required for entity '{entity.EntityId}'.");
            if (!entityIds.Add(entity.EntityId))
            {
                throw new InvalidDataException($"Snapshot contains duplicate entity '{entity.EntityId}'.");
            }
        }

        if (!string.IsNullOrWhiteSpace(metadata.ActivePlayerActor) &&
            !entityIds.Contains(metadata.ActivePlayerActor))
        {
            throw new InvalidDataException(
                $"Snapshot active player '{metadata.ActivePlayerActor}' does not exist in the entity list.");
        }

        EnsureUniquePositiveIds(events.Select(worldEvent => worldEvent.Id), "event");
        foreach (WorldEventSnapshot worldEvent in events)
        {
            if (worldEvent.Tick < 0)
            {
                throw new InvalidDataException($"Snapshot event '{worldEvent.Id}' has a negative tick.");
            }

            RequireKnownEntity(worldEvent.Actor, entityIds, $"event '{worldEvent.Id}' actor");
            RequireText(worldEvent.Location, $"Snapshot event '{worldEvent.Id}' location is required.");
            RequireText(worldEvent.Type, $"Snapshot event '{worldEvent.Id}' type is required.");
        }

        foreach (EntityMemoryStoreSnapshot store in memories)
        {
            RequireKnownEntity(store.Owner, entityIds, "memory store owner");
            if (store.Memories is null)
            {
                throw new InvalidDataException($"Snapshot memory store '{store.Owner}' has no memory collection.");
            }
        }

        foreach (SuspicionCaseSnapshot suspicion in suspicions)
        {
            RequireKnownEntity(suspicion.Observer, entityIds, "suspicion observer");
            RequireKnownEntity(suspicion.Subject, entityIds, "suspicion subject");
        }

        foreach (MovementRequestSnapshot movement in movements)
        {
            RequireKnownEntity(movement.Actor, entityIds, "movement actor");
            RequireText(movement.Origin, "Snapshot movement origin is required.");
            RequireText(movement.Destination, "Snapshot movement destination is required.");
        }

        ValidateObjects(snapshot.Objects);
        ValidateCoalition(snapshot.Coalition, entityIds);
        ValidateClimax(snapshot.ClimaxResolution);
    }

    private static void ValidateObjects(IReadOnlyList<InteractiveObjectSnapshot>? objects)
    {
        if (objects is null)
        {
            return;
        }

        var objectIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (InteractiveObjectSnapshot obj in objects)
        {
            RequireText(obj.Id, "Snapshot object id is required.");
            if (!objectIds.Add(obj.Id))
            {
                throw new InvalidDataException($"Snapshot contains duplicate object '{obj.Id}'.");
            }
        }
    }

    private static void ValidateCoalition(
        AccusationCoalitionSnapshot? coalition,
        IReadOnlySet<string> entityIds)
    {
        if (coalition is null)
        {
            return;
        }

        RequireKnownEntity(coalition.Initiator, entityIds, "coalition initiator");
        RequireKnownEntity(coalition.Target, entityIds, "coalition target");
        if (coalition.Members is null || coalition.EvidenceSummaries is null)
        {
            throw new InvalidDataException("Snapshot coalition collections are required.");
        }

        foreach (string member in coalition.Members)
        {
            RequireKnownEntity(member, entityIds, "coalition member");
        }

        if (!float.IsFinite(coalition.CombinedSuspicionScore) || coalition.CombinedSuspicionScore < 0f)
        {
            throw new InvalidDataException("Snapshot coalition score must be finite and non-negative.");
        }

        if (!Enum.TryParse(coalition.Stage, ignoreCase: true, out CoalitionStage _))
        {
            throw new InvalidDataException($"Snapshot coalition stage '{coalition.Stage}' is invalid.");
        }
    }

    private static void ValidateClimax(ClimaxResolutionSnapshot? climax)
    {
        if (climax is null)
        {
            return;
        }

        if (!Enum.TryParse(climax.Choice, ignoreCase: true, out PlayerClimaxChoice _))
        {
            throw new InvalidDataException($"Snapshot climax choice '{climax.Choice}' is invalid.");
        }

        RequireText(climax.Title, "Snapshot climax title is required.");
        RequireText(climax.NarrativeText, "Snapshot climax narrative is required.");
    }

    private static void EnsureUniquePositiveIds(IEnumerable<long> ids, string label)
    {
        var uniqueIds = new HashSet<long>();
        foreach (long id in ids)
        {
            if (id <= 0 || !uniqueIds.Add(id))
            {
                throw new InvalidDataException($"Snapshot contains invalid or duplicate {label} id '{id}'.");
            }
        }
    }

    private static void RequireKnownEntity(
        string entityId,
        IReadOnlySet<string> entityIds,
        string label)
    {
        RequireText(entityId, $"Snapshot {label} is required.");
        if (!entityIds.Contains(entityId))
        {
            throw new InvalidDataException($"Snapshot {label} '{entityId}' does not exist.");
        }
    }

    private static void RequireText(string? value, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidDataException(message);
        }
    }
}
