using Game.Sim.Actions;
using Game.Sim.Brain;
using Game.Sim.Entities;
using Game.Sim.Time;
using Game.Sim.World;

namespace Game.Sim.Routines;

public sealed class NpcRoutineSystem
{
    private readonly SimClock _clock;
    private readonly WorldState _world;
    private readonly MoveEntityActionHandler _movement;
    private readonly UtilityNpcBrain _brain;
    private readonly Dictionary<EntityId, NpcRoutineProfile> _profiles = [];

    public NpcRoutineSystem(
        SimClock clock,
        WorldState world,
        MoveEntityActionHandler movement,
        UtilityNpcBrain brain)
    {
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(movement);
        ArgumentNullException.ThrowIfNull(brain);
        _clock = clock;
        _world = world;
        _movement = movement;
        _brain = brain;
    }

    public void Register(NpcRoutineProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        EntityState entity = _world.GetEntity(profile.Entity);
        if (!profile.Role.CanEnter(entity.LogicalLocation))
        {
            throw new InvalidOperationException(
                $"Entity '{entity.Id}' starts in a location forbidden by role '{profile.Role.Role}'.");
        }

        if (!_profiles.TryAdd(profile.Entity, profile))
        {
            throw new InvalidOperationException(
                $"Entity '{profile.Entity}' already has a routine profile.");
        }
    }

    public IReadOnlyList<NpcRoutineDecision> Tick(SimDelta delta)
    {
        _clock.Advance(delta);
        var decisions = new List<NpcRoutineDecision>(_profiles.Count);

        foreach (NpcRoutineProfile profile in _profiles.Values
            .OrderBy(profile => profile.Entity.Value, StringComparer.Ordinal))
        {
            profile.Needs.Advance(delta, _clock.TicksPerSecond, profile.NeedProfile.GrowthRates);
            EntityState entity = _world.GetEntity(profile.Entity);
            var context = new NpcDecisionContext(entity, profile, _clock.TimeOfDay);
            GoalCandidate goal = _brain.SelectGoal(context);

            if (!profile.Role.CanEnter(goal.Destination))
            {
                throw new InvalidOperationException(
                    $"Goal '{goal.Type}' selected forbidden location '{goal.Destination}'.");
            }

            bool moved = entity.LogicalLocation != goal.Destination &&
                _movement.Execute(new MoveEntityCommand(entity.Id, goal.Destination));
            if (entity.LogicalLocation == goal.Destination)
            {
                profile.ApplyRecovery(goal.Type, delta, _clock.TicksPerSecond);
            }

            decisions.Add(new NpcRoutineDecision(_clock.Now, entity.Id, goal, moved));
        }

        return decisions;
    }
}
