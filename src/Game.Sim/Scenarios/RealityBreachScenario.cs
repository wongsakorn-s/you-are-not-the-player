using Game.Sim.Conspiracy;
using Game.Sim.Entities;
using Game.Sim.Events;
using Game.Sim.Locations;
using Game.Sim.Memory;
using Game.Sim.Player;
using Game.Sim.Routines;
using Game.Sim.Suspicion;

namespace Game.Sim.Scenarios;

public sealed class RealityBreachScenario
{
    public static readonly EntityId George = new("george");
    public static readonly EntityId Anna = new("anna");
    public static readonly EntityId Bob = new("bob");
    public static readonly EntityId Dana = new("dana");
    public static readonly LocationId Lobby = new("lobby");
    public static readonly LocationId SecurityRoom = new("security-room");

    private readonly ISuspicionRuleRepository _rules;

    public RealityBreachScenario(ISuspicionRuleRepository rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        _rules = rules;
    }

    public BasementScenarioSession CreateSession(
        BasementScenarioOptions options,
        bool autoCompleteMovements = true)
    {
        var session = new BasementScenarioSession(_rules, options, autoCompleteMovements);
        return session;
    }

    public BasementScenarioResult Run(BasementScenarioOptions options)
    {
        BasementScenarioSession session = CreateSession(options, autoCompleteMovements: true);

        // 1. Trigger Save Reload Deja Vu in Lobby
        _ = session.TriggerSaveReloadAnomaly(George);

        // 2. Move Dana to Security Room
        session.PlayerController.SetPlayerEntity(Dana);
        var moveDana = session.PlayerController.RequestMove(SecurityRoom);
        if (moveDana.Status == NpcMovementExecutionStatus.Pending && moveDana.Movement is not null)
        {
            _ = session.CompleteMovement(moveDana.Movement.RequestId);
        }

        // 3. Fast travel / blink George into Security Room
        _ = session.TriggerFastTravelAnomaly(George, SecurityRoom);

        // 4. Trigger second Save Reload Anomaly
        _ = session.TriggerSaveReloadAnomaly(George);

        // 5. Evaluate conspiracy and trigger confrontation
        AccusationCoalition? coalition = session.EvaluateConspiracy(George);
        if (coalition is not null && coalition.ConsensusReached)
        {
            _ = session.TriggerConfrontation(Lobby);
        }

        while (!session.IsComplete && session.Now.Tick < options.Ticks)
        {
            _ = session.AdvanceOneTick();
        }

        return session.BuildResult();
    }
}
