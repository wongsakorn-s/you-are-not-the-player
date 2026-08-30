using Game.Sim.Entities;
using Game.Sim.Routines;
using Game.Sim.Time;

namespace Game.Sim.Brain;

public sealed class NpcDecisionContext
{
    public NpcDecisionContext(
        EntityState entity,
        NpcRoutineProfile profile,
        SimMinuteOfDay timeOfDay)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentNullException.ThrowIfNull(profile);

        if (entity.Id != profile.Entity)
        {
            throw new ArgumentException(
                "Decision entity and routine profile must reference the same ID.",
                nameof(profile));
        }

        Entity = entity;
        Profile = profile;
        TimeOfDay = timeOfDay;
    }

    public EntityState Entity { get; }

    public NpcRoutineProfile Profile { get; }

    public SimMinuteOfDay TimeOfDay { get; }
}
