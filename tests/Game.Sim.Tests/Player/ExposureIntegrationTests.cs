using Game.Sim.Entities;
using Game.Sim.Cases;
using Game.Sim.Locations;
using Game.Sim.Player;
using Game.Sim.PlayerAi;
using Game.Sim.Scenarios;
using Game.Sim.Suspicion;

namespace Game.Sim.Tests.Player;

/// <summary>
/// The design pillar this closes: the human uses the same action pipeline as the
/// cast, so the hotel can build a case against them the same way they build one
/// against it.
/// </summary>
public sealed class ExposureIntegrationTests
{
    [Fact]
    public void AHostWhoTouchesNothingStaysUnnoticed()
    {
        BasementScenarioSession session = CreateSession();
        Advance(session, 12);

        ExposureReport exposure = session.GetExposure(BasementScenario.George);

        Assert.Equal(ExposureLevel.Unnoticed, exposure.Level);
        Assert.Empty(exposure.Observers);
    }

    [Fact]
    public void TamperingInFrontOfAWitnessRaisesExposure()
    {
        BasementScenarioSession session = CreateSession();

        // Bob starts in the lobby with George, and the guest registry is flagged
        // as suspicious to tamper with, so this is something a person standing
        // there would actually notice.
        _ = session.PlayerController.TamperObject("lobby-guest-registry");
        Advance(session, 4);

        ExposureReport exposure = session.GetExposure(BasementScenario.George);

        Assert.NotEqual(ExposureLevel.Unnoticed, exposure.Level);
        Assert.NotEmpty(exposure.Observers);
        Assert.Contains(exposure.Reasons, reason => reason.RuleId == "witnessed_suspicious_tampering");
    }

    [Fact]
    public void ExposureNamesTheCharacterWhoSawItAndWhy()
    {
        BasementScenarioSession session = CreateSession();
        _ = session.PlayerController.TamperObject("lobby-guest-registry");
        Advance(session, 4);

        ExposureReport exposure = session.GetExposure(BasementScenario.George);
        ObserverExposure? worst = exposure.MostSuspicious;

        Assert.NotNull(worst);
        Assert.NotEqual(BasementScenario.George, worst.Observer);
        Assert.True(worst.EvidenceCount > 0);
        Assert.NotNull(exposure.LeadingReason);
        Assert.Equal(worst.Observer, exposure.Reasons.First(r => r.Observer == worst.Observer).Observer);
    }

    [Fact]
    public void ExposureOnlyCountsWhatSomeoneCouldPerceive()
    {
        // The same act, with the lobby emptied first. Nobody sees it, so nobody
        // holds it against the host - exposure is evidence, not omniscience.
        BasementScenarioSession session = CreateSession();
        foreach (EntityId bystander in new[]
        {
            BasementScenario.Bob,
            BasementScenario.Charlie,
            BasementScenario.Dana,
            BasementScenario.Evelyn,
        })
        {
            _ = session.RequestNpcMove(bystander, new LocationId("kitchen"));
        }

        Advance(session, 6);
        _ = session.PlayerController.TamperObject("lobby-guest-registry");
        Advance(session, 4);

        ExposureReport exposure = session.GetExposure(BasementScenario.George);

        Assert.DoesNotContain(
            exposure.Reasons,
            reason => reason.RuleId == "witnessed_suspicious_tampering" &&
                reason.Observer == BasementScenario.Bob);
    }

    [Fact]
    public void HiddenTruthIsNotConsulted()
    {
        // GetExposure reads the suspicion pipeline only. If it ever started
        // reading SessionTruth, a host who happened to be the hidden player would
        // read differently with no in-world reason.
        BasementScenarioSession session = CreateSession();
        Advance(session, 8);

        ExposureReport exposure = session.GetExposure(BasementScenario.George);

        Assert.All(exposure.Observers, observer =>
        {
            SuspicionSnapshot snapshot = session.GetSuspicion(
                observer.Observer,
                BasementScenario.George);
            Assert.Equal(snapshot.Evidence.Count, observer.EvidenceCount);
        });
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
        // Built the way the game builds it: with a SessionTruth, so the Player AI
        // steers someone else and George is driven purely by the human. Without a
        // truth the scripted scenario walks George into the basement itself, and
        // the host would arrive already suspected through no act of the player's.
        var truth = new SessionTruth(
            seed: 481_516,
            humanHost: BasementScenario.George,
            hiddenPlayer: BasementScenario.Charlie,
            hiddenPlayerArchetype: PlayerAiArchetype.Explorer,
            incidentCulprit: BasementScenario.George);
        return new BasementScenario(rules).CreateSession(
            new BasementScenarioOptions(seed: 481_516, ticks: 64, truth),
            autoCompleteMovements: true);
    }
}
