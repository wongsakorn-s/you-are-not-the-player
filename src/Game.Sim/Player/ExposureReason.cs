using Game.Sim.Entities;
using Game.Sim.Suspicion;

namespace Game.Sim.Player;

/// <summary>
/// One thing a specific character holds against the host, kept as the rule that
/// fired rather than as prose so the client can phrase it in either language.
/// </summary>
public sealed record ExposureReason(
    EntityId Observer,
    string RuleId,
    SuspicionDimension Dimension,
    float Weight);
