using Game.Sim.Locations;
using Game.Sim.Roles;

namespace Game.Sim.Schedules;

/// <summary>
/// Who confides in whom, and where each person goes when they would rather not
/// be near somebody.
/// </summary>
/// <remarks>
/// Only two characters used to have a suspicion profile at all, and one of them
/// had nobody to tell, so four of the six could not react to anything they saw
/// and a rumour had almost nowhere to travel. The emergent chain the design is
/// built around - one person sees something, tells another, who starts watching -
/// needs a graph that actually connects.
/// <para>
/// Keyed by role rather than by character so the shape survives a different cast,
/// and so it reads as the hotel's chain of command rather than as six special
/// cases.
/// </para>
/// </remarks>
public static class HotelSocialGraph
{
    private static readonly LocationId Lobby = new("lobby");
    private static readonly LocationId Hallway = new("hallway");
    private static readonly LocationId Kitchen = new("kitchen");
    private static readonly LocationId Room201 = new("room-201");
    private static readonly LocationId SecurityRoom = new("security-room");
    private static readonly LocationId Office = new("office");

    /// <summary>
    /// Where this role retreats to when avoiding someone. Must be somewhere the
    /// role is allowed to be - SuspicionDrivenGoalSource throws otherwise, which
    /// is a crash rather than a quiet failure, so the test alongside this checks
    /// it for every role.
    /// </summary>
    public static LocationId SafePlace(RoleId role) =>
        role == HotelNightRoutines.Security ? SecurityRoom
        : role == HotelNightRoutines.Manager ? Office
        : role == HotelNightRoutines.Cook ? Kitchen
        : role == HotelNightRoutines.Guest ? Room201
        : role == HotelNightRoutines.Cleaner ? Hallway
        : Lobby;

    /// <summary>
    /// Who this role would take a worry to. Everyone has at least one ear, or a
    /// suspicion dies with the person who formed it.
    /// </summary>
    /// <remarks>
    /// The edges are directed and the whole graph has to be strongly connected. A
    /// first draft had the receptionist, security and manager confiding only in
    /// each other, which sealed management into a clique that word could enter but
    /// never leave - the cleaner, the cook and the guest could never hear anything
    /// at all.
    /// </remarks>
    public static IReadOnlyList<RoleId> Confidants(RoleId role) =>
        role == HotelNightRoutines.Receptionist
            ? [HotelNightRoutines.Security, HotelNightRoutines.Guest]
        : role == HotelNightRoutines.Cleaner
            ? [HotelNightRoutines.Cook, HotelNightRoutines.Security]
        : role == HotelNightRoutines.Security
            ? [HotelNightRoutines.Manager, HotelNightRoutines.Receptionist]
        : role == HotelNightRoutines.Cook
            ? [HotelNightRoutines.Manager, HotelNightRoutines.Cleaner]
        : role == HotelNightRoutines.Manager
            ? [HotelNightRoutines.Receptionist, HotelNightRoutines.Security]
        // A guest has no colleagues, but they do chat to whoever comes to do the
        // room - which is how anything the guest floor sees gets into the staff.
        : role == HotelNightRoutines.Guest
            ? [HotelNightRoutines.Cleaner]
        : [];
}
