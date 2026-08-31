namespace Game.Sim.Conspiracy;

public sealed record ClimaxResolution(
    PlayerClimaxChoice Choice,
    string Title,
    string NarrativeText,
    bool PlayerVindicated,
    bool ExistentialAwakeningTriggered,
    bool PlayerFled);
