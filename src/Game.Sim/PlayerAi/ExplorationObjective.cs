using Game.Sim.Locations;

namespace Game.Sim.PlayerAi;

public sealed record ExplorationObjective
{
    public ExplorationObjective(
        string id,
        LocationId location,
        string boundaryId,
        bool ignoresRolePermissions = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        if (location.IsEmpty)
        {
            throw new ArgumentException("Exploration location cannot be empty.", nameof(location));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(boundaryId);
        Id = id.Trim();
        Location = location;
        BoundaryId = boundaryId.Trim();
        IgnoresRolePermissions = ignoresRolePermissions;
    }

    public string Id { get; }

    public LocationId Location { get; }

    public string BoundaryId { get; }

    public bool IgnoresRolePermissions { get; }
}
