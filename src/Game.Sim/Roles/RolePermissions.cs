using Game.Sim.Locations;

namespace Game.Sim.Roles;

public sealed class RolePermissions
{
    private readonly HashSet<LocationId> _allowedLocations;

    public RolePermissions(RoleId role, IEnumerable<LocationId> allowedLocations)
    {
        if (role.IsEmpty)
        {
            throw new ArgumentException("Role ID cannot be empty.", nameof(role));
        }

        ArgumentNullException.ThrowIfNull(allowedLocations);
        LocationId[] materializedLocations = allowedLocations
            .Distinct()
            .OrderBy(location => location.Value, StringComparer.Ordinal)
            .ToArray();
        if (materializedLocations.Length == 0 || materializedLocations.Any(location => location.IsEmpty))
        {
            throw new ArgumentException(
                "A role must allow at least one valid location.",
                nameof(allowedLocations));
        }

        Role = role;
        AllowedLocations = Array.AsReadOnly(materializedLocations);
        _allowedLocations = [.. materializedLocations];
    }

    public RoleId Role { get; }

    public IReadOnlyList<LocationId> AllowedLocations { get; }

    public bool CanEnter(LocationId location) => _allowedLocations.Contains(location);
}
