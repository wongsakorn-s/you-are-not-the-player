using Game.Sim.Entities;
using Game.Sim.Locations;
using Game.Sim.Time;

namespace Game.Sim.Secrets;

public sealed class SecretPlan
{
    public SecretPlan(
        string id,
        SecretBehaviorKind behavior,
        IEnumerable<EntityId> participants,
        SimMinuteOfDay start,
        SimMinuteOfDay end,
        LocationId location,
        float utility,
        bool ignoresRolePermissions = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(participants);
        if (!Enum.IsDefined(behavior))
        {
            throw new ArgumentOutOfRangeException(nameof(behavior), behavior, "Unknown secret behavior.");
        }

        if (start == end)
        {
            throw new ArgumentException("A secret plan must have a non-zero duration.", nameof(end));
        }

        if (location.IsEmpty)
        {
            throw new ArgumentException("Secret plan location cannot be empty.", nameof(location));
        }

        if (!float.IsFinite(utility) || utility < 0.0f)
        {
            throw new ArgumentOutOfRangeException(
                nameof(utility),
                utility,
                "Secret plan utility must be a finite non-negative number.");
        }

        EntityId[] materializedParticipants = participants.ToArray();
        int expectedParticipants = behavior == SecretBehaviorKind.SecretMeeting ? 2 : 1;
        if (materializedParticipants.Length != expectedParticipants ||
            materializedParticipants.Any(participant => participant.IsEmpty) ||
            materializedParticipants.Distinct().Count() != materializedParticipants.Length)
        {
            throw new ArgumentException(
                $"Behavior '{behavior}' requires {expectedParticipants} distinct valid participant(s).",
                nameof(participants));
        }

        Id = id.Trim();
        Behavior = behavior;
        Participants = Array.AsReadOnly(materializedParticipants);
        Start = start;
        End = end;
        Location = location;
        Utility = utility;
        IgnoresRolePermissions = ignoresRolePermissions;
    }

    public string Id { get; }

    public SecretBehaviorKind Behavior { get; }

    public IReadOnlyList<EntityId> Participants { get; }

    public SimMinuteOfDay Start { get; }

    public SimMinuteOfDay End { get; }

    public LocationId Location { get; }

    public float Utility { get; }

    public bool IgnoresRolePermissions { get; }

    public bool IsActive(SimMinuteOfDay time) => Start < End
        ? time >= Start && time < End
        : time >= Start || time < End;
}
