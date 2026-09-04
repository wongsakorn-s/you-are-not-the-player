using Game.Sim.Locations;
using Game.Sim.Roles;
using Game.Sim.Time;

namespace Game.Sim.Schedules;

/// <summary>
/// What a night shift at the hotel is supposed to look like.
/// </summary>
/// <remarks>
/// The first design pillar is Observable Normality: the player can only notice
/// that somebody is out of place if there is a place they are supposed to be.
/// Every character used to carry a schedule of Idle for all twenty-four hours,
/// which meant there was no normal to deviate from, RoleNeglect could never fire,
/// and RoleDeviation suspicion had nothing to measure against.
/// <para>
/// This table is the obvious next thing to move into content JSON alongside
/// characters.json; it lives in code for now so the shape can settle first.
/// </para>
/// </remarks>
public static class HotelNightRoutines
{
    public static readonly RoleId Receptionist = new("receptionist");
    public static readonly RoleId Cleaner = new("cleaner");
    public static readonly RoleId Security = new("security");
    public static readonly RoleId Cook = new("cook");
    public static readonly RoleId Manager = new("manager");
    public static readonly RoleId Guest = new("guest");

    private static readonly LocationId Lobby = new("lobby");
    private static readonly LocationId Hallway = new("hallway");
    private static readonly LocationId Kitchen = new("kitchen");
    private static readonly LocationId Room201 = new("room-201");
    private static readonly LocationId Garden = new("garden");
    private static readonly LocationId SecurityRoom = new("security-room");
    private static readonly LocationId Office = new("office");

    /// <summary>
    /// Where a role is expected to be while on duty. Being somewhere else during a
    /// Work block is what <see cref="Game.Sim.Events.EventType.RoleDutyMissed"/>
    /// reports, so this is the answer to "should you be here?".
    /// </summary>
    public static LocationId? DutyLocation(RoleId role, SimMinuteOfDay time)
    {
        ScheduleEntry? entry = For(role).GetEntry(time);
        return entry?.Activity == RoutineActivity.Work ? entry.Location : null;
    }

    /// <summary>
    /// Rooms a role has a reason to be in. Everything else reads as out of place,
    /// which is what the restricted-area suspicion rule keys on.
    /// </summary>
    public static RolePermissions Permissions(RoleId role) => role switch
    {
        // The receptionist owns the front of house and nothing behind it.
        _ when role == Receptionist => new RolePermissions(
            role, [Lobby, Hallway, Garden, Kitchen]),

        // Cleaning takes you almost everywhere guests can see, and nowhere else.
        _ when role == Cleaner => new RolePermissions(
            role, [Lobby, Hallway, Room201, Kitchen, Garden]),

        // The only role with a legitimate reason to be in the camera room.
        _ when role == Security => new RolePermissions(
            role, [Lobby, Hallway, SecurityRoom, Garden]),

        _ when role == Cook => new RolePermissions(
            role, [Kitchen, Hallway, Lobby]),

        _ when role == Manager => new RolePermissions(
            role, [Office, Lobby, Hallway]),

        // A guest has no duties and no business past the guest floor, which is
        // exactly why a guest wandering is worth noticing.
        _ when role == Guest => new RolePermissions(
            role, [Room201, Hallway, Lobby, Garden]),

        _ => new RolePermissions(role, [Lobby, Hallway]),
    };

    /// <summary>
    /// A full day, because the clock does not stop at dawn. The shift itself runs
    /// 23:00 to 05:00; the daytime blocks keep behaviour defined either side of it.
    /// </summary>
    public static DailySchedule For(RoleId role) => role switch
    {
        // The desk is home, but a night receptionist still checks the corridor and
        // the outside door. The host needs a rhythm of their own, or "George left
        // the lobby" would never read as anything.
        _ when role == Receptionist => Build(
            (23, 0, 0, 30, RoutineActivity.Work, Lobby),
            (0, 30, 1, 0, RoutineActivity.Work, Hallway),
            (1, 0, 2, 0, RoutineActivity.Work, Lobby),
            (2, 0, 2, 30, RoutineActivity.Eat, Kitchen),
            (2, 30, 3, 30, RoutineActivity.Work, Lobby),
            (3, 30, 4, 0, RoutineActivity.Work, Garden),
            (4, 0, 5, 0, RoutineActivity.Work, Lobby),
            (5, 0, 23, 0, RoutineActivity.Rest, Lobby)),

        // Rounds rather than a post: hallway, then the guest floor, then back.
        _ when role == Cleaner => Build(
            (23, 0, 0, 30, RoutineActivity.Work, Hallway),
            (0, 30, 2, 0, RoutineActivity.Work, Room201),
            (2, 0, 2, 30, RoutineActivity.Rest, Lobby),
            (2, 30, 4, 0, RoutineActivity.Work, Kitchen),
            (4, 0, 5, 0, RoutineActivity.Work, Hallway),
            (5, 0, 23, 0, RoutineActivity.Rest, Lobby)),

        _ when role == Security => Build(
            (23, 0, 1, 0, RoutineActivity.Work, SecurityRoom),
            (1, 0, 1, 30, RoutineActivity.Work, Hallway),
            (1, 30, 3, 0, RoutineActivity.Work, SecurityRoom),
            (3, 0, 3, 30, RoutineActivity.Work, Garden),
            (3, 30, 5, 0, RoutineActivity.Work, SecurityRoom),
            (5, 0, 23, 0, RoutineActivity.Rest, Lobby)),

        // Night prep, with one run down the corridor to bring stock through.
        _ when role == Cook => Build(
            (23, 0, 0, 0, RoutineActivity.Work, Kitchen),
            (0, 0, 0, 30, RoutineActivity.Work, Hallway),
            (0, 30, 1, 30, RoutineActivity.Work, Kitchen),
            (1, 30, 2, 0, RoutineActivity.Rest, Lobby),
            (2, 0, 5, 0, RoutineActivity.Work, Kitchen),
            (5, 0, 23, 0, RoutineActivity.Rest, Lobby)),

        _ when role == Manager => Build(
            (23, 0, 0, 30, RoutineActivity.Work, Lobby),
            (0, 30, 3, 0, RoutineActivity.Work, Office),
            (3, 0, 3, 30, RoutineActivity.Socialize, Lobby),
            (3, 30, 5, 0, RoutineActivity.Work, Office),
            (5, 0, 23, 0, RoutineActivity.Rest, Office)),

        // No Work blocks at all: a guest cannot neglect a duty they do not have.
        _ when role == Guest => Build(
            (23, 0, 0, 0, RoutineActivity.Socialize, Lobby),
            (0, 0, 5, 0, RoutineActivity.Sleep, Room201),
            (5, 0, 23, 0, RoutineActivity.Rest, Room201)),

        _ => Build((0, 0, 0, 0, RoutineActivity.Idle, Lobby)),
    };

    private static DailySchedule Build(
        params (int StartHour, int StartMinute, int EndHour, int EndMinute,
            RoutineActivity Activity, LocationId Location)[] blocks) =>
        new(blocks.Select(block => new ScheduleEntry(
            SimMinuteOfDay.FromHourMinute(block.StartHour, block.StartMinute),
            SimMinuteOfDay.FromHourMinute(block.EndHour, block.EndMinute),
            block.Activity,
            block.Location,
            // Being where you are meant to be has to outweigh idling, or nobody
            // moves; it must not outweigh reacting to a real suspicion, or the
            // cast would ignore the player entirely.
            utility: block.Activity == RoutineActivity.Work ? 38.0f : 18.0f)));
}
