namespace Game.Sim.Locations;

public sealed class LocationGraph
{
    private readonly HashSet<LocationId> _locations = [];
    private readonly Dictionary<LocationId, List<LocationConnection>> _connections = [];

    public void AddLocation(LocationId location)
    {
        if (location.IsEmpty)
        {
            throw new ArgumentException("Location ID cannot be empty.", nameof(location));
        }

        if (!_locations.Add(location))
        {
            throw new InvalidOperationException($"Location '{location}' already exists in the graph.");
        }

        _connections.Add(location, []);
    }

    public void ConnectBidirectional(
        LocationId first,
        LocationId second,
        string portalId,
        bool requiresAccess = false)
    {
        EnsureKnown(first);
        EnsureKnown(second);
        AddDirected(new LocationConnection(first, second, portalId, requiresAccess));
        AddDirected(new LocationConnection(second, first, portalId, requiresAccess));
    }

    public LocationRoute? FindRoute(LocationId origin, LocationId destination)
    {
        EnsureKnown(origin);
        EnsureKnown(destination);
        if (origin == destination)
        {
            return new LocationRoute([origin], []);
        }

        var frontier = new Queue<LocationId>();
        var visited = new HashSet<LocationId> { origin };
        var previous = new Dictionary<LocationId, LocationConnection>();
        frontier.Enqueue(origin);

        while (frontier.Count > 0)
        {
            LocationId current = frontier.Dequeue();
            foreach (LocationConnection connection in _connections[current]
                         .OrderBy(edge => edge.Destination.Value, StringComparer.Ordinal)
                         .ThenBy(edge => edge.PortalId, StringComparer.Ordinal))
            {
                if (!visited.Add(connection.Destination))
                {
                    continue;
                }

                previous.Add(connection.Destination, connection);
                if (connection.Destination == destination)
                {
                    return BuildRoute(origin, destination, previous);
                }

                frontier.Enqueue(connection.Destination);
            }
        }

        return null;
    }

    private static LocationRoute BuildRoute(
        LocationId origin,
        LocationId destination,
        IReadOnlyDictionary<LocationId, LocationConnection> previous)
    {
        var reversed = new List<LocationConnection>();
        LocationId cursor = destination;
        while (cursor != origin)
        {
            LocationConnection connection = previous[cursor];
            reversed.Add(connection);
            cursor = connection.Origin;
        }

        reversed.Reverse();
        var locations = new List<LocationId>(reversed.Count + 1) { origin };
        locations.AddRange(reversed.Select(connection => connection.Destination));
        return new LocationRoute(locations, reversed);
    }

    private void AddDirected(LocationConnection connection)
    {
        List<LocationConnection> outgoing = _connections[connection.Origin];
        if (outgoing.Any(existing => existing.Destination == connection.Destination))
        {
            throw new InvalidOperationException(
                $"Locations '{connection.Origin}' and '{connection.Destination}' are already connected.");
        }

        outgoing.Add(connection);
    }

    private void EnsureKnown(LocationId location)
    {
        if (!_locations.Contains(location))
        {
            throw new KeyNotFoundException($"Location '{location}' does not exist in the graph.");
        }
    }
}
