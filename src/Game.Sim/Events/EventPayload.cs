using Game.Sim.Locations;

namespace Game.Sim.Events;

public abstract record EventPayload
{
    private protected EventPayload()
    {
    }
}

public sealed record EmptyEventPayload : EventPayload
{
    public static EmptyEventPayload Instance { get; } = new();

    private EmptyEventPayload()
    {
    }
}

public sealed record LocationTransitionPayload : EventPayload
{
    public LocationTransitionPayload(LocationId origin, LocationId destination)
    {
        if (origin.IsEmpty)
        {
            throw new ArgumentException("Origin location cannot be empty.", nameof(origin));
        }

        if (destination.IsEmpty)
        {
            throw new ArgumentException("Destination location cannot be empty.", nameof(destination));
        }

        Origin = origin;
        Destination = destination;
    }

    public LocationId Origin { get; }

    public LocationId Destination { get; }
}

public sealed record SecretActivityPayload : EventPayload
{
    public SecretActivityPayload(string planId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(planId);
        PlanId = planId.Trim();
    }

    public string PlanId { get; }
}

public sealed record InteractionPayload : EventPayload
{
    public InteractionPayload(InteractionKind kind, string interactionId)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown interaction kind.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(interactionId);
        Kind = kind;
        InteractionId = interactionId.Trim();
    }

    public InteractionKind Kind { get; }

    public string InteractionId { get; }
}

public sealed record RoleDutyPayload : EventPayload
{
    public RoleDutyPayload(string dutyId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dutyId);
        DutyId = dutyId.Trim();
    }

    public string DutyId { get; }
}

public sealed record BoundaryProbePayload : EventPayload
{
    public BoundaryProbePayload(string boundaryId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(boundaryId);
        BoundaryId = boundaryId.Trim();
    }

    public string BoundaryId { get; }
}

public sealed record BehaviorPatternPayload : EventPayload
{
    public BehaviorPatternPayload(
        BehaviorPatternKind pattern,
        IEnumerable<EventId> evidenceEvents)
    {
        if (!Enum.IsDefined(pattern))
        {
            throw new ArgumentOutOfRangeException(nameof(pattern), pattern, "Unknown behavior pattern.");
        }

        ArgumentNullException.ThrowIfNull(evidenceEvents);
        EventId[] materializedEvidence = evidenceEvents
            .Distinct()
            .OrderBy(eventId => eventId.Value)
            .ToArray();
        if (materializedEvidence.Length == 0 || materializedEvidence.Any(eventId => eventId.IsEmpty))
        {
            throw new ArgumentException(
                "A behavior pattern requires at least one valid evidence event.",
                nameof(evidenceEvents));
        }

        Pattern = pattern;
        EvidenceEvents = Array.AsReadOnly(materializedEvidence);
    }

    public BehaviorPatternKind Pattern { get; }

    public IReadOnlyList<EventId> EvidenceEvents { get; }
}
