using Game.Sim.Entities;
using Game.Sim.Events;
using Game.Sim.Locations;
using Game.Sim.Time;

namespace Game.Sim.Patterns;

public sealed class BehaviorPatternMatch
{
    public BehaviorPatternMatch(
        BehaviorPatternKind pattern,
        EntityId actor,
        SimTime detectedAt,
        LocationId location,
        IEnumerable<EventId> evidenceEvents)
    {
        if (!Enum.IsDefined(pattern))
        {
            throw new ArgumentOutOfRangeException(nameof(pattern), pattern, "Unknown behavior pattern.");
        }

        if (actor.IsEmpty)
        {
            throw new ArgumentException("Pattern actor cannot be empty.", nameof(actor));
        }

        if (location.IsEmpty)
        {
            throw new ArgumentException("Pattern location cannot be empty.", nameof(location));
        }

        ArgumentNullException.ThrowIfNull(evidenceEvents);
        EventId[] materializedEvidence = evidenceEvents
            .Distinct()
            .OrderBy(eventId => eventId.Value)
            .ToArray();
        if (materializedEvidence.Length == 0 || materializedEvidence.Any(eventId => eventId.IsEmpty))
        {
            throw new ArgumentException(
                "A pattern match requires at least one valid evidence event.",
                nameof(evidenceEvents));
        }

        Pattern = pattern;
        Actor = actor;
        DetectedAt = detectedAt;
        Location = location;
        EvidenceEvents = Array.AsReadOnly(materializedEvidence);
    }

    public BehaviorPatternKind Pattern { get; }

    public EntityId Actor { get; }

    public SimTime DetectedAt { get; }

    public LocationId Location { get; }

    public IReadOnlyList<EventId> EvidenceEvents { get; }
}
