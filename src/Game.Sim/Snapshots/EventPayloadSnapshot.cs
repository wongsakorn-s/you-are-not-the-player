namespace Game.Sim.Snapshots;

public sealed record EventPayloadSnapshot(
    string Type,
    string? Origin = null,
    string? Destination = null,
    string? InteractionKind = null,
    string? InteractionId = null,
    string? BoundaryId = null,
    string? PlanId = null,
    string? DutyId = null,
    string? Pattern = null,
    long[]? EvidenceEvents = null,
    string? Subject = null,
    long? RootEventId = null,
    string? Anomaly = null,
    string? Description = null);
