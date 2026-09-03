using Game.Sim.Entities;
using Game.Sim.Player;
using Game.Sim.Scenarios;
using Game.Sim.Suspicion;

namespace Game.Sim.Tests.Player;

/// <summary>
/// Exposure is the other half of the deduction, so it has to be earned the same
/// way the player's own clues are: only from things a character actually saw.
/// </summary>
public sealed class ExposureReportTests
{
    private static readonly EntityId George = BasementScenario.George;
    private static readonly EntityId Anna = BasementScenario.Anna;
    private static readonly EntityId Bob = BasementScenario.Bob;

    [Fact]
    public void AnUnwatchedHostIsUnnoticed()
    {
        var report = new ExposureReport(George, [], []);

        Assert.Equal(ExposureLevel.Unnoticed, report.Level);
        Assert.Equal(0.0f, report.Peak);
        Assert.Equal(0, report.Spread);
        Assert.Null(report.MostSuspicious);
        Assert.Null(report.LeadingReason);
    }

    [Theory]
    [InlineData(0.0f, ExposureLevel.Unnoticed)]
    [InlineData(14.9f, ExposureLevel.Unnoticed)]
    [InlineData(15.0f, ExposureLevel.Noticed)]
    [InlineData(39.9f, ExposureLevel.Noticed)]
    [InlineData(40.0f, ExposureLevel.Watched)]
    [InlineData(79.9f, ExposureLevel.Watched)]
    [InlineData(80.0f, ExposureLevel.Cornered)]
    public void LevelFollowsTheMostConvincedObserver(float score, ExposureLevel expected)
    {
        ExposureReport report = WithObservers((Anna, score));

        Assert.Equal(expected, report.Level);
    }

    [Fact]
    public void LevelIsDrivenByThePeakNotTheSum()
    {
        // Five people who mildly wonder is an ordinary night. One person who is
        // sure is what starts a coalition.
        ExposureReport report = WithObservers((Anna, 10.0f), (Bob, 10.0f), (new EntityId("dana"), 10.0f));

        Assert.Equal(ExposureLevel.Unnoticed, report.Level);
        Assert.Equal(3, report.Spread);
    }

    [Fact]
    public void GuardednessIsPerObserverNotGlobal()
    {
        // The person who caught you gets guarded; everyone else still talks. This
        // keeps exposure a price rather than a wall.
        ExposureReport report = WithObservers((Anna, 95.0f), (Bob, 5.0f));

        Assert.True(report.IsGuardedTowards(Anna));
        Assert.True(report.RefusesToGossipWith(Anna));
        Assert.False(report.IsGuardedTowards(Bob));
        Assert.False(report.RefusesToGossipWith(Bob));
        Assert.False(report.RefusesToGossipWith(new EntityId("evelyn")));
    }

    [Fact]
    public void PlayerLikeDimensionsWeighMoreThanOrdinarySuspicion()
    {
        // Being a thief should not make you look like the Player. Acting like a
        // person who is testing the world should.
        float ordinary = ExposureReport.WeighVector(
            new SuspicionVector(criminality: 40, 0, 0, 0, 0, 0));
        float playerLike = ExposureReport.WeighVector(
            new SuspicionVector(0, 0, 0, metaBehavior: 40, 0, 0));

        Assert.True(playerLike > ordinary);
        Assert.Equal(40.0f, ExposureReport.WeighPlayerLike(
            new SuspicionVector(0, 0, 0, metaBehavior: 40, 0, 0)));
        Assert.Equal(0.0f, ExposureReport.WeighPlayerLike(
            new SuspicionVector(criminality: 40, secrecy: 40, 0, 0, 0, 0)));
    }

    [Fact]
    public void ObserversAndReasonsAreOrderedWorstFirst()
    {
        ExposureReport report = new(
            George,
            [
                Observer(Bob, 20.0f),
                Observer(Anna, 60.0f),
            ],
            [
                new ExposureReason(Bob, "restricted_area_entry", SuspicionDimension.RoleDeviation, 20.0f),
                new ExposureReason(Anna, "detected_loot_sweep", SuspicionDimension.MetaBehavior, 35.0f),
            ]);

        Assert.Equal(Anna, report.Observers[0].Observer);
        Assert.Equal("detected_loot_sweep", report.Reasons[0].RuleId);
        Assert.Equal(60.0f, report.Peak);
    }

    private static ExposureReport WithObservers(params (EntityId Observer, float Score)[] observers) =>
        new(
            George,
            observers.Select(item => Observer(item.Observer, item.Score)),
            observers.Select(item => new ExposureReason(
                item.Observer,
                "restricted_area_entry",
                SuspicionDimension.RoleDeviation,
                item.Score)));

    private static ObserverExposure Observer(EntityId observer, float score) => new(
        observer,
        score,
        score,
        SuspicionVector.Zero,
        EvidenceCount: 1);
}
