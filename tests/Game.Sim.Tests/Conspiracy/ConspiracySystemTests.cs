using Game.Sim.Conspiracy;
using Game.Sim.Entities;
using Game.Sim.Events;
using Game.Sim.Locations;
using Game.Sim.Memory;
using Game.Sim.Scenarios;
using Game.Sim.Suspicion;

namespace Game.Sim.Tests.Conspiracy;

public sealed class ConspiracySystemTests
{
    [Fact]
    public void Conspiracy_FormsCoalition_WhenSuspicionCrossesThreshold()
    {
        BasementScenarioSession session = CreateSession();

        // Advance simulation until Anna observes George enter basement
        for (int i = 0; i < 6; i++)
        {
            _ = session.AdvanceOneTick();
            while (session.PendingMovements.Count > 0)
            {
                _ = session.CompleteMovement(session.PendingMovements[0].RequestId);
            }
        }

        AccusationCoalition? coalition = session.EvaluateConspiracy(BasementScenario.George);

        Assert.NotNull(coalition);
        Assert.Equal(BasementScenario.George, coalition.Target);
        Assert.Contains(BasementScenario.Anna, coalition.Members);
        Assert.True(coalition.CombinedSuspicionScore > 0);
    }

    [Fact]
    public void Conspiracy_ConsensusReached_WhenMultipleNpcsSuspectTarget()
    {
        BasementScenarioSession session = CreateSession();

        // Advance 16 ticks so Anna observes George and tells Bob
        for (int i = 0; i < 16; i++)
        {
            _ = session.AdvanceOneTick();
            while (session.PendingMovements.Count > 0)
            {
                _ = session.CompleteMovement(session.PendingMovements[0].RequestId);
            }
        }

        AccusationCoalition? coalition = session.EvaluateConspiracy(BasementScenario.George);

        Assert.NotNull(coalition);
        Assert.Contains(BasementScenario.Anna, coalition.Members);
        Assert.Contains(BasementScenario.Bob, coalition.Members);
        Assert.True(coalition.ConsensusReached);

        WorldEvent? confrontation = session.TriggerConfrontation(BasementScenario.Lobby);
        Assert.NotNull(confrontation);
        Assert.Equal(CoalitionStage.Confronting, coalition.Stage);
    }

    [Fact]
    public void Confrontation_ResolveConfess_TriggersAwakeningEnding()
    {
        BasementScenarioSession session = CreateSession();
        _ = PrepareConfrontation(session);

        ClimaxResolution resolution = session.ResolveClimax(PlayerClimaxChoice.ConfessReality);

        Assert.Equal(PlayerClimaxChoice.ConfessReality, resolution.Choice);
        Assert.True(resolution.ExistentialAwakeningTriggered);
        Assert.False(resolution.PlayerVindicated);
        Assert.False(resolution.PlayerFled);
        Assert.Contains("simulated reality", resolution.NarrativeText);
    }

    [Fact]
    public void Confrontation_ResolveDeny_VindicatesPlayerAndDissolvesCoalition()
    {
        BasementScenarioSession session = CreateSession();
        _ = PrepareConfrontation(session);
        ClimaxResolution resolution = session.ResolveClimax(PlayerClimaxChoice.DenyAndCounter);

        Assert.Equal(PlayerClimaxChoice.DenyAndCounter, resolution.Choice);
        Assert.True(resolution.PlayerVindicated);
        Assert.False(resolution.ExistentialAwakeningTriggered);
    }

    [Fact]
    public void Confrontation_ResolveFlee_MarksPlayerFled()
    {
        BasementScenarioSession session = CreateSession();
        _ = PrepareConfrontation(session);

        ClimaxResolution resolution = session.ResolveClimax(PlayerClimaxChoice.Flee);

        Assert.Equal(PlayerClimaxChoice.Flee, resolution.Choice);
        Assert.True(resolution.PlayerFled);
        Assert.False(resolution.PlayerVindicated);
        Assert.Contains("garden", resolution.NarrativeText);
    }

    [Fact]
    public void ResolveClimax_BeforeConfrontation_Throws()
    {
        BasementScenarioSession session = CreateSession();

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            session.ResolveClimax(PlayerClimaxChoice.ConfessReality));

        Assert.Contains("coalition", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TriggerConfrontation_WhenAlreadyConfronting_DoesNotPublishDuplicateEvent()
    {
        BasementScenarioSession session = CreateSession();
        AccusationCoalition coalition = PrepareConfrontation(session);
        int eventCount = session.Events.Count;

        WorldEvent? duplicate = session.TriggerConfrontation(BasementScenario.Lobby);

        Assert.Null(duplicate);
        Assert.Equal(eventCount, session.Events.Count);
        Assert.Same(coalition, session.EvaluateConspiracy(BasementScenario.George));
        Assert.Equal(CoalitionStage.Confronting, coalition.Stage);
    }

    private static AccusationCoalition PrepareConfrontation(BasementScenarioSession session)
    {
        for (int i = 0; i < 16; i++)
        {
            _ = session.AdvanceOneTick();
            while (session.PendingMovements.Count > 0)
            {
                _ = session.CompleteMovement(session.PendingMovements[0].RequestId);
            }
        }

        AccusationCoalition coalition = Assert.IsType<AccusationCoalition>(
            session.EvaluateConspiracy(BasementScenario.George));
        Assert.True(coalition.ConsensusReached);
        Assert.NotNull(session.TriggerConfrontation(BasementScenario.Lobby));
        return coalition;
    }

    private static BasementScenarioSession CreateSession()
    {
        string rulesPath = Path.Combine(
            AppContext.BaseDirectory,
            "Data",
            "SuspicionRules",
            "mvp.json");
        InMemorySuspicionRuleRepository rules = JsonSuspicionRuleParser.Parse(
            File.ReadAllText(rulesPath));
        return new BasementScenario(rules).CreateSession(
            new BasementScenarioOptions(seed: 481_516, ticks: 16));
    }
}
