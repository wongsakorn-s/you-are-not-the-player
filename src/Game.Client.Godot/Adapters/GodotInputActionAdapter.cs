using System.Globalization;
using Game.Sim.Actions;
using Game.Sim.Entities;
using Game.Sim.Events;
using Game.Sim.Locations;
using Game.Sim.Patterns;
using Game.Sim.Scenarios;
using Game.Sim.Time;
using Game.Sim.World;

namespace Game.Client.Godot.Adapters;

public sealed class GodotInputActionAdapter
{
    private readonly SimClock _clock;
    private readonly WorldState _world;
    private readonly WorldEventBuffer _buffer;
    private readonly MoveEntityActionHandler _movement;
    private readonly InteractionActionHandler _interactions;
    private int _interactionSequence;

    public GodotInputActionAdapter(
        BasementScenarioResult result,
        IEventIdGenerator? eventIds = null)
    {
        ArgumentNullException.ThrowIfNull(result);
        _world = new WorldState();
        _world.AddLocation(new LocationState(BasementScenario.Lobby));
        _world.AddLocation(new LocationState(BasementScenario.Basement, isRestricted: true));
        foreach (ScenarioActorSnapshot actor in result.Actors)
        {
            _world.AddEntity(new EntityState(actor.Entity, actor.LogicalLocation));
        }

        _clock = new SimClock(ticksPerSecond: 1);
        _clock.Advance(new SimDelta(result.CompletedAt.Tick));
        _buffer = new WorldEventBuffer();
        var eventFactory = new WorldEventFactory(
            _clock,
            eventIds ?? new SequentialEventIdGenerator(
                checked(result.Events.Max(worldEvent => worldEvent.Id.Value) + 1)));
        var patterns = new BehaviorPatternSystem(
            _clock,
            new RuleBasedBehaviorPatternDetector(_clock.TicksPerSecond),
            eventFactory,
            _buffer);
        _movement = new MoveEntityActionHandler(_world, eventFactory, _buffer);
        _interactions = new InteractionActionHandler(_world, eventFactory, _buffer, patterns);
    }

    public IReadOnlyList<WorldEvent> Interact(
        EntityId actor,
        LocationId logicalLocation,
        string? interactionId = null)
    {
        _clock.AdvanceOneTick();
        if (_world.GetEntity(actor).LogicalLocation != logicalLocation)
        {
            _ = _movement.Execute(new MoveEntityCommand(actor, logicalLocation));
        }

        _interactionSequence++;
        _ = _interactions.Execute(new InteractionCommand(
            actor,
            InteractionKind.Generic,
            interactionId ??
            $"godot-interact-{_interactionSequence.ToString(CultureInfo.InvariantCulture)}"));
        return _buffer.Drain();
    }
}
