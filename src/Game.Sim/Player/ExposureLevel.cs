namespace Game.Sim.Player;

/// <summary>
/// How close the cast is to deciding the human host is the one being played.
/// </summary>
public enum ExposureLevel
{
    /// <summary>Nobody has anything on you.</summary>
    Unnoticed = 0,

    /// <summary>Someone has filed one thing away.</summary>
    Noticed = 1,

    /// <summary>Someone is actively keeping track of you.</summary>
    Watched = 2,

    /// <summary>Someone has enough to say it out loud.</summary>
    Cornered = 3,
}
