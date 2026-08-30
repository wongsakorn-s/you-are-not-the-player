namespace Game.Sim.Random;

public sealed class Pcg32SimRandom : ISimRandom
{
    private const ulong Multiplier = 6364136223846793005UL;
    private const float InverseFloatRange = 1.0f / 16_777_216.0f;

    private readonly ulong _increment;
    private ulong _state;

    public Pcg32SimRandom(ulong seed, ulong sequence = 54UL)
    {
        _increment = unchecked((sequence << 1) | 1UL);
        _state = 0UL;
        _ = NextUInt32();
        _state = unchecked(_state + seed);
        _ = NextUInt32();
    }

    public uint NextUInt32()
    {
        ulong oldState = _state;
        _state = unchecked((oldState * Multiplier) + _increment);

        uint xorShifted = (uint)(((oldState >> 18) ^ oldState) >> 27);
        int rotation = (int)(oldState >> 59);

        return (xorShifted >> rotation) | (xorShifted << ((-rotation) & 31));
    }

    public int NextInt(int minInclusive, int maxExclusive)
    {
        if (minInclusive >= maxExclusive)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxExclusive),
                maxExclusive,
                "The exclusive maximum must be greater than the inclusive minimum.");
        }

        uint range = (uint)((long)maxExclusive - minInclusive);
        uint threshold = unchecked(0U - range) % range;
        uint value;

        do
        {
            value = NextUInt32();
        }
        while (value < threshold);

        return (int)(minInclusive + (long)(value % range));
    }

    public float NextFloat() => (NextUInt32() >> 8) * InverseFloatRange;

    public bool Chance(float probability)
    {
        if (float.IsNaN(probability) || probability is < 0.0f or > 1.0f)
        {
            throw new ArgumentOutOfRangeException(
                nameof(probability),
                probability,
                "Probability must be between 0 and 1 inclusive.");
        }

        return NextFloat() < probability;
    }
}
