namespace Game.Sim.Snapshots;

public sealed record DecisionSnapshot(
    long Tick,
    string Entity,
    string GoalType,
    string Destination,
    float BaseUtility,
    bool Moved,
    string? Target = null,
    string? InteractionPartner = null,
    string? IntentId = null);
