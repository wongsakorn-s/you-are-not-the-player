using Game.Sim.Locations;
using Game.Sim.Needs;
using Game.Sim.Roles;
using Game.Sim.Routines;

namespace Game.Sim.Schedules;

/// <summary>
/// Hunger and tiredness, sized so they arrive during a single night shift.
/// </summary>
/// <remarks>
/// Needs were switched off everywhere: every routine profile carried a rate of
/// zero, and NeedGoalSource - the thing that would read them - was not in any
/// brain. Turning them on adds a second innocent reason to leave your post,
/// which is worth more to the game than the realism: at four in the morning
/// somebody walking away from their station might be the Player, might be
/// hiding something, or might just be hungry.
/// </remarks>
public static class HotelNeeds
{
    /// <summary>Minutes from 23:00 to 05:00; one tick is one of them.</summary>
    private const double ShiftMinutes = 360.0;

    /// <summary>
    /// NeedState.Advance measures in hours of 3600 ticks, so a rate has to be
    /// scaled up by this much to mean anything inside a 360-tick night.
    /// </summary>
    private const double MinutesPerRateHour = 3_600.0;

    /// <summary>Hunger reaches NeedGoalSource's threshold around 02:30.</summary>
    private const double HungerAtMinute = 210.0;

    /// <summary>Fatigue reaches its threshold around 04:00, near the end.</summary>
    private const double FatigueAtMinute = 300.0;

    public static NeedProfile Profile() => new(
        new NeedRates(
            hungerPerHour: RateToReach(0.65, HungerAtMinute),
            fatiguePerHour: RateToReach(0.75, FatigueAtMinute),
            // Deliberately out of reach in one night. The manager already has a
            // Socialize block, and a cast that keeps wandering off to chat would
            // drown the signal the player is trying to read.
            socialPerHour: RateToReach(0.80, ShiftMinutes * 3.0)),
        // A break is a detour, not the rest of the shift: roughly twenty minutes
        // to recover from either.
        eatingRecoveryPerHour: RateToReach(1.0, 20.0),
        sleepingRecoveryPerHour: RateToReach(1.0, 20.0),
        socialRecoveryPerHour: RateToReach(1.0, 20.0));

    /// <summary>
    /// Where each role goes when a need finally outweighs the job. Every one of
    /// these has to be somewhere the role may actually enter, or the goal is taken
    /// and the walk never happens.
    /// </summary>
    public static NeedDestinations Destinations(RoleId role) => new(
        MealPlace(role),
        HotelSocialGraph.SafePlace(role),
        new LocationId("lobby"));

    private static LocationId MealPlace(RoleId role) =>
        // Security and the manager are not kitchen staff; they eat at the desk.
        role == HotelNightRoutines.Security || role == HotelNightRoutines.Manager
            ? new LocationId("lobby")
        : role == HotelNightRoutines.Guest
            ? new LocationId("lobby")
        : new LocationId("kitchen");

    private static double RateToReach(double urgency, double minutes) =>
        urgency * MinutesPerRateHour / minutes;
}
