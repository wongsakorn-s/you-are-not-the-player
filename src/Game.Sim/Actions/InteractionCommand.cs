using Game.Sim.Entities;
using Game.Sim.Events;

namespace Game.Sim.Actions;

public sealed record InteractionCommand
{
    public InteractionCommand(
        EntityId actor,
        InteractionKind kind,
        string interactionId)
    {
        if (actor.IsEmpty)
        {
            throw new ArgumentException("Interaction actor cannot be empty.", nameof(actor));
        }

        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown interaction kind.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(interactionId);
        Actor = actor;
        Kind = kind;
        InteractionId = interactionId.Trim();
    }

    public EntityId Actor { get; }

    public InteractionKind Kind { get; }

    public string InteractionId { get; }
}
