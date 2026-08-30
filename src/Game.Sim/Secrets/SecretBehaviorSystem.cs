using Game.Sim.Entities;
using Game.Sim.Events;
using Game.Sim.Routines;
using Game.Sim.Time;
using Game.Sim.World;

namespace Game.Sim.Secrets;

public sealed class SecretBehaviorSystem : INpcRoutineDecisionObserver
{
    private static readonly EventTag[] TheftTags = [
        EventTag.Visible,
        EventTag.Criminal,
        EventTag.Secret,
    ];
    private static readonly EventTag[] SecretMeetingTags = [
        EventTag.Visible,
        EventTag.Secret,
    ];
    private static readonly EventTag[] NightActivityTags = [
        EventTag.Visible,
        EventTag.AfterHours,
    ];

    private readonly SimClock _clock;
    private readonly WorldState _world;
    private readonly WorldEventFactory _events;
    private readonly IWorldEventBuffer _eventBuffer;
    private readonly SecretPlanRepository _plans;
    private readonly HashSet<OccurrenceKey> _completedOccurrences = [];

    public SecretBehaviorSystem(
        SimClock clock,
        WorldState world,
        WorldEventFactory events,
        IWorldEventBuffer eventBuffer,
        SecretPlanRepository plans)
    {
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(eventBuffer);
        ArgumentNullException.ThrowIfNull(plans);

        foreach (SecretPlan plan in plans.Plans)
        {
            _ = world.GetLocation(plan.Location);
            foreach (EntityId participant in plan.Participants)
            {
                _ = world.GetEntity(participant);
            }
        }

        _clock = clock;
        _world = world;
        _events = events;
        _eventBuffer = eventBuffer;
        _plans = plans;
    }

    public void Observe(NpcRoutineDecision decision)
    {
        ArgumentNullException.ThrowIfNull(decision);
        if (decision.Time != _clock.Now)
        {
            throw new ArgumentException(
                "Routine decision time must match the current simulation clock.",
                nameof(decision));
        }

        if (decision.Goal.IntentId is not string planId ||
            !_plans.TryGet(planId, out SecretPlan? plan) ||
            plan is null ||
            !plan.Participants.Contains(decision.Entity) ||
            decision.Goal.Type != SecretGoalSource.GetGoalType(plan.Behavior) ||
            decision.Goal.Destination != plan.Location ||
            !plan.IsActive(_clock.TimeOfDay) ||
            plan.Participants.Any(participant =>
                _world.GetEntity(participant).LogicalLocation != plan.Location))
        {
            return;
        }

        var occurrence = new OccurrenceKey(plan.Id, GetOccurrenceDay(plan));
        if (!_completedOccurrences.Add(occurrence))
        {
            return;
        }

        EventTag[] tags = GetTags(plan);
        EntityId? target = plan.Participants.Count > 1 ? plan.Participants[1] : null;
        WorldEvent worldEvent = _events.Create(
            plan.Participants[0],
            GetEventType(plan.Behavior),
            plan.Location,
            target,
            tags,
            new SecretActivityPayload(plan.Id));
        _eventBuffer.Publish(worldEvent);
    }

    private long GetOccurrenceDay(SecretPlan plan) =>
        plan.Start > plan.End && _clock.TimeOfDay < plan.End
            ? _clock.DayIndex - 1
            : _clock.DayIndex;

    private static EventType GetEventType(SecretBehaviorKind behavior) => behavior switch
    {
        SecretBehaviorKind.Theft => EventType.Theft,
        SecretBehaviorKind.SecretMeeting => EventType.SecretMeeting,
        SecretBehaviorKind.NightOwl => EventType.NightActivity,
        _ => throw new ArgumentOutOfRangeException(nameof(behavior), behavior, "Unknown secret behavior."),
    };

    private static EventTag[] GetTags(SecretPlan plan)
    {
        EventTag[] baseTags = plan.Behavior switch
        {
            SecretBehaviorKind.Theft => TheftTags,
            SecretBehaviorKind.SecretMeeting => SecretMeetingTags,
            SecretBehaviorKind.NightOwl => NightActivityTags,
            _ => throw new ArgumentOutOfRangeException(
                nameof(plan),
                plan.Behavior,
                "Unknown secret behavior."),
        };

        return plan.IgnoresRolePermissions
            ? [.. baseTags, EventTag.Restricted]
            : [.. baseTags];
    }

    private readonly record struct OccurrenceKey(string PlanId, long DayIndex);
}
