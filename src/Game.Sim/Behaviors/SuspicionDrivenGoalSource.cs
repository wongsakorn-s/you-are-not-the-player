using Game.Sim.Brain;
using Game.Sim.Entities;
using Game.Sim.Locations;
using Game.Sim.Memory;
using Game.Sim.Routines;
using Game.Sim.Suspicion;
using Game.Sim.Time;

namespace Game.Sim.Behaviors;

public sealed class SuspicionDrivenGoalSource : INpcGoalSource, INpcRoutineDecisionObserver
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
    private readonly Dictionary<EntityId, int> _attentionSpell = [];
    private readonly Dictionary<EntityId, int> _attentionRest = [];

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

        ContactBelief[] contacts = FindKnownContacts(profile);
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
                AddTargetGoals(
                    goals,
                    snapshot.Subject,
                    knownTargetLocation,
                    concern,
                    mayAttend: !_attentionRest.TryGetValue(context.Entity.Id, out int rest) || rest <= 0);
            }

            foreach (ContactBelief contact in contacts)
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

    /// <summary>
    /// Counts how long somebody has been giving one person their attention, so
    /// that it can end.
    /// </summary>
    public void Observe(NpcRoutineDecision decision)
    {
        ArgumentNullException.ThrowIfNull(decision);
        if (_attentionRest.TryGetValue(decision.Entity, out int rest) && rest > 0)
        {
            _attentionRest[decision.Entity] = rest - 1;
        }

        if (decision.Goal.Type is not (GoalType.FollowTarget or GoalType.ObserveTarget))
        {
            // Eases off rather than resetting. Requiring an unbroken run let a
            // character watch somebody eleven decisions out of twelve all night
            // and never once trip the limit, which is the behaviour this exists
            // to stop.
            if (_attentionSpell.TryGetValue(decision.Entity, out int cooling) && cooling > 0)
            {
                _attentionSpell[decision.Entity] = cooling - 1;
            }

            return;
        }

        int spell = _attentionSpell.TryGetValue(decision.Entity, out int current) ? current + 1 : 1;
        if (spell < _policy.AttentionSpellDecisions)
        {
            _attentionSpell[decision.Entity] = spell;
            return;
        }

        _attentionSpell[decision.Entity] = 0;
        _attentionRest[decision.Entity] = _policy.AttentionRestDecisions;
    }

    /// <summary>
    /// Everyone this character could pass something on to right now.
    /// </summary>
    /// <remarks>
    /// This used to return the first contact whose whereabouts were known, so
    /// each character spent the night confiding in exactly one other person. Only
    /// the manager lists the receptionist first, so on any night the manager was
    /// the one being steered - and therefore not in this roster at all - nobody
    /// ever turned to the night porter. Measured over seven nights the cast made
    /// two to three hundred attempts to pass something on and not one of them was
    /// aimed at the player, which is why the case file was first-hand only.
    /// </remarks>
    private ContactBelief[] FindKnownContacts(SuspicionBehaviorProfile profile)
    {
        var known = new List<ContactBelief>();
        foreach (EntityId contact in profile.Contacts)
        {
            LocationId? location = _memories.GetLastKnownLocation(profile.Entity, contact);
            if (location is LocationId knownLocation)
            {
                known.Add(new ContactBelief(contact, knownLocation));
            }
        }

        return [.. known];
    }

    private void AddTargetGoals(
        List<GoalCandidate> goals,
        EntityId subject,
        LocationId location,
        float concern,
        bool mayAttend)
    {
        if (mayAttend && concern >= _policy.ObserveThreshold)
        {
            goals.Add(CreateGoal(
                GoalType.ObserveTarget,
                location,
                ObserveBaseUtility,
                concern,
                subject,
                contact: null));
        }

        if (mayAttend && concern >= _policy.FollowThreshold)
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

    private GoalCandidate CreateGoal(
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
            [new UtilityReason(
                "belief:suspicion",
                MathF.Min(beliefWeight, _policy.MaxBeliefWeight))],
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
