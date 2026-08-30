using Game.Sim.Entities;
using Game.Sim.Events;

namespace Game.Sim.Patterns;

public sealed class RuleBasedBehaviorPatternDetector : IBehaviorPatternDetector
{
    private readonly int _ticksPerSecond;
    private readonly BehaviorPatternPolicy _policy;
    private readonly Dictionary<EntityId, List<WorldEvent>> _historyByActor = [];
    private readonly HashSet<DetectionKey> _activeDetections = [];
    private long _lastProcessedTick = -1;

    public RuleBasedBehaviorPatternDetector(
        int ticksPerSecond,
        BehaviorPatternPolicy? policy = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(ticksPerSecond);
        _ticksPerSecond = ticksPerSecond;
        _policy = policy ?? new BehaviorPatternPolicy();
    }

    public IReadOnlyList<BehaviorPatternMatch> Process(WorldEvent worldEvent)
    {
        ArgumentNullException.ThrowIfNull(worldEvent);
        if (worldEvent.Time.Tick < _lastProcessedTick)
        {
            throw new ArgumentException(
                "Behavior pattern events must be processed in chronological order.",
                nameof(worldEvent));
        }

        _lastProcessedTick = worldEvent.Time.Tick;
        if (!IsRelevant(worldEvent.Type))
        {
            return [];
        }

        List<WorldEvent> history = GetHistory(worldEvent.Actor);
        history.Add(worldEvent);
        PruneHistory(history, worldEvent.Time.Tick);

        var matches = new List<BehaviorPatternMatch>(2);
        switch (worldEvent.Type)
        {
            case EventType.Interaction:
                InteractionPayload interaction = GetRequiredPayload<InteractionPayload>(worldEvent);
                if (interaction.Kind == InteractionKind.LootContainer)
                {
                    EvaluateLootSweep(worldEvent, history, matches);
                }

                EvaluateRepeatInteraction(worldEvent, interaction.InteractionId, history, matches);
                break;
            case EventType.RoleDutyMissed:
                _ = GetRequiredPayload<RoleDutyPayload>(worldEvent);
                EvaluateRoleNeglect(worldEvent, history, matches);
                break;
            case EventType.BoundaryProbe:
                _ = GetRequiredPayload<BoundaryProbePayload>(worldEvent);
                EvaluateBoundaryTesting(worldEvent, history, matches);
                break;
        }

        return matches;
    }

    private void EvaluateLootSweep(
        WorldEvent current,
        IReadOnlyList<WorldEvent> history,
        ICollection<BehaviorPatternMatch> matches)
    {
        WorldEvent[] evidence = EventsInWindow(
                history,
                current.Time.Tick,
                _policy.LootSweepWindowSeconds,
                EventType.Interaction)
            .Where(worldEvent =>
                worldEvent.Payload is InteractionPayload { Kind: InteractionKind.LootContainer })
            .GroupBy(
                worldEvent => ((InteractionPayload)worldEvent.Payload).InteractionId,
                StringComparer.Ordinal)
            .Select(group => group.OrderByDescending(item => item.Time).ThenByDescending(item => item.Id.Value).First())
            .OrderBy(worldEvent => worldEvent.Id.Value)
            .ToArray();
        Evaluate(
            current,
            BehaviorPatternKind.LootSweep,
            discriminator: string.Empty,
            evidence,
            _policy.LootSweepDistinctInteractions,
            matches);
    }

    private void EvaluateRepeatInteraction(
        WorldEvent current,
        string interactionId,
        IReadOnlyList<WorldEvent> history,
        ICollection<BehaviorPatternMatch> matches)
    {
        WorldEvent[] evidence = EventsInWindow(
                history,
                current.Time.Tick,
                _policy.RepeatInteractionWindowSeconds,
                EventType.Interaction)
            .Where(worldEvent =>
                worldEvent.Payload is InteractionPayload payload &&
                string.Equals(payload.InteractionId, interactionId, StringComparison.Ordinal))
            .ToArray();
        Evaluate(
            current,
            BehaviorPatternKind.RepeatInteraction,
            interactionId,
            evidence,
            _policy.RepeatInteractionCount,
            matches);
    }

    private void EvaluateRoleNeglect(
        WorldEvent current,
        IReadOnlyList<WorldEvent> history,
        ICollection<BehaviorPatternMatch> matches)
    {
        WorldEvent[] evidence = EventsInWindow(
            history,
            current.Time.Tick,
            _policy.RoleNeglectWindowSeconds,
            EventType.RoleDutyMissed);
        Evaluate(
            current,
            BehaviorPatternKind.RoleNeglect,
            discriminator: string.Empty,
            evidence,
            _policy.RoleNeglectCount,
            matches);
    }

    private void EvaluateBoundaryTesting(
        WorldEvent current,
        IReadOnlyList<WorldEvent> history,
        ICollection<BehaviorPatternMatch> matches)
    {
        WorldEvent[] evidence = EventsInWindow(
                history,
                current.Time.Tick,
                _policy.BoundaryTestingWindowSeconds,
                EventType.BoundaryProbe)
            .GroupBy(
                worldEvent => ((BoundaryProbePayload)worldEvent.Payload).BoundaryId,
                StringComparer.Ordinal)
            .Select(group => group.OrderByDescending(item => item.Time).ThenByDescending(item => item.Id.Value).First())
            .OrderBy(worldEvent => worldEvent.Id.Value)
            .ToArray();
        Evaluate(
            current,
            BehaviorPatternKind.BoundaryTesting,
            discriminator: string.Empty,
            evidence,
            _policy.BoundaryTestingDistinctProbes,
            matches);
    }

    private void Evaluate(
        WorldEvent current,
        BehaviorPatternKind pattern,
        string discriminator,
        IReadOnlyCollection<WorldEvent> evidence,
        int threshold,
        ICollection<BehaviorPatternMatch> matches)
    {
        var key = new DetectionKey(current.Actor, pattern, discriminator);
        if (evidence.Count < threshold)
        {
            _activeDetections.Remove(key);
            return;
        }

        if (!_activeDetections.Add(key))
        {
            return;
        }

        matches.Add(new BehaviorPatternMatch(
            pattern,
            current.Actor,
            current.Time,
            current.Location,
            evidence.Select(worldEvent => worldEvent.Id)));
    }

    private WorldEvent[] EventsInWindow(
        IEnumerable<WorldEvent> history,
        long currentTick,
        int windowSeconds,
        EventType type)
    {
        long windowTicks = checked((long)windowSeconds * _ticksPerSecond);
        long minimumTick = Math.Max(0, currentTick - windowTicks);
        return history
            .Where(worldEvent => worldEvent.Type == type && worldEvent.Time.Tick >= minimumTick)
            .OrderBy(worldEvent => worldEvent.Time)
            .ThenBy(worldEvent => worldEvent.Id.Value)
            .ToArray();
    }

    private List<WorldEvent> GetHistory(EntityId actor)
    {
        if (!_historyByActor.TryGetValue(actor, out List<WorldEvent>? history))
        {
            history = [];
            _historyByActor.Add(actor, history);
        }

        return history;
    }

    private void PruneHistory(List<WorldEvent> history, long currentTick)
    {
        long minimumTick = Math.Max(
            0,
            currentTick - _policy.GetMaximumWindowTicks(_ticksPerSecond));
        history.RemoveAll(worldEvent => worldEvent.Time.Tick < minimumTick);
    }

    private static TPayload GetRequiredPayload<TPayload>(WorldEvent worldEvent)
        where TPayload : EventPayload =>
        worldEvent.Payload as TPayload ?? throw new ArgumentException(
            $"Event '{worldEvent.Type}' requires payload '{typeof(TPayload).Name}'.",
            nameof(worldEvent));

    private static bool IsRelevant(EventType type) => type is
        EventType.Interaction or EventType.RoleDutyMissed or EventType.BoundaryProbe;

    private readonly record struct DetectionKey(
        EntityId Actor,
        BehaviorPatternKind Pattern,
        string Discriminator);
}
