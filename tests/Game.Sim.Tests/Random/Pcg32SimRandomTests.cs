using Game.Sim.Random;

namespace Game.Sim.Tests.Random;

public sealed class Pcg32SimRandomTests
{
    [Fact]
    public void NextUInt32_MatchesReferenceSequence()
    {
        var random = new Pcg32SimRandom(seed: 42, sequence: 54);
        uint[] expected = [
            2_707_161_783,
            2_068_313_097,
            3_122_475_824,
            2_211_639_955,
            3_215_226_955,
        ];

        uint[] actual = Enumerable.Range(0, expected.Length)
            .Select(_ => random.NextUInt32())
            .ToArray();

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void SameSeed_ProducesSameSequence()
    {
        var first = new Pcg32SimRandom(seed: 481_516);
        var second = new Pcg32SimRandom(seed: 481_516);

        int[] firstSequence = Enumerable.Range(0, 100)
            .Select(_ => first.NextInt(-50, 50))
            .ToArray();
        int[] secondSequence = Enumerable.Range(0, 100)
            .Select(_ => second.NextInt(-50, 50))
            .ToArray();

        Assert.Equal(firstSequence, secondSequence);
    }

    [Fact]
    public void NextInt_StaysInsideRequestedRange()
    {
        var random = new Pcg32SimRandom(seed: 7);

        int[] values = Enumerable.Range(0, 1_000)
            .Select(_ => random.NextInt(-3, 8))
            .ToArray();

        Assert.All(values, value => Assert.InRange(value, -3, 7));
    }

    [Theory]
    [InlineData(5, 5)]
    [InlineData(6, 5)]
    public void NextInt_RejectsInvalidRange(int minInclusive, int maxExclusive)
    {
        var random = new Pcg32SimRandom(seed: 7);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => random.NextInt(minInclusive, maxExclusive));
    }

    [Theory]
    [InlineData(-0.01f)]
    [InlineData(1.01f)]
    public void Chance_RejectsOutOfRangeProbability(float probability)
    {
        var random = new Pcg32SimRandom(seed: 7);

        Assert.Throws<ArgumentOutOfRangeException>(() => random.Chance(probability));
    }
}
