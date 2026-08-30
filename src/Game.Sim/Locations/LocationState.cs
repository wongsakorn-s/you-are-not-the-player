namespace Game.Sim.Locations;

public sealed class LocationState
{
    public LocationState(LocationId id)
    {
        if (id.IsEmpty)
        {
            throw new ArgumentException("Location ID cannot be empty.", nameof(id));
        }

        Id = id;
    }

    public LocationId Id { get; }
}
