using Game.Sim.Entities;
using Game.Sim.Events;
using Game.Sim.Locations;
using Game.Sim.Memory;
using Game.Sim.Objects;
using Game.Sim.Perception;
using Game.Sim.Suspicion;
using Game.Sim.Time;
using Game.Sim.World;

namespace Game.Sim.Player;

public sealed class DialogueSystem
{
    private const float TransmissionConfidence = 0.85f;
    private static readonly EventTag[] DialogueTags = [
        EventTag.Visible,
        EventTag.Audible,
    ];

    private readonly SimClock _clock;
    private readonly WorldState _world;
    private readonly MemorySystem _memories;
    private readonly SuspicionSystem _suspicion;
    private readonly WorldEventFactory _events;
    private readonly IWorldEventBuffer _eventBuffer;
    private readonly HotelObjectRegistry _objects;

    public DialogueSystem(
        SimClock clock,
        WorldState world,
        MemorySystem memories,
        SuspicionSystem suspicion,
        WorldEventFactory events,
        IWorldEventBuffer eventBuffer,
        HotelObjectRegistry? objects = null)
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
        _objects = objects ?? HotelObjectRegistry.CreateDefaultHotelObjects();
    }

    public DialogueOutcome Execute(DialogueRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        EntityState requesterState = _world.GetEntity(request.Requester);
        EntityState partnerState = _world.GetEntity(request.Partner);

        if (requesterState.LogicalLocation != partnerState.LogicalLocation)
        {
            return new DialogueOutcome(
                Succeeded: false,
                Text: string.Empty,
                FailureReason: $"Cannot speak with {request.Partner.Value}; not in the same location.");
        }

        return request.Kind switch
        {
            DialogueActionKind.AskAboutSubject => HandleAskAboutSubject(request, requesterState.LogicalLocation),
            DialogueActionKind.ShareRumor => HandleShareRumor(request, requesterState.LogicalLocation),
            DialogueActionKind.InquireSchedule => HandleInquireSchedule(request),
            DialogueActionKind.InquireAboutObject => HandleInquireAboutObject(request, requesterState.LogicalLocation),
            DialogueActionKind.ConfrontEvidence => HandleConfrontEvidence(request, requesterState.LogicalLocation),
            _ => throw new InvalidOperationException($"Unsupported dialogue action kind '{request.Kind}'."),
        };
    }

    private DialogueOutcome HandleAskAboutSubject(DialogueRequest request, LocationId location)
    {
        EntityId subject = request.Subject!.Value;
        MemoryStore requesterStore = _memories.GetStore(request.Requester);
        MemoryStore partnerStore = _memories.GetStore(request.Partner);

        MemoryRecord? memoryToShare = partnerStore.Memories
            .Where(memory =>
                memory.Subject == subject &&
                !requesterStore.KnowsRootEvent(memory.RootEventId))
            .OrderByDescending(memory => memory.EventTime)
            .ThenByDescending(memory => memory.CreatedAt)
            .ThenByDescending(memory => memory.Id.Value)
            .FirstOrDefault();

        if (memoryToShare is null)
        {
            return new DialogueOutcome(
                Succeeded: true,
                Text: $"{request.Partner.Value}: \"I haven't seen or heard anything notable about {subject.Value} recently.\"");
        }

        MemoryRecord? shared = _memories.ShareMemory(
            request.Partner,
            request.Requester,
            memoryToShare.Id,
            _clock.Now,
            TransmissionConfidence);

        if (shared is null)
        {
            return new DialogueOutcome(
                Succeeded: false,
                Text: string.Empty,
                FailureReason: "Failed to transfer memory from partner.");
        }

        _ = _suspicion.ProcessMemory(request.Requester, shared);

        WorldEvent askEvent = _events.Create(
            request.Requester,
            EventType.AskInformation,
            location,
            request.Partner,
            DialogueTags,
            new InformationExchangePayload(subject, memoryToShare.RootEventId));

        _eventBuffer.Publish(askEvent);

        string locationDesc = memoryToShare.Location is { IsEmpty: false } loc ? loc.Value : "an unknown area";
        string responseText = $"{request.Partner.Value}: \"I remember seeing {subject.Value} involved in {memoryToShare.EventType} at {locationDesc} around tick {memoryToShare.EventTime.Tick}.\"";

        return new DialogueOutcome(
            Succeeded: true,
            Text: responseText,
            TransferredMemory: shared,
            GeneratedEvent: askEvent);
    }

    private DialogueOutcome HandleShareRumor(DialogueRequest request, LocationId location)
    {
        MemoryId memoryId = request.MemoryToShare!.Value;
        MemoryStore requesterStore = _memories.GetStore(request.Requester);
        MemoryStore partnerStore = _memories.GetStore(request.Partner);

        MemoryRecord sourceMemory = requesterStore.GetMemory(memoryId);
        if (partnerStore.KnowsRootEvent(sourceMemory.RootEventId))
        {
            return new DialogueOutcome(
                Succeeded: true,
                Text: $"{request.Partner.Value}: \"I already know about that.\"");
        }

        MemoryRecord? shared = _memories.ShareMemory(
            request.Requester,
            request.Partner,
            sourceMemory.Id,
            _clock.Now,
            TransmissionConfidence);

        if (shared is null)
        {
            return new DialogueOutcome(
                Succeeded: false,
                Text: string.Empty,
                FailureReason: "Failed to transfer memory to partner.");
        }

        _ = _suspicion.ProcessMemory(request.Partner, shared);

        EntityId subject = sourceMemory.Subject ?? request.Requester;
        WorldEvent shareEvent = _events.Create(
            request.Requester,
            EventType.ShareInformation,
            location,
            request.Partner,
            DialogueTags,
            new InformationExchangePayload(subject, sourceMemory.RootEventId));

        _eventBuffer.Publish(shareEvent);

        string text = $"You told {request.Partner.Value} about {subject.Value} ({sourceMemory.EventType}).";
        return new DialogueOutcome(
            Succeeded: true,
            Text: text,
            TransferredMemory: shared,
            GeneratedEvent: shareEvent);
    }

    private static DialogueOutcome HandleInquireSchedule(DialogueRequest request)
    {
        string text = $"{request.Partner.Value}: \"I'm on duty around the hotel right now.\"";
        return new DialogueOutcome(
            Succeeded: true,
            Text: text);
    }

    private DialogueOutcome HandleInquireAboutObject(DialogueRequest request, LocationId location)
    {
        string objectId = request.TargetObjectId!;
        InteractiveObject? targetObj = _objects.GetObject(objectId);

        WorldEvent askEvent = _events.Create(
            request.Requester,
            EventType.AskInformation,
            location,
            request.Partner,
            DialogueTags,
            new InformationExchangePayload(request.Partner, new EventId(1)));

        _eventBuffer.Publish(askEvent);

        if (string.Equals(objectId, "kitchen-pantry-safe", StringComparison.OrdinalIgnoreCase))
        {
            if (request.Partner == new EntityId("bob"))
            {
                string clueText = "Bob: \"Oh, the kitchen wall safe? I keep it locked with my chef key, but I think I misplaced it near the marble statue in the Garden earlier...\"";

                // Remember clue
                MemoryRecord? clueMemory = _memories.Remember(new Observation(
                    id: new ObservationId(askEvent.Id.Value),
                    sourceEvent: askEvent.Id,
                    observer: request.Requester,
                    perceivedActor: request.Partner,
                    perceivedType: EventType.AskInformation,
                    location: location,
                    perceivedTags: DialogueTags,
                    time: _clock.Now,
                    confidence: 1.0f,
                    salience: 0.9f,
                    channel: PerceptionChannel.Audio));

                return new DialogueOutcome(
                    Succeeded: true,
                    Text: clueText,
                    TransferredMemory: clueMemory,
                    GeneratedEvent: askEvent);
            }

            return new DialogueOutcome(
                Succeeded: true,
                Text: $"{request.Partner.Value}: \"That's the kitchen wall safe. Only Bob the Chef has the key to it.\"",
                GeneratedEvent: askEvent);
        }

        if (string.Equals(objectId, "basement-incriminating-ledger", StringComparison.OrdinalIgnoreCase))
        {
            string alarmedText = $"{request.Partner.Value}: \"Wait, what black ledger? Why are you asking about that? Are you snooping around where you shouldn't be?!\"";
            return new DialogueOutcome(
                Succeeded: true,
                Text: alarmedText,
                GeneratedEvent: askEvent);
        }

        if (targetObj is not null)
        {
            string text = $"{request.Partner.Value}: \"I've seen the {targetObj.DisplayName} over at {targetObj.Location.Value}. {targetObj.ClueDescription}\"";
            return new DialogueOutcome(
                Succeeded: true,
                Text: text,
                GeneratedEvent: askEvent);
        }

        return new DialogueOutcome(
            Succeeded: true,
            Text: $"{request.Partner.Value}: \"I'm not familiar with that item.\"",
            GeneratedEvent: askEvent);
    }

    private DialogueOutcome HandleConfrontEvidence(DialogueRequest request, LocationId location)
    {
        MemoryId evidenceId = request.ConfrontingMemoryId!.Value;
        MemoryStore requesterStore = _memories.GetStore(request.Requester);
        MemoryRecord evidence = requesterStore.GetMemory(evidenceId);

        WorldEvent confrontEvent = _events.Create(
            request.Requester,
            EventType.AskInformation,
            location,
            request.Partner,
            DialogueTags,
            new InformationExchangePayload(evidence.Subject ?? request.Partner, evidence.RootEventId));

        _eventBuffer.Publish(confrontEvent);

        if (evidence.Subject == request.Partner)
        {
            string locName = evidence.Location is { IsEmpty: false } loc ? loc.Value : "there";
            string responseText = $"{request.Partner.Value}: \"Look, I had a valid reason to be at {locName}! Don't go spreading wild accusations!\"";
            return new DialogueOutcome(
                Succeeded: true,
                Text: responseText,
                GeneratedEvent: confrontEvent);
        }

        // Shared rumor about third party
        MemoryRecord? shared = _memories.ShareMemory(
            request.Requester,
            request.Partner,
            evidence.Id,
            _clock.Now,
            TransmissionConfidence);

        if (shared is not null)
        {
            _ = _suspicion.ProcessMemory(request.Partner, shared);
        }

        string thirdPartyResponse = $"{request.Partner.Value}: \"You actually saw {evidence.Subject?.Value} doing that? That is very concerning...\"";
        return new DialogueOutcome(
            Succeeded: true,
            Text: thirdPartyResponse,
            TransferredMemory: shared,
            GeneratedEvent: confrontEvent);
    }
}
