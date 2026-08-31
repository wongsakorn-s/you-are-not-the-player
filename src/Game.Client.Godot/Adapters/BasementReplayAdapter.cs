using Game.Sim.Entities;
using Game.Sim.Events;
using Game.Sim.Locations;
using Game.Sim.Scenarios;

namespace Game.Client.Godot.Adapters;

public sealed class BasementReplayAdapter
{
    private const double SecondsPerTick = 0.5;

    private readonly BasementScenarioResult _result;
    private readonly Dictionary<EntityId, LocationId> _locations;
    private readonly List<WorldEvent> _visibleEvents = [];
    private readonly List<WorldEvent> _newEvents = [];
    private double _accumulator;

    public BasementReplayAdapter(BasementScenarioResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        _result = result;
        _locations = result.Actors.ToDictionary(
            actor => actor.Entity,
            _ => BasementScenario.Lobby);
    }

    public bool IsPaused { get; private set; }

    public float Speed { get; private set; } = 1.0f;

    public long CurrentTick { get; private set; }

    public bool IsComplete => CurrentTick >= _result.CompletedAt.Tick;

    public IReadOnlyDictionary<EntityId, LocationId> Locations => _locations;

    public IReadOnlyList<WorldEvent> VisibleEvents => _visibleEvents;

    public BasementScenarioResult Result => _result;

    public bool Update(double delta)
    {
        if (IsPaused || IsComplete)
        {
            return false;
        }

        _accumulator += delta * Speed;
        bool advanced = false;
        while (_accumulator >= SecondsPerTick && !IsComplete)
        {
            _accumulator -= SecondsPerTick;
            Step();
            advanced = true;
        }

        return advanced;
    }

    public void Step()
    {
        if (IsComplete)
        {
            return;
        }

        CurrentTick++;
        foreach (WorldEvent worldEvent in _result.Events.Where(
            candidate => candidate.Time.Tick == CurrentTick))
        {
            _visibleEvents.Add(worldEvent);
            _newEvents.Add(worldEvent);
            if (worldEvent.Type == EventType.EnterLocation)
            {
                _locations[worldEvent.Actor] = worldEvent.Location;
            }
        }
    }

    public void TogglePause() => IsPaused = !IsPaused;

    public IReadOnlyList<WorldEvent> DrainNewEvents()
    {
        WorldEvent[] drained = [.. _newEvents];
        _newEvents.Clear();
        return drained;
    }

    public void SetSpeed(float speed)
    {
        if (!float.IsFinite(speed) || speed <= 0.0f)
        {
            throw new ArgumentOutOfRangeException(nameof(speed), speed, "Speed must be positive.");
        }

        Speed = speed;
    }

    public void Reset()
    {
        CurrentTick = 0;
        IsPaused = false;
        Speed = 1.0f;
        _accumulator = 0.0;
        _visibleEvents.Clear();
        _newEvents.Clear();
        foreach (EntityId actor in _locations.Keys.ToArray())
        {
            _locations[actor] = BasementScenario.Lobby;
        }
    }
}
