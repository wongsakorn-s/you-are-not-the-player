using Game.Sim.Cases;
using Game.Sim.Conspiracy;
using Game.Sim.Entities;
using Game.Sim.Player;
using Game.Sim.PlayerAi;
using Game.Sim.Scenarios;
using Game.Sim.Suspicion;

namespace Game.Sim.Tests.Player;

/// <summary>
/// The night needs a shape. Running out of clock is not a threat; a group of
/// people quietly agreeing about you is.
/// </summary>
public sealed class ClosingNetTests
{
    private static readonly string[] SuspiciousObjectsInReach =
    [
        "lobby-guest-registry",
        "lobby-reception-bell",
    ];

    [Fact]
    public void AQuietHostIsNeverGangedUpOn()
    {
        BasementScenarioSession session = CreateSession();
        Advance(session, 20);

        AccusationCoalition? coalition = session.EvaluateConspiracy(BasementScenario.George);

        Assert.True(
            coalition is null || !coalition.ConsensusReached,
            "Doing nothing should not turn the hotel against you.");
    }

    [Fact]
    public void ActingLikeAPlayerInPublicPullsACoalitionTogether()
    {
        BasementScenarioSession session = CreateSession();
        MisbehaveInFrontOfEveryone(session);

        AccusationCoalition? coalition = session.EvaluateConspiracy(BasementScenario.George);

        Assert.NotNull(coalition);
        Assert.Equal(BasementScenario.George, coalition.Target);
        Assert.True(coalition.Members.Count >= 2, "A coalition is more than one worried person.");
        Assert.DoesNotContain(BasementScenario.George, coalition.Members);
    }

    [Fact]
    public void ConsensusIsWhatArmsTheConfrontation()
    {
        BasementScenarioSession session = CreateSession();
        MisbehaveInFrontOfEveryone(session);

        AccusationCoalition? coalition = session.EvaluateConspiracy(BasementScenario.George);
        Assert.NotNull(coalition);
        Assert.True(coalition.ConsensusReached);
        Assert.Equal(CoalitionStage.ConsensusReached, coalition.Stage);

        // The client will not arm the net until the coalition clears this, so a
        // pattern of visible misbehaviour has to be able to reach it. If this ever
        // fails, the closing net silently stops being part of the game.
        Assert.True(
            coalition.CombinedSuspicionScore >= 90.0f,
            $"A run of visible misbehaviour scored only {coalition.CombinedSuspicionScore:F1}.");

        Sim.Events.WorldEvent? confrontation = session.TriggerConfrontation(
            session.GetLogicalLocation(BasementScenario.George));

        Assert.NotNull(confrontation);
        Assert.Equal(CoalitionStage.Confronting, coalition.Stage);
        Assert.True(session.CanResolveClimax(BasementScenario.George));
    }

    [Theory]
    [InlineData(PlayerClimaxChoice.ConfessReality)]
    [InlineData(PlayerClimaxChoice.DenyAndCounter)]
    [InlineData(PlayerClimaxChoice.Flee)]
    public void EveryClimaxChoiceEndsTheNightWithSomethingToSay(PlayerClimaxChoice choice)
    {
        BasementScenarioSession session = CreateSession();
        MisbehaveInFrontOfEveryone(session);
        _ = session.EvaluateConspiracy(BasementScenario.George);
        _ = session.TriggerConfrontation(session.GetLogicalLocation(BasementScenario.George));

        ClimaxResolution resolution = session.ResolveClimax(choice, BasementScenario.George);

        Assert.Equal(choice, resolution.Choice);
        Assert.False(string.IsNullOrWhiteSpace(resolution.Title));
        Assert.False(string.IsNullOrWhiteSpace(resolution.NarrativeText));
    }

    [Fact]
    public void TheConfrontationCannotFireBeforeThereIsAgreement()
    {
        BasementScenarioSession session = CreateSession();
        Advance(session, 10);
        _ = session.EvaluateConspiracy(BasementScenario.George);

        Sim.Events.WorldEvent? confrontation = session.TriggerConfrontation(BasementScenario.Lobby);

        Assert.Null(confrontation);
        Assert.False(session.CanResolveClimax(BasementScenario.George));
    }

    [Fact]
    public void TheHotelBuildsItsCaseFromTheSameEvidenceTheExposurePageShows()
    {
        // The two read the same suspicion pipeline and answer different questions:
        // exposure asks how much like the Player you look, the coalition asks how
        // much trouble you are in. They weigh the dimensions differently on
        // purpose, so the check is that they rest on the same witnesses - anyone
        // ganging up on the host must be someone the exposure page already named.
        BasementScenarioSession session = CreateSession();
        MisbehaveInFrontOfEveryone(session);

        ExposureReport exposure = session.GetExposure(BasementScenario.George);
        AccusationCoalition? coalition = session.EvaluateConspiracy(BasementScenario.George);

        Assert.NotNull(coalition);
        Assert.NotEqual(ExposureLevel.Unnoticed, exposure.Level);
        Assert.All(
            coalition.Members,
            member => Assert.Contains(
                exposure.Observers,
                observer => observer.Observer == member));
    }

    private static void MisbehaveInFrontOfEveryone(BasementScenarioSession session)
    {
        // Everything here is something a person standing in the lobby can see, so
        // the case against the host is built from witnesses, not from bookkeeping.
        foreach (string objectId in SuspiciousObjectsInReach)
        {
            _ = session.PlayerController.TamperObject(objectId);
            Advance(session, 2);
        }

        _ = session.PlayerController.RequestMove(BasementScenario.Basement);
        Advance(session, 6);
        _ = session.PlayerController.RequestMove(BasementScenario.Lobby);
        Advance(session, 6);
    }

    private static void Advance(BasementScenarioSession session, int ticks)
    {
        for (int index = 0; index < ticks && !session.IsComplete; index++)
        {
            _ = session.AdvanceOneTick();
        }
    }

    private static BasementScenarioSession CreateSession()
    {
        InMemorySuspicionRuleRepository rules = JsonSuspicionRuleParser.Parse(
            File.ReadAllText(Path.Combine(
                AppContext.BaseDirectory,
                "Data",
                "SuspicionRules",
                "mvp.json")));
        var truth = new SessionTruth(
            seed: 481_516,
            humanHost: BasementScenario.George,
            hiddenPlayer: BasementScenario.Charlie,
            hiddenPlayerArchetype: PlayerAiArchetype.Explorer,
            incidentCulprit: BasementScenario.George);
        return new BasementScenario(rules).CreateSession(
            new BasementScenarioOptions(481_516, 128, truth),
            autoCompleteMovements: true);
    }
}
