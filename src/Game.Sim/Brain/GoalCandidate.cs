using Game.Sim.Entities;
using Game.Sim.Locations;

namespace Game.Sim.Brain;

public sealed class GoalCandidate
{
    public GoalCandidate(
        GoalType type,
        LocationId destination,
        float baseUtility,
        IEnumerable<UtilityReason>? reasons = null,
        bool ignoresRolePermissions = false,
        string? intentId = null,
        EntityId? target = null,
        EntityId? interactionPartner = null)
    {
        if (!Enum.IsDefined(type))
        {
            throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown goal type.");
        }

        if (destination.IsEmpty)
        {
            throw new ArgumentException("Goal destination cannot be empty.", nameof(destination));
        }

        if (!float.IsFinite(baseUtility))
        {
            throw new ArgumentOutOfRangeException(
                nameof(baseUtility),
                baseUtility,
                "Base utility must be finite.");
        }

        if (intentId is not null && string.IsNullOrWhiteSpace(intentId))
        {
            throw new ArgumentException("Goal intent ID cannot be blank.", nameof(intentId));
        }

        if (target is { IsEmpty: true })
        {
            throw new ArgumentException("Goal target cannot be empty.", nameof(target));
        }

        if (interactionPartner is { IsEmpty: true })
        {
            throw new ArgumentException(
                "Goal interaction partner cannot be empty.",
                nameof(interactionPartner));
        }

        UtilityReason[] materializedReasons = reasons?.ToArray() ?? [];
        float totalUtility = baseUtility + materializedReasons.Sum(reason => reason.Weight);
        if (!float.IsFinite(totalUtility))
        {
            throw new ArgumentOutOfRangeException(
                nameof(reasons),
                "Total utility must be finite.");
        }

        Type = type;
        Destination = destination;
        BaseUtility = baseUtility;
        Reasons = Array.AsReadOnly(materializedReasons);
        TotalUtility = totalUtility;
        IgnoresRolePermissions = ignoresRolePermissions;
        IntentId = intentId?.Trim();
        Target = target;
        InteractionPartner = interactionPartner;
    }

    public GoalType Type { get; }

    public LocationId Destination { get; }

    public float BaseUtility { get; }

    public IReadOnlyList<UtilityReason> Reasons { get; }

    public float TotalUtility { get; }

    public bool IgnoresRolePermissions { get; }

    public string? IntentId { get; }

    public EntityId? Target { get; }

    public EntityId? InteractionPartner { get; }
}
