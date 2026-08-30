using Game.Sim.Locations;

namespace Game.Sim.Events;

public abstract record EventPayload
{
    private protected EventPayload()
    {
    }
}

public sealed record EmptyEventPayload : EventPayload
{
    public static EmptyEventPayload Instance { get; } = new();

    private EmptyEventPayload()
    {
    }
}

public sealed record LocationTransitionPayload : EventPayload
{
    public LocationTransitionPayload(LocationId origin, LocationId destination)
    {
        if (origin.IsEmpty)
        {
            throw new ArgumentException("Origin location cannot be empty.", nameof(origin));
        }

        if (destination.IsEmpty)
        {
            throw new ArgumentException("Destination location cannot be empty.", nameof(destination));
        }

        Origin = origin;
        Destination = destination;
    }

    public LocationId Origin { get; }

    public LocationId Destination { get; }
}
