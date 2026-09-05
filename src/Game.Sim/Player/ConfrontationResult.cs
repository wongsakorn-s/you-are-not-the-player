namespace Game.Sim.Player;

/// <summary>
/// What happened when the player put a clue to someone's face.
/// </summary>
public enum ConfrontationResult
{
    /// <summary>The exchange was not a challenge to anything the partner said.</summary>
    None = 0,

    /// <summary>
    /// The clue broke the partner's own account and they gave something up.
    /// </summary>
    Cracked = 1,

    /// <summary>
    /// The partner's account held. The player has just accused someone of lying
    /// on the strength of a story that was not true, in front of them.
    /// </summary>
    Backfired = 2,
}
