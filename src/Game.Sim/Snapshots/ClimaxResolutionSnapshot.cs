namespace Game.Sim.Snapshots;

public sealed record ClimaxResolutionSnapshot(
    string Choice,
    string Title,
    string NarrativeText,
    bool PlayerVindicated,
    bool ExistentialAwakeningTriggered,
    bool PlayerFled);
