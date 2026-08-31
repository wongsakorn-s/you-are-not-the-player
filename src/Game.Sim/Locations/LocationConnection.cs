namespace Game.Sim.Locations;

public sealed record LocationConnection
{
    public LocationConnection(
        LocationId origin,
        LocationId destination,
        string portalId,
        bool requiresAccess = false)
    {
        if (origin.IsEmpty)
        {
            throw new ArgumentException("Origin location cannot be empty.", nameof(origin));
        }

        if (destination.IsEmpty)
        {
            throw new ArgumentException("Destination location cannot be empty.", nameof(destination));
        }

        if (origin == destination)
        {
            throw new ArgumentException("A connection must link different locations.", nameof(destination));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(portalId);
        Origin = origin;
        Destination = destination;
        PortalId = portalId.Trim();
        RequiresAccess = requiresAccess;
    }

    public LocationId Origin { get; }

    public LocationId Destination { get; }

    public string PortalId { get; }

    public bool RequiresAccess { get; }
}
