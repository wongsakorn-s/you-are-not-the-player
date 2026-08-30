using Game.Sim.Entities;

namespace Game.Sim.Suspicion;

public sealed class SuspicionSnapshot
{
    public SuspicionSnapshot(
        EntityId observer,
        EntityId subject,
        SuspicionVector vector,
        IEnumerable<EvaluatedEvidence> evidence)
    {
        ArgumentNullException.ThrowIfNull(vector);
        ArgumentNullException.ThrowIfNull(evidence);
        Observer = observer;
        Subject = subject;
        Vector = vector;
        Evidence = Array.AsReadOnly(evidence.ToArray());
    }

    public EntityId Observer { get; }

    public EntityId Subject { get; }

    public SuspicionVector Vector { get; }

    public IReadOnlyList<EvaluatedEvidence> Evidence { get; }
}
