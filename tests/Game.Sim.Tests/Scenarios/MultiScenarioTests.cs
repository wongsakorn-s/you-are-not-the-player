using Game.Sim.Entities;
using Game.Sim.Events;
using Game.Sim.Locations;
using Game.Sim.Memory;
using Game.Sim.Scenarios;
using Game.Sim.Suspicion;

namespace Game.Sim.Tests.Scenarios;

public sealed class MultiScenarioTests
{
    [Fact]
    public void RumorCascadeScenario_PropagatesLineageAcrossFourHops()
    {
        InMemorySuspicionRuleRepository rules = LoadRules();
        var scenario = new RumorCascadeScenario(rules);

        BasementScenarioResult result = scenario.Run(new BasementScenarioOptions(seed: 481_516, ticks: 16));

        Assert.NotEmpty(result.Events);
        Assert.True(result.Events.Count(e => e.Type == EventType.ShareInformation) >= 2);
    }

    [Fact]
    public void DeceptiveAlibiScenario_TamperAndInquiry_ProducesContradictionAndEvents()
    {
        InMemorySuspicionRuleRepository rules = LoadRules();
        var scenario = new DeceptiveAlibiScenario(rules);

        BasementScenarioResult result = scenario.Run(new BasementScenarioOptions(seed: 481_516, ticks: 16));

        Assert.NotEmpty(result.Events);
        Assert.Contains(result.Events, e => e.Type == EventType.Interaction && e.Tags.Contains(EventTag.Suspicious));
    }

    [Fact]
    public void RealityBreachScenario_ProducesConfrontationAndHighMetaSuspicion()
    {
        InMemorySuspicionRuleRepository rules = LoadRules();
        var scenario = new RealityBreachScenario(rules);

        BasementScenarioResult result = scenario.Run(new BasementScenarioOptions(seed: 481_516, ticks: 16));

        Assert.NotEmpty(result.Events);
        Assert.Contains(result.Events, e => e.Type == EventType.RealityAnomaly);
    }

    private static InMemorySuspicionRuleRepository LoadRules()
    {
        string rulesPath = Path.Combine(
            AppContext.BaseDirectory,
            "Data",
            "SuspicionRules",
            "mvp.json");
        return JsonSuspicionRuleParser.Parse(File.ReadAllText(rulesPath));
    }
}
