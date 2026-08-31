using Game.Sim.Entities;

namespace Game.Sim.Actions;

public sealed record BoundaryProbeCommand
{
    public BoundaryProbeCommand(EntityId actor, string boundaryId)
    {
        if (actor.IsEmpty)
        {
            throw new ArgumentException("Boundary probe actor cannot be empty.", nameof(actor));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(boundaryId);
        Actor = actor;
        BoundaryId = boundaryId.Trim();
    }

    public EntityId Actor { get; }

    public string BoundaryId { get; }
}
