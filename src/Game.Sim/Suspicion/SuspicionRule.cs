using Game.Sim.Events;
using Game.Sim.Memory;

namespace Game.Sim.Suspicion;

public sealed class SuspicionRule
{
    public SuspicionRule(
        string id,
        EventType eventType,
        IEnumerable<EventTag>? requiredTags,
        MemoryKind? memoryKind,
        IEnumerable<SuspicionEffect> effects,
        BehaviorPatternKind? behaviorPattern = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(effects);

        if (!Enum.IsDefined(eventType))
        {
            throw new ArgumentOutOfRangeException(nameof(eventType), eventType, "Unknown event type.");
        }

        if (memoryKind is not null && !Enum.IsDefined(memoryKind.Value))
        {
            throw new ArgumentOutOfRangeException(nameof(memoryKind), memoryKind, "Unknown memory kind.");
        }

        if (behaviorPattern is not null && !Enum.IsDefined(behaviorPattern.Value))
        {
            throw new ArgumentOutOfRangeException(
                nameof(behaviorPattern),
                behaviorPattern,
                "Unknown behavior pattern.");
        }

        if (behaviorPattern is not null && eventType != EventType.BehaviorPattern)
        {
            throw new ArgumentException(
                "A behavior pattern matcher requires the BehaviorPattern event type.",
                nameof(behaviorPattern));
        }

        EventTag[] materializedTags = requiredTags?
            .Distinct()
            .Order()
            .ToArray() ?? [];
        if (materializedTags.Any(tag => !Enum.IsDefined(tag)))
        {
            throw new ArgumentOutOfRangeException(
                nameof(requiredTags),
                "Required tags contain an unknown value.");
        }

        SuspicionEffect[] materializedEffects = effects
            .OrderBy(effect => effect.Dimension)
            .ToArray();
        if (materializedEffects.Length == 0)
        {
            throw new ArgumentException("A suspicion rule must have at least one effect.", nameof(effects));
        }

        if (materializedEffects.Select(effect => effect.Dimension).Distinct().Count() !=
            materializedEffects.Length)
        {
            throw new ArgumentException(
                "A suspicion rule cannot define the same dimension more than once.",
                nameof(effects));
        }

        Id = id.Trim();
        EventType = eventType;
        RequiredTags = Array.AsReadOnly(materializedTags);
        MemoryKind = memoryKind;
        Effects = Array.AsReadOnly(materializedEffects);
        BehaviorPattern = behaviorPattern;
    }

    public string Id { get; }

    public EventType EventType { get; }

    public IReadOnlyList<EventTag> RequiredTags { get; }

    public MemoryKind? MemoryKind { get; }

    public IReadOnlyList<SuspicionEffect> Effects { get; }

    public BehaviorPatternKind? BehaviorPattern { get; }

    public bool Matches(MemoryRecord memory)
    {
        ArgumentNullException.ThrowIfNull(memory);

        return memory.EventType == EventType &&
            (MemoryKind is null || memory.Kind == MemoryKind) &&
            (BehaviorPattern is null || memory.BehaviorPattern == BehaviorPattern) &&
            RequiredTags.All(memory.Tags.Contains);
    }
}
