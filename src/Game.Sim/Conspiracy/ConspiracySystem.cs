using Game.Sim.Entities;
using Game.Sim.Events;
using Game.Sim.Locations;
using Game.Sim.Memory;
using Game.Sim.Snapshots;
using Game.Sim.Suspicion;
using Game.Sim.Time;
using Game.Sim.World;

namespace Game.Sim.Conspiracy;

public sealed class ConspiracySystem
{
    private static readonly EventTag[] ConfrontationTags = [
        EventTag.Suspicious,
        EventTag.Pattern,
    ];

    private readonly WorldState _world;
    private readonly SimClock _clock;
    private readonly SuspicionSystem _suspicion;
    private readonly MemorySystem _memories;
    private readonly WorldEventFactory _events;
    private readonly IWorldEventBuffer _eventBuffer;

    private AccusationCoalition? _activeCoalition;

    public ConspiracySystem(
        WorldState world,
        SimClock clock,
        SuspicionSystem suspicion,
        MemorySystem memories,
        WorldEventFactory events,
        IWorldEventBuffer eventBuffer)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(suspicion);
        ArgumentNullException.ThrowIfNull(memories);
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(eventBuffer);

        _world = world;
        _clock = clock;
        _suspicion = suspicion;
        _memories = memories;
        _events = events;
        _eventBuffer = eventBuffer;
    }

    public AccusationCoalition? ActiveCoalition => _activeCoalition;

    public ClimaxResolution? LastResolution { get; private set; }

    public AccusationCoalition? EvaluateAndFormCoalition(EntityId target)
    {
        if (target.IsEmpty)
        {
            return null;
        }

        if (_activeCoalition is { } existing &&
            existing.Target == target &&
            existing.Stage is CoalitionStage.Confronting or CoalitionStage.Concluded)
        {
            return existing;
        }

        var candidateSuspicions = new List<(EntityId Actor, float Score, List<string> Evidence)>();

        foreach (EntityState entity in _world.Entities.Where(e => e.Id != target))
        {
            SuspicionSnapshot snapshot = _suspicion.GetSnapshot(entity.Id, target, _clock.Now);
            float score = CalculateConcernScore(snapshot.Vector);

            if (score >= 15.0f)
            {
                var evidenceRules = snapshot.Evidence
                    .Select(e => e.Contribution.RuleId)
                    .Distinct()
                    .ToList();
                candidateSuspicions.Add((entity.Id, score, evidenceRules));
            }
        }

        if (candidateSuspicions.Count == 0)
        {
            _activeCoalition = null;
            return null;
        }

        // Sort by highest suspicion score
        candidateSuspicions.Sort((a, b) => b.Score.CompareTo(a.Score));
        var top = candidateSuspicions[0];

        var coalition = new AccusationCoalition(top.Actor, target);
        coalition.AddMember(top.Actor, top.Score, top.Evidence);

        for (int i = 1; i < candidateSuspicions.Count; i++)
        {
            coalition.AddMember(candidateSuspicions[i].Actor, candidateSuspicions[i].Score, candidateSuspicions[i].Evidence);
        }

        _activeCoalition = coalition;
        return coalition;
    }

    public WorldEvent? TriggerConfrontation(LocationId confrontationLocation)
    {
        if (_activeCoalition is null ||
            !_activeCoalition.ConsensusReached ||
            _activeCoalition.Stage != CoalitionStage.ConsensusReached)
        {
            return null;
        }

        _activeCoalition.Stage = CoalitionStage.Confronting;

        WorldEvent confrontationEvent = _events.Create(
            actor: _activeCoalition.Initiator,
            type: EventType.Interaction,
            location: confrontationLocation,
            target: _activeCoalition.Target,
            tags: ConfrontationTags,
            payload: new InteractionPayload(InteractionKind.Generic, $"coalition-confrontation:{_activeCoalition.Target.Value}"));

        _eventBuffer.Publish(confrontationEvent);
        return confrontationEvent;
    }

    public ClimaxResolution ResolveClimax(PlayerClimaxChoice choice, EntityId target)
    {
        if (!CanResolveClimax(target))
        {
            throw new InvalidOperationException(
                "The climax cannot be resolved until a coalition reaches consensus and begins a confrontation.");
        }

        ClimaxResolution resolution;
        switch (choice)
        {
            case PlayerClimaxChoice.ConfessReality:
                _activeCoalition!.Stage = CoalitionStage.Concluded;

                resolution = new ClimaxResolution(
                    Choice: PlayerClimaxChoice.ConfessReality,
                    Title: "Existential Awakening Ending",
                    NarrativeText: $"The NPCs of the hotel stand frozen in collective awe and terror. The impossible time skips, vanishing locked items, and unnatural speed... it wasn't madness or crime. You are 'The Player', and their world is a simulated reality.",
                    PlayerVindicated: false,
                    ExistentialAwakeningTriggered: true,
                    PlayerFled: false);
                break;

            case PlayerClimaxChoice.DenyAndCounter:
                _activeCoalition!.Dissolve();

                resolution = new ClimaxResolution(
                    Choice: PlayerClimaxChoice.DenyAndCounter,
                    Title: "Doubt & Division Ending",
                    NarrativeText: $"You skillfully cross-examine the witnesses, pointing out contradictions in their hearsay and turning their suspicions against each other. The coalition collapses into mutual confusion and second-guessing.",
                    PlayerVindicated: true,
                    ExistentialAwakeningTriggered: false,
                    PlayerFled: false);
                break;

            case PlayerClimaxChoice.Flee:
            default:
                _activeCoalition!.Stage = CoalitionStage.Concluded;

                resolution = new ClimaxResolution(
                    Choice: PlayerClimaxChoice.Flee,
                    Title: "The Great Escape Ending",
                    NarrativeText: $"Exploiting a moment of hesitation, you dash past the perimeter into the mist-shrouded hotel garden, escaping their grasp while leaving the conspiracy shouting into the darkness.",
                    PlayerVindicated: false,
                    ExistentialAwakeningTriggered: false,
                    PlayerFled: true);
                break;
        }

        LastResolution = resolution;
        return resolution;
    }

    public bool CanResolveClimax(EntityId target) =>
        _activeCoalition is { } coalition &&
        coalition.Target == target &&
        coalition.ConsensusReached &&
        coalition.Stage == CoalitionStage.Confronting &&
        LastResolution is null;

    internal void RestoreState(
        AccusationCoalitionSnapshot? coalitionSnapshot,
        ClimaxResolutionSnapshot? resolutionSnapshot)
    {
        _activeCoalition = coalitionSnapshot is null
            ? null
            : AccusationCoalition.Restore(
                new EntityId(coalitionSnapshot.Initiator),
                new EntityId(coalitionSnapshot.Target),
                coalitionSnapshot.Members.Select(member => new EntityId(member)),
                coalitionSnapshot.EvidenceSummaries,
                coalitionSnapshot.CombinedSuspicionScore,
                Enum.Parse<CoalitionStage>(coalitionSnapshot.Stage, ignoreCase: true));

        LastResolution = resolutionSnapshot is null
            ? null
            : new ClimaxResolution(
                Enum.Parse<PlayerClimaxChoice>(resolutionSnapshot.Choice, ignoreCase: true),
                resolutionSnapshot.Title,
                resolutionSnapshot.NarrativeText,
                resolutionSnapshot.PlayerVindicated,
                resolutionSnapshot.ExistentialAwakeningTriggered,
                resolutionSnapshot.PlayerFled);
    }

    public static float CalculateConcernScore(SuspicionVector vector)
    {
        return vector.Criminality +
               vector.RoleDeviation +
               vector.Secrecy +
               (vector.MetaBehavior * 1.5f) +
               (vector.ImpossibleBehavior * 2.0f);
    }
}
