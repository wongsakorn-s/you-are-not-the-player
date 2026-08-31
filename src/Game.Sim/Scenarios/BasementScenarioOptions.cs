namespace Game.Sim.Scenarios;

public sealed record BasementScenarioOptions
{
    public const int MinimumTicks = 4;

    public BasementScenarioOptions(ulong seed, int ticks)
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
    }

    public ulong Seed { get; }

    public int Ticks { get; }
}
