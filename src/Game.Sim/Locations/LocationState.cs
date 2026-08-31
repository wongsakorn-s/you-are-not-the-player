namespace Game.Sim.Locations;

public sealed class LocationState
{
    public LocationState(LocationId id, bool isRestricted = false)
    {
        if (id.IsEmpty)
        {
            throw new ArgumentException("Location ID cannot be empty.", nameof(id));
        }

        Id = id;
        IsRestricted = isRestricted;
    }

    public LocationId Id { get; }

    public bool IsRestricted { get; }
}
