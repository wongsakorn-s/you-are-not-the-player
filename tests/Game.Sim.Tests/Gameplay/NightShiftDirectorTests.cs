using Game.Client.Godot.Gameplay;

namespace Game.Sim.Tests.Gameplay;

public sealed class NightShiftDirectorTests
{
    [Fact]
    public void CollectDue_ReturnsEachBeatOnlyOnce()
    {
        var director = new NightShiftDirector(481516UL);

        Assert.Empty(director.CollectDue(23));
        Assert.Single(director.CollectDue(24));
        Assert.Empty(director.CollectDue(24));
        Assert.Equal(9, director.CollectDue(NightShiftDirector.DeadlineTick).Count);
        Assert.Empty(director.CollectDue(NightShiftDirector.DeadlineTick));
    }

    [Fact]
    public void Constructor_UsesSeedDeterministically()
    {
        var first = new NightShiftDirector(99UL);
        var second = new NightShiftDirector(99UL);

        Assert.Equal(
            first.CollectDue(NightShiftDirector.DeadlineTick),
            second.CollectDue(NightShiftDirector.DeadlineTick));
    }

    [Fact]
    public void CollectDue_RejectsNegativeTick()
    {
        var director = new NightShiftDirector(1UL);

        Assert.Throws<ArgumentOutOfRangeException>(() => director.CollectDue(-1));
    }
}
