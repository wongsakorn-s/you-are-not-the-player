using Game.Sim.Entities;
using Game.Sim.Locations;
using Game.Sim.Suspicion;

namespace Game.Sim.Scenarios;

public sealed class BasementScenario
{
    public static readonly EntityId Anna = new("anna");
    public static readonly EntityId Bob = new("bob");
    public static readonly EntityId George = new("george");
    public static readonly EntityId Charlie = new("charlie");
    public static readonly EntityId Dana = new("dana");
    public static readonly EntityId Evelyn = new("evelyn");
    public static readonly LocationId Lobby = new("lobby");
    public static readonly LocationId Basement = new("basement");

    private readonly ISuspicionRuleRepository _rules;

    public BasementScenario(ISuspicionRuleRepository rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        _rules = rules;
    }

    public BasementScenarioSession CreateSession(
        BasementScenarioOptions options,
        bool autoCompleteMovements = false) =>
        new(_rules, options, autoCompleteMovements);

    public BasementScenarioResult Run(BasementScenarioOptions options)
    {
        BasementScenarioSession session = CreateSession(
            options,
            autoCompleteMovements: true);
        while (!session.IsComplete)
        {
            _ = session.AdvanceOneTick();
        }

        return session.BuildResult();
    }
}
