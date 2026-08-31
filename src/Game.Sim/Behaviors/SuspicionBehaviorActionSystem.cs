using Game.Sim.Brain;
using Game.Sim.Entities;
using Game.Sim.Events;
using Game.Sim.Memory;
using Game.Sim.Routines;
using Game.Sim.Suspicion;
using Game.Sim.Time;
using Game.Sim.World;

namespace Game.Sim.Behaviors;

public sealed class SuspicionBehaviorActionSystem : INpcRoutineDecisionObserver
{
    private const float TransmissionConfidence = 0.85f;
    private static readonly EventTag[] InformationTags = [
        EventTag.Visible,
        EventTag.Audible,
    ];

    private readonly SimClock _clock;
    private readonly WorldState _world;
    private readonly MemorySystem _memories;
    private readonly SuspicionSystem _suspicion;
    private readonly WorldEventFactory _events;
    private readonly IWorldEventBuffer _eventBuffer;

    public SuspicionBehaviorActionSystem(
        SimClock clock,
        WorldState world,
        MemorySystem memories,
        SuspicionSystem suspicion,
        WorldEventFactory events,
        IWorldEventBuffer eventBuffer)
    {
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(memories);
        ArgumentNullException.ThrowIfNull(suspicion);
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(eventBuffer);
        _clock = clock;
        _world = world;
        _memories = memories;
        _suspicion = suspicion;
        _events = events;
        _eventBuffer = eventBuffer;
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

        if (decision.Goal.Target is not EntityId subject ||
            decision.Goal.InteractionPartner is not EntityId partner ||
            _world.GetEntity(decision.Entity).LogicalLocation != decision.Goal.Destination ||
            _world.GetEntity(partner).LogicalLocation != decision.Goal.Destination)
        {
            return;
        }

        switch (decision.Goal.Type)
        {
            case GoalType.ShareSuspicion:
                ShareSuspicion(decision.Entity, partner, subject);
                break;
            case GoalType.AskAboutTarget:
                AskAboutTarget(decision.Entity, partner, subject);
                break;
        }
    }

    private void ShareSuspicion(EntityId source, EntityId recipient, EntityId subject)
    {
        MemoryStore sourceStore = _memories.GetStore(source);
        MemoryStore recipientStore = _memories.GetStore(recipient);
        MemoryRecord? memory = _suspicion
            .GetSnapshot(source, subject, _clock.Now)
            .Evidence
            .OrderByDescending(evidence => evidence.EffectiveStrength)
            .ThenBy(evidence => evidence.Contribution.SourceMemory.Value)
            .Select(evidence => sourceStore.GetMemory(evidence.Contribution.SourceMemory))
            .FirstOrDefault(candidate => !recipientStore.KnowsRootEvent(candidate.RootEventId));
        if (memory is null)
        {
            return;
        }

        ShareMemory(
            informationSource: source,
            recipient,
            subject,
            memory,
            EventType.ShareInformation,
            eventActor: source,
            eventTarget: recipient);
    }

    private void AskAboutTarget(EntityId requester, EntityId contact, EntityId subject)
    {
        MemoryStore requesterStore = _memories.GetStore(requester);
        MemoryRecord? memory = _memories.GetStore(contact).Memories
            .Where(candidate =>
                candidate.Subject == subject &&
                !requesterStore.KnowsRootEvent(candidate.RootEventId))
            .OrderByDescending(candidate => candidate.EventTime)
            .ThenByDescending(candidate => candidate.CreatedAt)
            .ThenByDescending(candidate => candidate.Id.Value)
            .FirstOrDefault();
        if (memory is null)
        {
            return;
        }

        ShareMemory(
            informationSource: contact,
            recipient: requester,
            subject,
            memory,
            EventType.AskInformation,
            eventActor: requester,
            eventTarget: contact);
    }

    private void ShareMemory(
        EntityId informationSource,
        EntityId recipient,
        EntityId subject,
        MemoryRecord sourceMemory,
        EventType eventType,
        EntityId eventActor,
        EntityId eventTarget)
    {
        MemoryRecord? sharedMemory = _memories.ShareMemory(
            informationSource,
            recipient,
            sourceMemory.Id,
            _clock.Now,
            TransmissionConfidence);
        if (sharedMemory is null)
        {
            return;
        }

        _ = _suspicion.ProcessMemory(recipient, sharedMemory);
        WorldEvent informationEvent = _events.Create(
            eventActor,
            eventType,
            _world.GetEntity(eventActor).LogicalLocation,
            eventTarget,
            InformationTags,
            new InformationExchangePayload(subject, sourceMemory.RootEventId));
        _eventBuffer.Publish(informationEvent);
    }
}
