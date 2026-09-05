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

    /// <summary>
    /// How far past the requested tick count the scripted milestone chain is given
    /// to resolve before the run is called stuck.
    /// </summary>
    public const int CompletionGraceTicks = 4_096;

    public BasementScenarioResult Run(BasementScenarioOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        BasementScenarioSession session = CreateSession(
            options,
            autoCompleteMovements: true);

        // Completion needs more than the tick count: the Anna/Bob milestone chain
        // has to resolve too. A generated SessionTruth can steer a character that
        // never satisfies it, and an unbounded loop would hang instead of saying so.
        long limit = options.Ticks + CompletionGraceTicks;
        while (!session.IsComplete && session.Now.Tick < limit)
        {
            _ = session.AdvanceOneTick();
        }

        if (!session.IsComplete)
        {
            throw new InvalidOperationException(
                $"The basement scenario did not complete within {limit} ticks " +
                $"(stuck in phase '{session.Phase}'). A generated case may not " +
                "satisfy the scripted milestone chain.");
        }

        return session.BuildResult();
    }
}
