using Game.Sim.Cases;
using Game.Sim.Entities;
using Game.Sim.Events;
using Game.Sim.PlayerAi;
using Game.Sim.Scenarios;
using Game.Sim.Suspicion;

namespace Game.Sim.Tests.Cases;

/// <summary>
/// The ending the whole premise points at: the hand on the basement door was
/// your own, and you were not the one deciding when it moved.
/// </summary>
public sealed class HostAsHiddenPlayerTests
{
    private static readonly EntityId[] Roster =
    [
        BasementScenario.Anna, BasementScenario.Bob, BasementScenario.George,
        BasementScenario.Charlie, BasementScenario.Dana, BasementScenario.Evelyn,
    ];

    [Fact]
    public void TheHostBecomesTheHiddenPlayerSometimesButNotOften()
    {
        // Often enough to be a real possibility the player has to hold open, rare
        // enough that it stays a twist rather than the expected answer.
        int hosted = 0;
        for (ulong seed = 0; seed < 300; seed++)
        {
            if (Generate(seed, allowHost: true).HostIsHiddenPlayer)
            {
                hosted++;
            }
        }

        Assert.InRange(hosted / 300.0, 0.08, 0.30);
    }

    [Fact]
    public void NobodyElseIsSteeredWhenTheHostIsTheOne()
    {
        SessionTruth truth = FindHostedTruth();

        Assert.Equal(truth.HumanHost, truth.HiddenPlayer);
        Assert.DoesNotContain(truth.Secrets, secret => secret.Owner == truth.HumanHost);
        Assert.All(truth.AnomalySchedule, beat => Assert.Equal(truth.HumanHost, beat.Subject));
    }

    [Fact]
    public void ThePlayerAiDoesNotDriveAHostTheHumanIsAlreadyDriving()
    {
        // Two drivers on one character is the bug that made George unplayable
        // before Milestone 4, and it would also rob the twist of its point: the
        // Player-like behaviour the cast reacts to has to be the human's own.
        BasementScenarioSession session = CreateSession(FindHostedSeed());
        RunNight(session);

        Assert.DoesNotContain(
            session.Decisions,
            decision => decision.Entity == BasementScenario.George);
    }

    [Fact]
    public void TheHotelStillBuildsItsCaseAgainstAHostedPlayer()
    {
        // The anomalies land on the host, so the exposure meter and the coalition
        // both converge on the person holding the controller. That is the ending
        // arriving through the systems rather than through a scripted reveal.
        BasementScenarioSession session = CreateSession(FindHostedSeed());
        RunNight(session);

        Assert.Contains(
            session.Events,
            worldEvent => worldEvent.Type == EventType.RealityAnomaly &&
                worldEvent.Actor == BasementScenario.George);
    }

    [Fact]
    public void AHostedNightIsStillWinnableAndStillLoseable()
    {
        // Both halves matter: naming yourself has to be right sometimes, and the
        // other five have to remain plausible or the twist is a coin flip.
        SessionTruth hosted = FindHostedTruth();
        SessionTruth ordinary = Generate(0UL, allowHost: true);
        while (ordinary.HostIsHiddenPlayer)
        {
            ordinary = Generate(ordinary.Seed + 1, allowHost: true);
        }

        Assert.Equal(BasementScenario.George, hosted.HiddenPlayer);
        Assert.NotEqual(BasementScenario.George, ordinary.HiddenPlayer);
    }

    [Fact]
    public void LeavingTheOptionOffKeepsTheHostOutOfIt()
    {
        for (ulong seed = 0; seed < 200; seed++)
        {
            Assert.False(Generate(seed, allowHost: false).HostIsHiddenPlayer);
        }
    }

    private static SessionTruth FindHostedTruth() => Generate(FindHostedSeed(), allowHost: true);

    private static ulong FindHostedSeed()
    {
        for (ulong seed = 0; seed < 300; seed++)
        {
            if (Generate(seed, allowHost: true).HostIsHiddenPlayer)
            {
                return seed;
            }
        }

        throw new InvalidOperationException("No seed in 300 put the host behind the Player.");
    }

    private static SessionTruth Generate(ulong seed, bool allowHost) => CaseGenerator.Generate(
        seed,
        new CaseGenerationOptions(
            BasementScenario.George,
            Roster,
            shiftTicks: 360,
            pinnedIncidentCulprit: BasementScenario.George,
            allowHostAsHiddenPlayer: allowHost));

    private static void RunNight(BasementScenarioSession session)
    {
        for (int tick = 0; tick < 360 && !session.IsComplete; tick++)
        {
            _ = session.AdvanceOneTick();
        }
    }

    private static BasementScenarioSession CreateSession(ulong seed)
    {
        InMemorySuspicionRuleRepository rules = JsonSuspicionRuleParser.Parse(
            File.ReadAllText(Path.Combine(
                AppContext.BaseDirectory,
                "Data",
                "SuspicionRules",
                "mvp.json")));
        return new BasementScenario(rules).CreateSession(
            new BasementScenarioOptions(seed, 360, Generate(seed, allowHost: true)),
            autoCompleteMovements: true);
    }
}
