using Game.Sim.Entities;
using Game.Sim.Suspicion;

namespace Game.Sim.Player;

/// <summary>What one character has on the host.</summary>
public sealed record ObserverExposure(
    EntityId Observer,
    float Score,
    float PlayerLikeScore,
    SuspicionVector Vector,
    int EvidenceCount);
