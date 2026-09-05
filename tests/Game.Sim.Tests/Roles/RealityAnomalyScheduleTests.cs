using Game.Sim.Anomalies;
using Game.Sim.Cases;
using Game.Sim.Entities;
using Game.Sim.Events;
using Game.Sim.Scenarios;
using Game.Sim.Suspicion;

namespace Game.Sim.Tests.Roles;

/// <summary>
/// Reality anomalies are the premise the game is named for. They had never once
/// occurred in a playable run: CaseGenerator scheduled them and nothing read the
/// schedule.
/// </summary>
public sealed class RealityAnomalyScheduleTests
{
    private static readonly EntityId[] Roster =
    [
        BasementScenario.Anna, BasementScenario.Bob, BasementScenario.George,
        BasementScenario.Charlie, BasementScenario.Dana, BasementScenario.Evelyn,
    ];

    [Fact]
    public void AnomaliesOnlyEverHappenToTheHiddenPlayer()
    {
        // An anomaly is the seam where something outside the world reaches into
        // it. Staging one on an innocent would not be a red herring, it would be
        // the fiction contradicting itself.
        for (ulong seed = 0; seed < 120; seed++)
        {
            SessionTruth truth = Generate(seed);
            Assert.All(
                truth.AnomalySchedule,
                beat => Assert.Equal(truth.HiddenPlayer, beat.Subject));
        }
    }

    [Fact]
    public void TheScheduleIsSpreadAcrossTheNightRatherThanClumped()
    {
        SessionTruth truth = Generate(481_516UL);

        Assert.Equal(CaseGenerationOptions.DefaultAnomalyCount, truth.AnomalySchedule.Count);
        Assert.All(truth.AnomalySchedule, beat => Assert.InRange(beat.Tick, 0, 359));
        Assert.True(
            truth.AnomalySchedule.Select(beat => beat.Tick).Distinct().Count() > 1,
            "Every anomaly landed on the same tick.");
    }

    [Fact]
    public void EveryScheduledAnomalyActuallyHappens()
    {
        BasementScenarioSession session = CreateSession(481_516UL);
        int scheduled = Generate(481_516UL).AnomalySchedule.Count;

        RunNight(session);

        int fired = session.Events.Count(worldEvent =>
            worldEvent.Type == EventType.RealityAnomaly);
        Assert.Equal(scheduled, fired);
    }

    [Fact]
    public void EveryAnomalyKindCanBeStagedAndSurvivesTheRun()
    {
        // DialogueReset had no trigger at all; the enum listed it and nothing
        // could produce it.
        AnomalyKind[] scheduled = Enumerable.Range(0, 60)
            .SelectMany(seed => Generate((ulong)seed).AnomalySchedule)
            .Select(beat => beat.Kind)
            .Distinct()
            .ToArray();

        Assert.Equal(
            Enum.GetValues<AnomalyKind>().OrderBy(kind => (int)kind),
            scheduled.OrderBy(kind => (int)kind));
    }

    [Fact]
    public void SeeingSomethingImpossiblePointsAtThePersonItHappenedTo()
    {
        // The payoff: the strongest evidence in the game names the right person,
        // and only reaches whoever was standing there.
        BasementScenarioSession session = CreateSession(481_516UL);
        SessionTruth truth = Generate(481_516UL);
        RunNight(session);

        EntityId[] accusers = Roster
            .Where(observer => observer != truth.HiddenPlayer)
            .Where(observer => session
                .GetSuspicion(observer, truth.HiddenPlayer).Evidence
                .Any(evidence => evidence.Contribution.RuleId.Contains(
                    "anomaly",
                    StringComparison.Ordinal)))
            .ToArray();

        Assert.NotEmpty(accusers);
    }

    [Fact]
    public void AnOrdinarySecretIsStillMistakableForThePlayer()
    {
        // The other half: somebody with a mundane secret has to be suspected too,
        // or the anomaly trail is the only trail and there is nothing to weigh.
        BasementScenarioSession session = CreateSession(481_516UL);
        SessionTruth truth = Generate(481_516UL);
        RunNight(session);

        bool innocentUnderSuspicion = Roster
            .Where(subject => subject != truth.HiddenPlayer && subject != truth.HumanHost)
            .Any(subject => Roster
                .Where(observer => observer != subject)
                .Any(observer => session.GetSuspicion(observer, subject).Evidence.Count > 0));

        Assert.True(innocentUnderSuspicion, "Nobody innocent was ever suspected of anything.");
    }

    private static void RunNight(BasementScenarioSession session)
    {
        for (int tick = 0; tick < 360 && !session.IsComplete; tick++)
        {
            _ = session.AdvanceOneTick();
        }
    }

    private static SessionTruth Generate(ulong seed) => CaseGenerator.Generate(
        seed,
        new CaseGenerationOptions(
            BasementScenario.George,
            Roster,
            shiftTicks: 360,
            pinnedIncidentCulprit: BasementScenario.George));

    private static BasementScenarioSession CreateSession(ulong seed)
    {
        InMemorySuspicionRuleRepository rules = JsonSuspicionRuleParser.Parse(
            File.ReadAllText(Path.Combine(
                AppContext.BaseDirectory,
                "Data",
                "SuspicionRules",
                "mvp.json")));
        return new BasementScenario(rules).CreateSession(
            new BasementScenarioOptions(seed, 360, Generate(seed)),
            autoCompleteMovements: true);
    }
}
