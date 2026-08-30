using Game.Sim.Brain;
using Game.Sim.Entities;
using Game.Sim.Needs;
using Game.Sim.Roles;
using Game.Sim.Schedules;
using Game.Sim.Time;

namespace Game.Sim.Routines;

public sealed class NpcRoutineProfile
{
    public NpcRoutineProfile(
        EntityId entity,
        RolePermissions role,
        DailySchedule schedule,
        NeedState needs,
        NeedProfile needProfile,
        NeedDestinations needDestinations)
    {
        if (entity.IsEmpty)
        {
            throw new ArgumentException("Routine entity cannot be empty.", nameof(entity));
        }

        ArgumentNullException.ThrowIfNull(role);
        ArgumentNullException.ThrowIfNull(schedule);
        ArgumentNullException.ThrowIfNull(needs);
        ArgumentNullException.ThrowIfNull(needProfile);
        ArgumentNullException.ThrowIfNull(needDestinations);

        if (schedule.Entries.Any(entry => !role.CanEnter(entry.Location)))
        {
            throw new ArgumentException(
                "Every schedule location must be allowed by the entity role.",
                nameof(schedule));
        }

        if (!role.CanEnter(needDestinations.MealLocation) ||
            !role.CanEnter(needDestinations.RestLocation) ||
            !role.CanEnter(needDestinations.SocialLocation))
        {
            throw new ArgumentException(
                "Every need destination must be allowed by the entity role.",
                nameof(needDestinations));
        }

        Entity = entity;
        Role = role;
        Schedule = schedule;
        Needs = needs;
        NeedProfile = needProfile;
        NeedDestinations = needDestinations;
    }

    public EntityId Entity { get; }

    public RolePermissions Role { get; }

    public DailySchedule Schedule { get; }

    public NeedState Needs { get; }

    public NeedProfile NeedProfile { get; }

    public NeedDestinations NeedDestinations { get; }

    internal void ApplyRecovery(GoalType goal, SimDelta delta, int ticksPerSecond)
    {
        double elapsedHours = (double)delta.Ticks / ticksPerSecond / 3_600.0;

        switch (goal)
        {
            case GoalType.Eat:
                Needs.Satisfy(
                    NeedType.Hunger,
                    NeedProfile.EatingRecoveryPerHour * elapsedHours);
                break;
            case GoalType.Sleep:
                Needs.Satisfy(
                    NeedType.Fatigue,
                    NeedProfile.SleepingRecoveryPerHour * elapsedHours);
                break;
            case GoalType.Socialize:
                Needs.Satisfy(
                    NeedType.Social,
                    NeedProfile.SocialRecoveryPerHour * elapsedHours);
                break;
        }
    }
}
