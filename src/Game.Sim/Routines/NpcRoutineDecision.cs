using Game.Sim.Brain;
using Game.Sim.Entities;
using Game.Sim.Time;

namespace Game.Sim.Routines;

public sealed record NpcRoutineDecision(
    SimTime Time,
    EntityId Entity,
    GoalCandidate Goal,
    bool Moved);
