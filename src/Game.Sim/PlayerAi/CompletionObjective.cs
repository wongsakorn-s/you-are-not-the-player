using Game.Sim.Events;
using Game.Sim.Locations;

namespace Game.Sim.PlayerAi;

public sealed record CompletionObjective
{
    public CompletionObjective(
        string id,
        LocationId location,
        InteractionKind interactionKind)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        if (location.IsEmpty)
        {
            throw new ArgumentException("Completion location cannot be empty.", nameof(location));
        }

        if (!Enum.IsDefined(interactionKind))
        {
            throw new ArgumentOutOfRangeException(
                nameof(interactionKind),
                interactionKind,
                "Unknown interaction kind.");
        }

        Id = id.Trim();
        Location = location;
        InteractionKind = interactionKind;
    }

    public string Id { get; }

    public LocationId Location { get; }

    public InteractionKind InteractionKind { get; }
}
