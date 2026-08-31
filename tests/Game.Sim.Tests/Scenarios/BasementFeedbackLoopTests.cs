using Game.Sim.Brain;
using Game.Sim.Events;
using Game.Sim.Logging;
using Game.Sim.Memory;
using Game.Sim.Scenarios;
using Game.Sim.Suspicion;

namespace Game.Sim.Tests.Scenarios;

public sealed class BasementFeedbackLoopTests
{
    [Fact]
    public void Scenario_ClosesEventMemoryRumorSuspicionBehaviorLoop()
    {
        BasementScenarioResult result = RunScenario(ticks: 16);

        Assert.Equal(16, result.CompletedAt.Tick);
        Assert.Equal(6, result.Actors.Count);
        Assert.Contains(result.Events, worldEvent =>
            worldEvent.Actor == BasementScenario.George &&
            worldEvent.Type == EventType.BoundaryProbe);
        Assert.Equal(MemoryKind.Episodic, result.AnnaMemory.Kind);
        Assert.Equal(MemoryKind.Social, result.BobRumor.Kind);
        Assert.Equal(result.RestrictedEntry.Id, result.BobRumor.RootEventId);
        Assert.Equal(BasementScenario.Anna, result.BobRumor.InformationSource);
        Assert.Equal(19.0f, result.AnnaSuspicion.Vector.RoleDeviation, precision: 5);
        Assert.Equal(7.6f, result.AnnaSuspicion.Vector.Secrecy, precision: 5);
        Assert.Equal(16.15f, result.BobSuspicion.Vector.RoleDeviation, precision: 5);
        Assert.Equal(6.46f, result.BobSuspicion.Vector.Secrecy, precision: 5);
        Assert.Equal(3, result.AnnaFirstSuspicionAt.Tick);
        Assert.Equal(4, result.BobFirstSuspicionAt.Tick);
        Assert.Equal(GoalType.ShareSuspicion, result.AnnaInitialDecision.Goal.Type);
        Assert.Equal(BasementScenario.Bob, result.AnnaInitialDecision.Goal.InteractionPartner);
        Assert.Equal(GoalType.FollowTarget, result.BobInitialDecision.Goal.Type);
        Assert.Equal(BasementScenario.George, result.BobInitialDecision.Goal.Target);
        Assert.Equal(BasementScenario.Basement, result.BobInitialDecision.Goal.Destination);
        Assert.Contains(result.Events, worldEvent =>
            worldEvent.Actor == BasementScenario.Anna &&
            worldEvent.Type == EventType.ShareInformation &&
            worldEvent.Target == BasementScenario.Bob);
        Assert.Contains(result.Events, worldEvent =>
            worldEvent.Actor == BasementScenario.Bob &&
            worldEvent.Type == EventType.EnterLocation &&
            worldEvent.Location == BasementScenario.Basement);
        Assert.Equal(BasementScenario.Basement, result.BobFinalLocation);
        Assert.Equal(BasementScenario.Basement, result.GeorgeFinalLocation);
    }

    [Fact]
    public void Scenario_WithSameInputsProducesSameTraceFingerprint()
    {
        BasementScenarioResult first = RunScenario(ticks: 100);
        BasementScenarioResult second = RunScenario(ticks: 100);

        Assert.Equal(
            WorldEventTrace.ComputeSha256(first.Events),
            WorldEventTrace.ComputeSha256(second.Events));
    }

    [Fact]
    public void Scenario_CanRunTenThousandTicksHeadlessly()
    {
        BasementScenarioResult result = RunScenario(ticks: 10_000);

        Assert.Equal(10_000, result.CompletedAt.Tick);
        Assert.NotEmpty(result.Events);
        Assert.Equal(49_986, result.Decisions.Count);
    }

    [Fact]
    public void Options_RejectTooFewTicks()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new BasementScenarioOptions(seed: 1, ticks: 3));
    }

    private static BasementScenarioResult RunScenario(int ticks)
    {
        string rulesPath = Path.Combine(
            AppContext.BaseDirectory,
            "Data",
            "SuspicionRules",
            "mvp.json");
        InMemorySuspicionRuleRepository rules = JsonSuspicionRuleParser.Parse(
            File.ReadAllText(rulesPath));
        return new BasementScenario(rules).Run(
            new BasementScenarioOptions(seed: 481_516, ticks));
    }
}
