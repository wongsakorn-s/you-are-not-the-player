using Game.Sim.Actions;
using Game.Sim.Entities;
using Game.Sim.Events;
using Game.Sim.Locations;
using Game.Sim.Memory;
using Game.Sim.Objects;
using Game.Sim.Patterns;
using Game.Sim.Perception;
using Game.Sim.Routines;
using Game.Sim.Suspicion;
using Game.Sim.Time;
using Game.Sim.World;

namespace Game.Sim.Player;

public sealed class PlayerSessionController
{
    private readonly SimClock _clock;
    private readonly WorldState _world;
    private readonly LocationGraph _graph;
    private readonly CoordinatedNpcMovementExecutor _movementExecutor;
    private readonly InteractionActionHandler _interactions;
    private readonly BoundaryProbeActionHandler _boundaryProbes;
    private readonly DialogueSystem _dialogue;
    private readonly MemorySystem _memories;
    private readonly SuspicionSystem _suspicion;
    private readonly HotelObjectRegistry _objects;
    private readonly ObjectActionHandler _objectActions;
    private EntityId _playerEntity;

    public PlayerSessionController(
        EntityId playerEntity,
        SimClock clock,
        WorldState world,
        LocationGraph graph,
        CoordinatedNpcMovementExecutor movementExecutor,
        InteractionActionHandler interactions,
        BoundaryProbeActionHandler boundaryProbes,
        DialogueSystem dialogue,
        MemorySystem memories,
        SuspicionSystem suspicion,
        HotelObjectRegistry? objects = null,
        ObjectActionHandler? objectActions = null)
    {
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(movementExecutor);
        ArgumentNullException.ThrowIfNull(interactions);
        ArgumentNullException.ThrowIfNull(boundaryProbes);
        ArgumentNullException.ThrowIfNull(dialogue);
        ArgumentNullException.ThrowIfNull(memories);
        ArgumentNullException.ThrowIfNull(suspicion);

        if (playerEntity.IsEmpty)
        {
            throw new ArgumentException("Player entity cannot be empty.", nameof(playerEntity));
        }

        _playerEntity = playerEntity;
        _clock = clock;
        _world = world;
        _graph = graph;
        _movementExecutor = movementExecutor;
        _interactions = interactions;
        _boundaryProbes = boundaryProbes;
        _dialogue = dialogue;
        _memories = memories;
        _suspicion = suspicion;
        _objects = objects ?? HotelObjectRegistry.CreateDefaultHotelObjects();
        _objectActions = objectActions ?? new ObjectActionHandler(
            _world,
            _objects,
            new WorldEventFactory(_clock, new SequentialEventIdGenerator()),
            new WorldEventBuffer(),
            new BehaviorPatternSystem(_clock, new RuleBasedBehaviorPatternDetector(_clock.TicksPerSecond), new WorldEventFactory(_clock, new SequentialEventIdGenerator()), new WorldEventBuffer()),
            _memories);
    }

    public EntityId PlayerEntity => _playerEntity;

    public LocationId CurrentLocation => _world.GetEntity(_playerEntity).LogicalLocation;

    public bool HasActiveMovement => _movementExecutor.IsBusy(_playerEntity);

    public MovementSnapshot? ActiveMovement => _movementExecutor.GetPending(_playerEntity);

    public void SetPlayerEntity(EntityId playerEntity)
    {
        if (playerEntity.IsEmpty)
        {
            throw new ArgumentException("Player entity cannot be empty.", nameof(playerEntity));
        }

        _ = _world.GetEntity(playerEntity);
        _playerEntity = playerEntity;
    }

    public NpcMovementExecution RequestMove(LocationId destination)
    {
        if (destination.IsEmpty)
        {
            throw new ArgumentException("Destination cannot be empty.", nameof(destination));
        }

        return _movementExecutor.Execute(new MoveEntityCommand(_playerEntity, destination));
    }

    public EventActionResult Interact(
        string interactionId,
        InteractionKind kind = InteractionKind.Generic)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(interactionId);
        return _interactions.Execute(new InteractionCommand(_playerEntity, kind, interactionId));
    }

    public EventActionResult ProbeBoundary(string boundaryId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(boundaryId);
        return _boundaryProbes.Execute(new BoundaryProbeCommand(_playerEntity, boundaryId));
    }

    public DialogueOutcome Talk(DialogueRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Requester != _playerEntity)
        {
            throw new ArgumentException(
                $"Dialogue requester must be the active player entity '{_playerEntity.Value}'.",
                nameof(request));
        }

        return _dialogue.Execute(request);
    }

    public DialogueOutcome InquireObject(EntityId partner, string objectId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(objectId);
        return Talk(new DialogueRequest(
            DialogueActionKind.InquireAboutObject,
            _playerEntity,
            partner,
            targetObjectId: objectId));
    }

    public DialogueOutcome ConfrontWithEvidence(EntityId partner, MemoryId evidenceMemoryId)
    {
        return Talk(new DialogueRequest(
            DialogueActionKind.ConfrontEvidence,
            _playerEntity,
            partner,
            confrontingMemoryId: evidenceMemoryId));
    }

    public IReadOnlyList<InteractiveObject> GetPresentObjects() =>
        _objects.GetObjectsInLocation(CurrentLocation);

    public ObjectActionResult InspectObject(string objectId) =>
        _objectActions.Inspect(_playerEntity, objectId);

    public ObjectActionResult TamperObject(string objectId, string? keyId = null) =>
        _objectActions.TamperOrUnlock(_playerEntity, objectId, keyId);

    public IReadOnlyList<EntityId> GetPresentActors()
    {
        LocationId currentLoc = CurrentLocation;
        return _world.Entities
            .Where(entity => entity.LogicalLocation == currentLoc && entity.Id != _playerEntity)
            .Select(entity => entity.Id)
            .OrderBy(id => id.Value, StringComparer.Ordinal)
            .ToArray();
    }

    public IReadOnlyList<LocationId> GetAdjacentLocations()
    {
        return _graph.GetConnections(CurrentLocation)
            .Select(connection => connection.Destination)
            .Distinct()
            .OrderBy(loc => loc.Value, StringComparer.Ordinal)
            .ToArray();
    }

    public PlayerJournal GetJournal()
    {
        EntityState entity = _world.GetEntity(_playerEntity);
        MemoryStore store = _memories.GetStore(_playerEntity);

        var entries = new List<PlayerJournalEntry>(store.Memories.Count);
        foreach (MemoryRecord memory in store.Memories.OrderByDescending(m => m.EventTime.Tick))
        {
            string summary = FormatMemorySummary(memory);
            entries.Add(new PlayerJournalEntry(
                memory.Id,
                memory.Kind,
                memory.Subject,
                memory.EventType,
                memory.Location,
                memory.EventTime,
                memory.InitialConfidence,
                memory.InformationSource,
                memory.RootEventId,
                summary));
        }

        var suspicionSnapshots = new List<SuspicionSnapshot>();
        foreach (EntityState other in _world.Entities.Where(e => e.Id != _playerEntity))
        {
            SuspicionSnapshot snapshot = _suspicion.GetSnapshot(_playerEntity, other.Id, _clock.Now);
            if (snapshot.Evidence.Count > 0)
            {
                suspicionSnapshots.Add(snapshot);
            }
        }

        return new PlayerJournal(
            _playerEntity,
            entity.LogicalLocation,
            _clock.Now,
            entries,
            suspicionSnapshots,
            GetPresentActors(),
            GetAdjacentLocations());
    }

    private static string FormatMemorySummary(MemoryRecord memory)
    {
        string locName = memory.Location is { IsEmpty: false } loc ? loc.Value : "unknown area";
        if (memory.Kind == MemoryKind.Social)
        {
            string source = memory.InformationSource is { IsEmpty: false } s ? s.Value : "someone";
            string subject = memory.Subject is { IsEmpty: false } sub ? sub.Value : "an entity";
            return $"{source} told you: saw {subject} ({memory.EventType}) at {locName} [tick {memory.EventTime.Tick}].";
        }

        string actor = memory.Subject is { IsEmpty: false } act ? act.Value : "an entity";
        return $"You observed: {actor} did {memory.EventType} at {locName} [tick {memory.EventTime.Tick}].";
    }
}
