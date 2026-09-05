using Game.Sim.Cases;

namespace Game.Sim.Scenarios;

public sealed record BasementScenarioOptions
{
    public const int MinimumTicks = 4;

    public BasementScenarioOptions(ulong seed, int ticks, SessionTruth? truth = null)
    {
        if (ticks < MinimumTicks)
        {
            throw new ArgumentOutOfRangeException(
                nameof(ticks),
                ticks,
                $"The basement scenario requires at least {MinimumTicks} ticks.");
        }

        Seed = seed;
        Ticks = ticks;
        Truth = truth;
    }

    public ulong Seed { get; }

    public int Ticks { get; }

    /// <summary>
    /// The hidden truth for this run, or null to keep the scripted arrangement
    /// where George is both the human host and the character the Player AI drives.
    /// The four regression scenarios pass null so their event fingerprints stay
    /// pinned; the game supplies a generated truth so the seed decides who is
    /// really being steered.
    /// </summary>
    public SessionTruth? Truth { get; }
}
