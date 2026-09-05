using Game.Sim.Events;
using Game.Sim.Locations;

namespace Game.Sim.PlayerAi;

public sealed record CompletionObjective
{
    public CompletionObjective(
        string id,
        LocationId location,
        InteractionKind interactionKind,
        string? interactionId = null,
        bool ignoresRolePermissions = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        if (location.IsEmpty)
        {
            throw new ArgumentException("Completion location cannot be empty.", nameof(location));
        }

        if (!Enum.IsDefined(interactionKind))
        {
            throw new ArgumentOutOfRangeException(
                nameof(interactionKind),
                interactionKind,
                "Unknown interaction kind.");
        }

        Id = id.Trim();
        Location = location;
        InteractionKind = interactionKind;
        InteractionId = string.IsNullOrWhiteSpace(interactionId) ? Id : interactionId.Trim();
        IgnoresRolePermissions = ignoresRolePermissions;
    }

    /// <summary>
    /// Whether the character's job is allowed to stop them going there.
    /// </summary>
    /// <remarks>
    /// A plan that respects the role silently stalls the moment it names a room
    /// that role cannot enter: the goal is filtered out, the objective never
    /// completes, and the rest of the night never happens. Somebody playing a
    /// game does not check whether their character is rostered for the kitchen.
    /// </remarks>
    public bool IgnoresRolePermissions { get; }

    /// <summary>Orders the plan and must be unique within it.</summary>
    public string Id { get; }

    /// <summary>
    /// What the world sees being handled. Several objectives can share one, which
    /// is how a plan expresses doing the same ordinary thing over and over.
    /// </summary>
    public string InteractionId { get; }

    public LocationId Location { get; }

    public InteractionKind InteractionKind { get; }
}
