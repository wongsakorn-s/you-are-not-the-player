using Game.Sim.Time;

namespace Game.Sim.Memory;

public sealed class ExponentialMemoryDecayPolicy : IMemoryDecayPolicy
{
    private readonly double _episodicDecayRatePerTick;
    private readonly double _socialDecayRatePerTick;

    public ExponentialMemoryDecayPolicy(
        double episodicDecayRatePerTick,
        double socialDecayRatePerTick)
    {
        ValidateRate(episodicDecayRatePerTick, nameof(episodicDecayRatePerTick));
        ValidateRate(socialDecayRatePerTick, nameof(socialDecayRatePerTick));
        _episodicDecayRatePerTick = episodicDecayRatePerTick;
        _socialDecayRatePerTick = socialDecayRatePerTick;
    }

    public float CalculateRetainedConfidence(MemoryRecord memory, SimTime now)
    {
        ArgumentNullException.ThrowIfNull(memory);

        if (now < memory.CreatedAt)
        {
            throw new ArgumentOutOfRangeException(
                nameof(now),
                now,
                "Confidence cannot be evaluated before the memory was created.");
        }

        long ageInTicks = now.Tick - memory.CreatedAt.Tick;
        double decayRate = memory.Kind == MemoryKind.Episodic
            ? _episodicDecayRatePerTick
            : _socialDecayRatePerTick;
        double retained = memory.InitialConfidence * Math.Exp(-decayRate * ageInTicks);
        return (float)retained;
    }

    private static void ValidateRate(double rate, string parameterName)
    {
        if (double.IsNaN(rate) || double.IsInfinity(rate) || rate < 0.0)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                rate,
                "Decay rate must be a finite non-negative number.");
        }
    }
}
