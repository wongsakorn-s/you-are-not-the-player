using Game.Sim.Entities;
using Game.Sim.Locations;

namespace Game.Sim.Actions;

public sealed class PortalAccessPolicy : ILocationAccessPolicy
{
    private readonly HashSet<string> _accessiblePortals = new(StringComparer.Ordinal);

    public bool CanTraverse(EntityId actor, LocationConnection connection)
    {
        _ = actor;
        ArgumentNullException.ThrowIfNull(connection);
        return !connection.RequiresAccess || _accessiblePortals.Contains(connection.PortalId);
    }

    public void SetAccess(string portalId, bool isAccessible)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(portalId);
        if (isAccessible)
        {
            _accessiblePortals.Add(portalId.Trim());
        }
        else
        {
            _accessiblePortals.Remove(portalId.Trim());
        }
    }
}
