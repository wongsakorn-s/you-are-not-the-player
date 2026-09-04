using Game.Sim.Cases;
using Game.Sim.Entities;
using Game.Sim.Locations;
using Game.Sim.Roles;
using Game.Sim.Secrets;
using Game.Sim.Time;

namespace Game.Sim.Schedules;

/// <summary>
/// Turns "this character has this kind of secret" into a time and a place in
/// this particular hotel.
/// </summary>
/// <remarks>
/// The seed decides who is hiding what; the setting decides where a theft or a
/// rendezvous would actually happen, which is why the two are separated -
/// <see cref="SessionTruth"/> has to stay usable if the game ever moves to a ship
/// or a village.
/// <para>
/// This is what makes false positives possible at all. Until secrets were staged,
/// nobody but the Player AI ever did anything odd, so "acting strangely means
/// Player" was a rule that actually worked - the exact failure the design warns
/// about in the False Positive section.
/// </para>
/// </remarks>
public static class HotelSecretStaging
{
    /// <summary>
    /// Has to outrank a Work block or nobody ever slips away, and stay under the
    /// weight of reacting to a real suspicion so the cast still notices the player.
    /// </summary>
    public const float SecretUtility = 46.0f;

    private static readonly LocationId Kitchen = new("kitchen");
    private static readonly LocationId Office = new("office");
    private static readonly LocationId Garden = new("garden");
    private static readonly LocationId Hallway = new("hallway");

    /// <param name="roleOf">
    /// Which role each character holds. A rendezvous has to be somewhere both
    /// people can walk into, or the goal is never taken and the secret silently
    /// does not exist.
    /// </param>
    public static SecretPlanRepository Stage(
        IEnumerable<SecretAssignment> secrets,
        Func<EntityId, RoleId> roleOf)
    {
        ArgumentNullException.ThrowIfNull(secrets);
        ArgumentNullException.ThrowIfNull(roleOf);

        var plans = new List<SecretPlan>();
        int index = 0;
        foreach (SecretAssignment secret in secrets
            .OrderBy(secret => secret.Owner.Value, StringComparer.Ordinal))
        {
            plans.Add(Plan(secret, index, roleOf));
            index++;
        }

        return new SecretPlanRepository(plans);
    }

    private static SecretPlan Plan(
        SecretAssignment secret,
        int index,
        Func<EntityId, RoleId> roleOf)
    {
        // Windows are staggered by position so two secrets never sit on top of
        // each other, and the order is ordinal, so a seed stages the same night
        // every time it is replayed.
        int offsetMinutes = index * 40;
        return secret.Behavior switch
        {
            SecretBehaviorKind.Theft => Build(
                secret,
                $"theft-{secret.Owner.Value}",
                // Somewhere with something worth taking, and a reason to be caught.
                index % 2 == 0 ? Kitchen : Office,
                startHour: 0,
                startMinute: offsetMinutes,
                durationMinutes: 45,
                // Taking something means going where you are not allowed; that is
                // the part a witness reports.
                ignoresRolePermissions: true),

            // Meeting quietly is secrecy, not trespass, so this one stays inside
            // what both people are allowed to do - it just happens at an hour and
            // in a company that invites questions.
            SecretBehaviorKind.SecretMeeting => Build(
                secret,
                $"meeting-{secret.Owner.Value}",
                MeetingPlace(secret, index, roleOf),
                startHour: 1,
                startMinute: 30 + offsetMinutes,
                durationMinutes: 40,
                ignoresRolePermissions: false),

            // Being up and about where you have no business at four in the
            // morning is the whole tell, so this ignores role permissions - a
            // manager staged into a guest room simply never went, and the secret
            // silently did not exist.
            _ => Build(
                secret,
                $"nightowl-{secret.Owner.Value}",
                index % 2 == 0 ? Hallway : Garden,
                startHour: 3,
                startMinute: offsetMinutes,
                durationMinutes: 50,
                ignoresRolePermissions: true),
        };
    }

    private static LocationId MeetingPlace(
        SecretAssignment secret,
        int index,
        Func<EntityId, RoleId> roleOf)
    {
        LocationId[] preferred = index % 2 == 0 ? [Garden, Hallway] : [Hallway, Garden];
        EntityId[] attendees = secret.Accomplice is { } accomplice
            ? [secret.Owner, accomplice]
            : [secret.Owner];

        foreach (LocationId candidate in preferred)
        {
            if (attendees.All(attendee =>
                HotelNightRoutines.Permissions(roleOf(attendee)).CanEnter(candidate)))
            {
                return candidate;
            }
        }

        // The corridor is the one room everybody on this shift passes through.
        return Hallway;
    }

    private static SecretPlan Build(
        SecretAssignment secret,
        string id,
        LocationId location,
        int startHour,
        int startMinute,
        int durationMinutes,
        bool ignoresRolePermissions)
    {
        int start = ((startHour * 60) + startMinute) % SimMinuteOfDay.MinutesPerDay;
        int end = (start + durationMinutes) % SimMinuteOfDay.MinutesPerDay;
        EntityId[] participants = secret.Accomplice is { } accomplice
            ? [secret.Owner, accomplice]
            : [secret.Owner];

        return new SecretPlan(
            id,
            secret.Behavior,
            participants,
            new SimMinuteOfDay(start),
            new SimMinuteOfDay(end),
            location,
            SecretUtility,
            ignoresRolePermissions);
    }
}
