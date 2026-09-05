using Game.Client.Godot.Gameplay;

namespace Game.Sim.Tests.Gameplay;

public sealed class NightShiftDirectorTests
{
    [Fact]
    public void CollectDue_ReturnsEachBeatOnlyOnce()
    {
        var director = new NightShiftDirector(481516UL);
        int total = director.Beats.Count;

        Assert.Equal(total, director.CollectDue(NightShiftDirector.DeadlineTick).Count);
        Assert.Empty(director.CollectDue(NightShiftDirector.DeadlineTick));
    }

    [Fact]
    public void NoTwoNightsRunTheSameBeats()
    {
        // Fifteen played nights had the same ten beats in the same order at the
        // same minutes, including a forty-three minute silence in the middle of
        // every one of them. A second playthrough has to be a different night.
        string[] first = Signature(new NightShiftDirector(1UL));
        string[] second = Signature(new NightShiftDirector(2UL));
        string[] third = Signature(new NightShiftDirector(3UL));

        Assert.NotEqual(first, second);
        Assert.NotEqual(second, third);
        Assert.NotEqual(first, third);
    }

    [Fact]
    public void TheNightIsNeverSilentForLong()
    {
        // The gap that matters is the one where the player is told nothing and
        // starts wondering whether the game is still running.
        for (ulong seed = 0; seed < 40; seed++)
        {
            var director = new NightShiftDirector(seed);
            long previous = 0;
            foreach (ShiftBeat beat in director.Beats)
            {
                Assert.True(
                    beat.Tick - previous <= 90,
                    $"Seed {seed} says nothing for {beat.Tick - previous} minutes.");
                previous = beat.Tick;
            }
        }
    }

    [Fact]
    public void EveryBeatArrivesInsideTheShift()
    {
        for (ulong seed = 0; seed < 40; seed++)
        {
            var director = new NightShiftDirector(seed);
            Assert.All(director.Beats, beat => Assert.InRange(
                beat.Tick,
                0,
                NightShiftDirector.DeadlineTick));
            Assert.Equal(
                ShiftBeatKind.FinalWarning,
                director.Beats[^1].Kind);
        }
    }

    [Fact]
    public void ABeatAboutSomebodySaysWhoAndWhere()
    {
        // The text carries placeholders because the director does not know what
        // the cast is called or which language is being read. A beat that names
        // an actor without a room, or a room without an actor, renders a brace.
        for (ulong seed = 0; seed < 40; seed++)
        {
            foreach (ShiftBeat beat in new NightShiftDirector(seed).Beats)
            {
                foreach (string text in new[] { beat.EnglishText, beat.ThaiText })
                {
                    Assert.Equal(text.Contains("{actor}"), beat.ActorId is not null);
                    Assert.Equal(text.Contains("{room}"), beat.DestinationId is not null);
                }
            }
        }
    }

    private static string[] Signature(NightShiftDirector director) =>
        [.. director.Beats.Select(beat => $"{beat.Tick}:{beat.Kind}:{beat.EnglishText}")];

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
