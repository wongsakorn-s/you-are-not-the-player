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
    private readonly ClaimLedger _claims = new();

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

    public ClaimLedger Claims => _claims;

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

    private DialogueOutcome HandleInquireSchedule(DialogueRequest request)
    {
        // The answer comes from the speaker's own memory rather than from world
        // state, so a character can only account for what they actually noticed
        // themselves - and can be caught out by someone who noticed more.
        MemoryRecord? lastMove = _memories.GetStore(request.Partner).Memories
            .Where(memory =>
                memory.Kind == MemoryKind.Episodic &&
                memory.Subject == request.Partner &&
                memory.EventType == EventType.EnterLocation &&
                memory.Location is { IsEmpty: false })
            .OrderByDescending(memory => memory.EventTime.Tick)
            .ThenByDescending(memory => memory.Id.Value)
            .FirstOrDefault();

        // Someone who has not moved all night still has an account to give: they
        // were standing where they are standing. Falling back to that keeps every
        // character challengeable instead of making stillness a free alibi.
        LocationId actual = lastMove?.Location!.Value
            ?? _world.GetEntity(request.Partner).LogicalLocation;
        SimTime when = lastMove?.EventTime ?? _clock.Now;
        LocationId stated = ChooseStatedLocation(request.Partner, actual);
        AlibiClaim claim = _claims.Record(
            request.Partner,
            stated,
            when,
            _clock.Now);

        return new DialogueOutcome(
            Succeeded: true,
            Text: $"{request.Partner.Value}: \"I was at {stated.Value} around then.\"",
            Claim: claim);
    }

    /// <summary>
    /// Nobody volunteers that they were somewhere they had no business being. A
    /// character who was last in a restricted room names an ordinary room next to
    /// it instead - which is exactly the kind of statement a witness can break.
    /// </summary>
    private LocationId ChooseStatedLocation(EntityId speaker, LocationId actual)
    {
        if (!_world.GetLocation(actual).IsRestricted)
        {
            return actual;
        }

        // Ordinal order, no RNG: the cover story has to be the same on every
        // replay of the same seed.
        LocationId? cover = _world.Locations
            .Where(location => !location.IsRestricted && location.Id != actual)
            .OrderBy(location => location.Id.Value, StringComparer.Ordinal)
            .Select(location => (LocationId?)location.Id)
            .FirstOrDefault();
        return cover ?? actual;
    }

    /// <summary>
    /// The player has put a clue against something the partner said about
    /// themselves. Either the account breaks or the player does.
    /// </summary>
    private DialogueOutcome ResolveChallenge(
        DialogueRequest request,
        MemoryRecord evidence,
        WorldEvent confrontEvent)
    {
        string locName = evidence.Location is { IsEmpty: false } loc ? loc.Value : "there";
        if (!ContradictionFinder.ContradictsAnyClaim(
                _claims.Claims,
                evidence,
                request.Partner,
                out AlibiClaim? challenged) ||
            challenged is null)
        {
            // Nothing they said is on the line, so this is just an awkward remark.
            return new DialogueOutcome(
                Succeeded: true,
                Text: $"{request.Partner.Value}: \"I had a reason to be at {locName}. Ask me something real.\"",
                GeneratedEvent: confrontEvent);
        }

        if (IsClaimFalse(challenged))
        {
            // Caught. The partner gives up the strongest thing they were holding
            // back, which is the reward that makes talking worth doing twice.
            MemoryRecord? conceded = ConcedeSomething(request);
            _ = _suspicion.ProcessMemory(request.Requester, evidence);
            return new DialogueOutcome(
                Succeeded: true,
                Text: $"{request.Partner.Value}: \"...Fine. I was not at {challenged.ClaimedLocation.Value}. " +
                    "I did not want this written down anywhere.\"",
                TransferredMemory: conceded,
                GeneratedEvent: confrontEvent,
                Confrontation: ConfrontationResult.Cracked);
        }

        // Their account held. Accusing someone of lying on the strength of a
        // second-hand story is exactly the kind of thing that gets remembered
        // about you - the world builds its case against the accuser instead.
        WorldEvent backfire = _events.Create(
            request.Requester,
            EventType.Interaction,
            _world.GetEntity(request.Requester).LogicalLocation,
            request.Partner,
            [EventTag.Visible, EventTag.Suspicious],
            new InteractionPayload(InteractionKind.Dialogue, "false-accusation"));
        _eventBuffer.Publish(backfire);

        return new DialogueOutcome(
            Succeeded: true,
            Text: $"{request.Partner.Value}: \"I was at {challenged.ClaimedLocation.Value}, and other people saw me there. " +
                "Where did you get that story?\"",
            GeneratedEvent: backfire,
            Confrontation: ConfrontationResult.Backfired);
    }

    /// <summary>
    /// The most recent thing the partner knows that the player does not.
    /// </summary>
    private MemoryRecord? ConcedeSomething(DialogueRequest request)
    {
        MemoryStore requesterStore = _memories.GetStore(request.Requester);
        MemoryRecord? withheld = _memories.GetStore(request.Partner).Memories
            .Where(memory =>
                memory.Subject is not null &&
                memory.Subject != request.Requester &&
                !requesterStore.KnowsRootEvent(memory.RootEventId))
            .OrderByDescending(memory => memory.EventTime.Tick)
            .ThenByDescending(memory => memory.Id.Value)
            .FirstOrDefault();

        return withheld is null
            ? null
            : _memories.ShareMemory(
                request.Partner,
                request.Requester,
                withheld.Id,
                _clock.Now,
                TransmissionConfidence);
    }

    /// <summary>
    /// Whether a claim is false, judged against what the speaker themselves
    /// remembers. No hidden truth is consulted; the speaker is simply held to
    /// their own recollection.
    /// </summary>
    private bool IsClaimFalse(AlibiClaim claim)
    {
        // Where the speaker actually stood at the moment in question: the last
        // room they remember walking into at or before that time. Asking "did they
        // move anywhere nearby" instead would call almost every claim a lie, since
        // people move rooms all night.
        MemoryRecord? whereTheyWere = _memories.GetStore(claim.Speaker).Memories
            .Where(memory =>
                memory.Kind == MemoryKind.Episodic &&
                memory.Subject == claim.Speaker &&
                memory.EventType == EventType.EnterLocation &&
                memory.Location is { IsEmpty: false } &&
                memory.EventTime.Tick <= claim.ClaimedTime.Tick)
            .OrderByDescending(memory => memory.EventTime.Tick)
            .ThenByDescending(memory => memory.Id.Value)
            .FirstOrDefault();

        // Mirrors the fallback in HandleInquireSchedule: a character with no
        // recollection of moving is held to where they are standing. Without this
        // the two halves disagree and a cover story generated from the current
        // room could never be broken.
        LocationId actual = whereTheyWere?.Location
            ?? _world.GetEntity(claim.Speaker).LogicalLocation;
        return !actual.IsEmpty && actual != claim.ClaimedLocation;
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
            return ResolveChallenge(request, evidence, confrontEvent);
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
