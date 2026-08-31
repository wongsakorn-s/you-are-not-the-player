namespace Game.Sim.Locations;

public sealed class LocationRoute
{
    private readonly LocationId[] _locations;
    private readonly LocationConnection[] _connections;

    internal LocationRoute(
        IEnumerable<LocationId> locations,
        IEnumerable<LocationConnection> connections)
    {
        _locations = [.. locations];
        _connections = [.. connections];
    }

    public IReadOnlyList<LocationId> Locations => _locations;

    public IReadOnlyList<LocationConnection> Connections => _connections;
}
