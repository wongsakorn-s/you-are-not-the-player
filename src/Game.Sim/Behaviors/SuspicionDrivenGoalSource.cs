using Game.Sim.Brain;
using Game.Sim.Entities;
using Game.Sim.Locations;
using Game.Sim.Memory;
using Game.Sim.Suspicion;
using Game.Sim.Time;

namespace Game.Sim.Behaviors;

public sealed class SuspicionDrivenGoalSource : INpcGoalSource
{
    private const float ObserveBaseUtility = 20.0f;
    private const float AskBaseUtility = 25.0f;
    private const float FollowBaseUtility = 30.0f;
    private const float ShareBaseUtility = 40.0f;
    private const float AvoidBaseUtility = 55.0f;

    private readonly SuspicionSystem _suspicion;
    private readonly MemorySystem _memories;
    private readonly SimClock _clock;
    private readonly SuspicionBehaviorRepository _profiles;
    private readonly SuspicionBehaviorPolicy _policy;

    public SuspicionDrivenGoalSource(
        SuspicionSystem suspicion,
        MemorySystem memories,
        SimClock clock,
        SuspicionBehaviorRepository profiles,
        SuspicionBehaviorPolicy? policy = null)
    {
        ArgumentNullException.ThrowIfNull(suspicion);
        ArgumentNullException.ThrowIfNull(memories);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(profiles);
        _suspicion = suspicion;
        _memories = memories;
        _clock = clock;
        _profiles = profiles;
        _policy = policy ?? new SuspicionBehaviorPolicy();
    }

    public IReadOnlyList<GoalCandidate> Generate(NpcDecisionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!_profiles.TryGet(context.Entity.Id, out SuspicionBehaviorProfile? profile) ||
            profile is null)
        {
            return [];
        }

        if (!context.Profile.Role.CanEnter(profile.SafeLocation))
        {
            throw new InvalidOperationException(
                $"Safe location '{profile.SafeLocation}' is forbidden for '{profile.Entity}'.");
        }

        ContactBelief? contact = FindKnownContact(profile);
        var goals = new List<GoalCandidate>();
        foreach (SuspicionSnapshot snapshot in _suspicion.GetKnownSuspicions(
            context.Entity.Id,
            _clock.Now))
        {
            float concern = GetConcern(snapshot.Vector);
            LocationId? targetLocation = _memories.GetLastKnownLocation(
                context.Entity.Id,
                snapshot.Subject);

            if (targetLocation is LocationId knownTargetLocation)
            {
                AddTargetGoals(goals, snapshot.Subject, knownTargetLocation, concern);
            }

            if (contact is not null)
            {
                AddSocialGoals(
                    goals,
                    context.Entity.Id,
                    snapshot,
                    contact,
                    concern);
            }

            if (snapshot.Vector.Criminality >= _policy.AvoidCriminalityThreshold)
            {
                goals.Add(CreateGoal(
                    GoalType.AvoidTarget,
                    profile.SafeLocation,
                    AvoidBaseUtility,
                    snapshot.Vector.Criminality,
                    snapshot.Subject,
                    contact: null));
            }
        }

        return goals;
    }

    private ContactBelief? FindKnownContact(SuspicionBehaviorProfile profile)
    {
        foreach (EntityId contact in profile.Contacts)
        {
            LocationId? location = _memories.GetLastKnownLocation(profile.Entity, contact);
            if (location is LocationId knownLocation)
            {
                return new ContactBelief(contact, knownLocation);
            }
        }

        return null;
    }

    private void AddTargetGoals(
        List<GoalCandidate> goals,
        EntityId subject,
        LocationId location,
        float concern)
    {
        if (concern >= _policy.ObserveThreshold)
        {
            goals.Add(CreateGoal(
                GoalType.ObserveTarget,
                location,
                ObserveBaseUtility,
                concern,
                subject,
                contact: null));
        }

        if (concern >= _policy.FollowThreshold)
        {
            goals.Add(CreateGoal(
                GoalType.FollowTarget,
                location,
                FollowBaseUtility,
                concern,
                subject,
                contact: null));
        }
    }

    private void AddSocialGoals(
        List<GoalCandidate> goals,
        EntityId observer,
        SuspicionSnapshot snapshot,
        ContactBelief contact,
        float concern)
    {
        if (contact.Entity == snapshot.Subject)
        {
            return;
        }

        MemoryStore observerStore = _memories.GetStore(observer);
        MemoryStore contactStore = _memories.GetStore(contact.Entity);
        bool canAsk = contactStore.Memories.Any(memory =>
            memory.Subject == snapshot.Subject &&
            !observerStore.KnowsRootEvent(memory.RootEventId));
        bool canShare = snapshot.Evidence.Any(evidence =>
        {
            MemoryRecord sourceMemory = observerStore.GetMemory(
                evidence.Contribution.SourceMemory);
            return !contactStore.KnowsRootEvent(sourceMemory.RootEventId);
        });

        if (canAsk && concern >= _policy.AskThreshold)
        {
            goals.Add(CreateGoal(
                GoalType.AskAboutTarget,
                contact.Location,
                AskBaseUtility,
                concern,
                snapshot.Subject,
                contact.Entity));
        }

        if (canShare && concern >= _policy.ShareThreshold)
        {
            goals.Add(CreateGoal(
                GoalType.ShareSuspicion,
                contact.Location,
                ShareBaseUtility,
                concern,
                snapshot.Subject,
                contact.Entity));
        }
    }

    private static GoalCandidate CreateGoal(
        GoalType type,
        LocationId destination,
        float baseUtility,
        float beliefWeight,
        EntityId subject,
        EntityId? contact) =>
        new(
            type,
            destination,
            baseUtility,
            [new UtilityReason("belief:suspicion", beliefWeight)],
            ignoresRolePermissions: false,
            CreateIntentId(type, subject, contact),
            target: subject,
            interactionPartner: contact);

    private static string CreateIntentId(
        GoalType type,
        EntityId subject,
        EntityId? contact) =>
        contact is EntityId knownContact
            ? $"belief:{type}:{subject.Value}:{knownContact.Value}"
            : $"belief:{type}:{subject.Value}";

    private static float GetConcern(SuspicionVector vector) =>
        vector.Criminality +
        vector.Secrecy +
        vector.RoleDeviation +
        vector.MetaBehavior +
        vector.ImpossibleBehavior +
        vector.Deception;

    private sealed record ContactBelief(EntityId Entity, LocationId Location);
}
