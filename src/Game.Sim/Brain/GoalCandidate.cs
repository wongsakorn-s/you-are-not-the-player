using Game.Sim.Locations;

namespace Game.Sim.Brain;

public sealed class GoalCandidate
{
    public GoalCandidate(
        GoalType type,
        LocationId destination,
        float baseUtility,
        IEnumerable<UtilityReason>? reasons = null)
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
    }

    public GoalType Type { get; }

    public LocationId Destination { get; }

    public float BaseUtility { get; }

    public IReadOnlyList<UtilityReason> Reasons { get; }

    public float TotalUtility { get; }
}
