using Game.Sim.Entities;
using Game.Sim.Events;
using Game.Sim.Locations;
using Game.Sim.Memory;
using Game.Sim.Player;
using Game.Sim.Routines;
using Game.Sim.Suspicion;

namespace Game.Sim.Scenarios;

public sealed class RumorCascadeScenario
{
    public static readonly EntityId Anna = new("anna");
    public static readonly EntityId Bob = new("bob");
    public static readonly EntityId Charlie = new("charlie");
    public static readonly EntityId Dana = new("dana");
    public static readonly EntityId George = new("george");

    private readonly ISuspicionRuleRepository _rules;

    public RumorCascadeScenario(ISuspicionRuleRepository rules)
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

        // 1. Advance until Anna witnesses George in Basement and tells Bob
        for (int i = 0; i < 6; i++)
        {
            _ = session.AdvanceOneTick();
        }

        // 2. Transfer Bob to Kitchen, Charlie to Kitchen (hop 2)
        session.PlayerController.SetPlayerEntity(Bob);
        var moveBobToKitchen = session.PlayerController.RequestMove(new LocationId("kitchen"));
        if (moveBobToKitchen.Status == NpcMovementExecutionStatus.Pending && moveBobToKitchen.Movement is not null)
        {
            _ = session.CompleteMovement(moveBobToKitchen.Movement.RequestId);
        }

        session.PlayerController.SetPlayerEntity(Charlie);
        var moveCharlieToKitchen = session.PlayerController.RequestMove(new LocationId("kitchen"));
        if (moveCharlieToKitchen.Status == NpcMovementExecutionStatus.Pending && moveCharlieToKitchen.Movement is not null)
        {
            _ = session.CompleteMovement(moveCharlieToKitchen.Movement.RequestId);
        }

        // Bob talks to Charlie about George
        session.PlayerController.SetPlayerEntity(Bob);
        PlayerJournal bobJournal = session.GetPlayerJournal(Bob);
        if (bobJournal.Entries.Count > 0)
        {
            _ = session.PlayerController.Talk(new DialogueRequest(
                DialogueActionKind.ShareRumor,
                requester: Bob,
                partner: Charlie,
                subject: George,
                memoryToShare: bobJournal.Entries[0].Id));
        }
        else
        {
            _ = session.PlayerController.Talk(new DialogueRequest(
                DialogueActionKind.AskAboutSubject,
                requester: Bob,
                partner: Charlie,
                subject: George));
        }

        // 3. Transfer Charlie to Hallway, Dana to Hallway (hop 3)
        session.PlayerController.SetPlayerEntity(Charlie);
        var moveCharlieToHallway = session.PlayerController.RequestMove(new LocationId("hallway"));
        if (moveCharlieToHallway.Status == NpcMovementExecutionStatus.Pending && moveCharlieToHallway.Movement is not null)
        {
            _ = session.CompleteMovement(moveCharlieToHallway.Movement.RequestId);
        }

        session.PlayerController.SetPlayerEntity(Dana);
        var moveDanaToHallway = session.PlayerController.RequestMove(new LocationId("hallway"));
        if (moveDanaToHallway.Status == NpcMovementExecutionStatus.Pending && moveDanaToHallway.Movement is not null)
        {
            _ = session.CompleteMovement(moveDanaToHallway.Movement.RequestId);
        }

        // Charlie talks to Dana about George
        session.PlayerController.SetPlayerEntity(Charlie);
        PlayerJournal charlieJournal = session.GetPlayerJournal(Charlie);
        if (charlieJournal.Entries.Count > 0)
        {
            _ = session.PlayerController.Talk(new DialogueRequest(
                DialogueActionKind.ShareRumor,
                requester: Charlie,
                partner: Dana,
                subject: George,
                memoryToShare: charlieJournal.Entries[0].Id));
        }
        else
        {
            _ = session.PlayerController.Talk(new DialogueRequest(
                DialogueActionKind.AskAboutSubject,
                requester: Charlie,
                partner: Dana,
                subject: George));
        }

        // Run remaining ticks
        while (!session.IsComplete && session.Now.Tick < options.Ticks)
        {
            _ = session.AdvanceOneTick();
        }

        return session.BuildResult();
    }
}
