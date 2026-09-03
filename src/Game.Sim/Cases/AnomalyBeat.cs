using Game.Sim.Anomalies;
using Game.Sim.Entities;

namespace Game.Sim.Cases;

/// <summary>
/// A reality anomaly the session plans to surface at a known tick. Scheduling
/// these up front keeps them reproducible from the seed instead of depending on
/// whether the human happened to quick-load during the run.
/// </summary>
public sealed record AnomalyBeat
{
    public AnomalyBeat(long tick, AnomalyKind kind, EntityId subject)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(tick);
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown anomaly kind.");
        }

        if (subject.IsEmpty)
        {
            throw new ArgumentException("Anomaly subject cannot be empty.", nameof(subject));
        }

        Tick = tick;
        Kind = kind;
        Subject = subject;
    }

    public long Tick { get; }

    public AnomalyKind Kind { get; }

    public EntityId Subject { get; }
}
