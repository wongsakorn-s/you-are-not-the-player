using Game.Sim.Entities;
using Game.Sim.Events;
using Game.Sim.Locations;
using Game.Sim.Patterns;
using Game.Sim.Time;
using Game.Sim.World;

namespace Game.Sim.Roles;

/// <summary>
/// Notices when somebody is not where their job says they should be.
/// </summary>
/// <remarks>
/// The producer that was missing. RuleBasedBehaviorPatternDetector handles
/// RoleDutyMissed, the suspicion rules score it, and the case file can render it -
/// but nothing in the game ever emitted one, so RoleNeglect could not fire and
/// wandering off your post cost nothing.
/// <para>
/// It reports on the human host as well as the cast, deliberately: investigating
/// means leaving the front desk, and the world is supposed to notice that.
/// </para>
/// </remarks>
public sealed class RoleDutySystem
{
    /// <summary>
    /// How long somebody can be away from their post before it is worth
    /// remarking on. Short enough that a detour is noticed, long enough that
    /// walking through a doorway is not.
    /// </summary>
    public const long GraceTicks = 20;

    /// <summary>
    /// How long before the same absence is reported again. RoleNeglect wants a
    /// pattern of separate lapses, not one long one counted many times.
    /// </summary>
    public const long RepeatTicks = 45;

    private readonly SimClock _clock;
    private readonly WorldState _world;
    private readonly WorldEventFactory _events;
    private readonly IWorldEventBuffer _buffer;
    private readonly BehaviorPatternSystem _patterns;
    private readonly Dictionary<EntityId, RoleId> _roles = [];
    private readonly Dictionary<EntityId, long> _awaySince = [];
    private readonly Dictionary<EntityId, long> _lastReported = [];

    private static readonly EventTag[] DutyTags = [EventTag.Visible, EventTag.Audible];

    public RoleDutySystem(
        SimClock clock,
        WorldState world,
        WorldEventFactory events,
        IWorldEventBuffer buffer,
        BehaviorPatternSystem patterns)
    {
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(buffer);
        ArgumentNullException.ThrowIfNull(patterns);
        _clock = clock;
        _world = world;
        _events = events;
        _buffer = buffer;
        _patterns = patterns;
    }

    public void Register(EntityId entity, RoleId role)
    {
        if (entity.IsEmpty)
        {
            throw new ArgumentException("Duty entity cannot be empty.", nameof(entity));
        }

        _roles[entity] = role;
    }

    public IReadOnlyDictionary<EntityId, RoleId> Roles => _roles;

    /// <summary>
    /// Publishes a RoleDutyMissed for everyone who has been off their post long
    /// enough for it to mean something.
    /// </summary>
    public IReadOnlyList<WorldEvent> Tick()
    {
        var published = new List<WorldEvent>();
        long now = _clock.Now.Tick;
        SimMinuteOfDay timeOfDay = _clock.TimeOfDay;

        // Ordinal order so a tick that catches two people reports them in the same
        // sequence on every replay of the same seed.
        foreach (EntityId entity in _roles.Keys
            .OrderBy(entity => entity.Value, StringComparer.Ordinal))
        {
            LocationId? duty = Schedules.HotelNightRoutines.DutyLocation(_roles[entity], timeOfDay);
            if (duty is not { IsEmpty: false } expected)
            {
                // Off shift, or a role without duties. Nothing to neglect.
                _ = _awaySince.Remove(entity);
                continue;
            }

            LocationId actual = _world.GetEntity(entity).LogicalLocation;
            if (actual == expected)
            {
                _ = _awaySince.Remove(entity);
                continue;
            }

            if (!_awaySince.TryGetValue(entity, out long since))
            {
                _awaySince[entity] = now;
                continue;
            }

            if (now - since < GraceTicks)
            {
                continue;
            }

            if (_lastReported.TryGetValue(entity, out long last) && now - last < RepeatTicks)
            {
                continue;
            }

            _lastReported[entity] = now;
            WorldEvent worldEvent = _events.Create(
                entity,
                EventType.RoleDutyMissed,
                actual,
                target: null,
                tags: DutyTags,
                payload: new RoleDutyPayload($"{_roles[entity].Value}:{expected.Value}"));
            _buffer.Publish(worldEvent);
            _ = _patterns.Process(worldEvent);
            published.Add(worldEvent);
        }

        return published;
    }
}
