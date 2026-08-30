using Game.Sim.Time;

namespace Game.Sim.Tests.Time;

public sealed class SimClockTests
{
    [Fact]
    public void NewClock_StartsAtZero()
    {
        var clock = new SimClock();

        Assert.Equal(SimTime.Zero, clock.Now);
        Assert.Equal(TimeSpan.Zero, clock.Elapsed);
    }

    [Fact]
    public void Advance_MovesClockByLogicalTicks()
    {
        var clock = new SimClock(ticksPerSecond: 4);

        SimTime now = clock.Advance(new SimDelta(10));

        Assert.Equal(new SimTime(10), now);
        Assert.Equal(TimeSpan.FromSeconds(2.5), clock.Elapsed);
    }

    [Fact]
    public void Constructor_RejectsInvalidTickRate()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SimClock(0));
    }

    [Fact]
    public void SimDelta_RejectsNegativeTicks()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SimDelta(-1));
    }
}
