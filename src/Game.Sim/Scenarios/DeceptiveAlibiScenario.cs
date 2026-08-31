using Game.Sim.Entities;
using Game.Sim.Events;
using Game.Sim.Locations;
using Game.Sim.Memory;
using Game.Sim.Objects;
using Game.Sim.Player;
using Game.Sim.Routines;
using Game.Sim.Suspicion;

namespace Game.Sim.Scenarios;

public sealed class DeceptiveAlibiScenario
{
    public static readonly EntityId Bob = new("bob");
    public static readonly EntityId Charlie = new("charlie");
    public static readonly EntityId George = new("george");
    public static readonly LocationId Kitchen = new("kitchen");
    public static readonly LocationId Lobby = new("lobby");

    private readonly ISuspicionRuleRepository _rules;

    public DeceptiveAlibiScenario(ISuspicionRuleRepository rules)
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

        // 1. Move Charlie to Kitchen and tamper with safe using chef-key
        session.PlayerController.SetPlayerEntity(Charlie);
        var moveCharlie = session.PlayerController.RequestMove(Kitchen);
        if (moveCharlie.Status == NpcMovementExecutionStatus.Pending && moveCharlie.Movement is not null)
        {
            _ = session.CompleteMovement(moveCharlie.Movement.RequestId);
        }

        ObjectActionResult tamperResult = session.TamperObject("kitchen-pantry-safe", keyId: "chef-key");

        // 2. Move Bob to Kitchen
        session.PlayerController.SetPlayerEntity(Bob);
        var moveBob = session.PlayerController.RequestMove(Kitchen);
        if (moveBob.Status == NpcMovementExecutionStatus.Pending && moveBob.Movement is not null)
        {
            _ = session.CompleteMovement(moveBob.Movement.RequestId);
        }

        // Bob inspects the tampered safe
        ObjectActionResult inspectResult = session.InspectObject("kitchen-pantry-safe");

        // 3. Bob inquires Charlie about the safe
        DialogueOutcome inquiry = session.PlayerController.Talk(new DialogueRequest(
            DialogueActionKind.InquireAboutObject,
            requester: Bob,
            partner: Charlie,
            targetObjectId: "kitchen-pantry-safe"));

        // Advance remaining ticks
        while (!session.IsComplete && session.Now.Tick < options.Ticks)
        {
            _ = session.AdvanceOneTick();
        }

        return session.BuildResult();
    }
}
