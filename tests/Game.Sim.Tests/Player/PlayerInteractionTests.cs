using Game.Sim.Actions;
using Game.Sim.Entities;
using Game.Sim.Events;
using Game.Sim.Locations;
using Game.Sim.Memory;
using Game.Sim.Objects;
using Game.Sim.Player;
using Game.Sim.Routines;
using Game.Sim.Scenarios;
using Game.Sim.Suspicion;

namespace Game.Sim.Tests.Player;

public sealed class PlayerInteractionTests
{
    private static readonly LocationId Hallway = new("hallway");

    [Fact]
    public void PlayerController_RequestMove_ValidRoute_RequestsLiveMovement()
    {
        BasementScenarioSession session = CreateSession();
        PlayerSessionController controller = session.PlayerController;
        controller.SetPlayerEntity(BasementScenario.George);

        Assert.Equal(BasementScenario.Lobby, controller.CurrentLocation);
        Assert.False(controller.HasActiveMovement);

        NpcMovementExecution request = controller.RequestMove(BasementScenario.Basement);

        Assert.Equal(NpcMovementExecutionStatus.Pending, request.Status);
        Assert.NotNull(request.Movement);
        Assert.Equal(MovementStatus.Navigating, request.Movement.Status);
        Assert.Equal(BasementScenario.George, request.Movement.Actor);
        Assert.Equal(BasementScenario.Basement, request.Movement.Destination);
        Assert.True(controller.HasActiveMovement);

        _ = session.CompleteMovement(request.Movement.RequestId);

        Assert.Equal(BasementScenario.Basement, controller.CurrentLocation);
        Assert.False(controller.HasActiveMovement);
    }

    [Fact]
    public void PlayerController_Interact_ProducesEventAndAppearsInSession()
    {
        BasementScenarioSession session = CreateSession();
        PlayerSessionController controller = session.PlayerController;
        controller.SetPlayerEntity(BasementScenario.George);

        EventActionResult result = controller.Interact("lobby-reception-bell");

        Assert.NotNull(result.SourceEvent);
        Assert.Equal(BasementScenario.George, result.SourceEvent.Actor);
        Assert.Equal(EventType.Interaction, result.SourceEvent.Type);
        Assert.Equal(BasementScenario.Lobby, result.SourceEvent.Location);
    }

    [Fact]
    public void PlayerController_ProbeBoundary_ProducesBoundaryProbeEvent()
    {
        BasementScenarioSession session = CreateSession();
        PlayerSessionController controller = session.PlayerController;
        controller.SetPlayerEntity(BasementScenario.George);

        EventActionResult result = controller.ProbeBoundary("basement-door");

        Assert.NotNull(result.SourceEvent);
        Assert.Equal(BasementScenario.George, result.SourceEvent.Actor);
        Assert.Equal(EventType.BoundaryProbe, result.SourceEvent.Type);
    }

    [Fact]
    public void PlayerController_Dialogue_AskAboutSubject_TransfersSocialMemoryAndLineage()
    {
        BasementScenarioSession session = CreateSession();

        // 1. Advance until Anna witnesses George in Basement
        _ = session.AdvanceOneTick();
        _ = session.AdvanceOneTick();
        MovementSnapshot annaMove = Assert.Single(session.PendingMovements);
        _ = session.AdvanceOneTick();
        _ = session.CompleteMovement(annaMove.RequestId);

        _ = session.AdvanceOneTick();
        MovementSnapshot georgeMove = Assert.Single(session.PendingMovements);
        _ = session.AdvanceOneTick();
        _ = session.CompleteMovement(georgeMove.RequestId);

        // Anna returns to Lobby
        _ = session.AdvanceOneTick();
        MovementSnapshot annaReturn = Assert.Single(session.PendingMovements);
        _ = session.CompleteMovement(annaReturn.RequestId);

        // 2. Set player as Bob in Lobby, talk to Anna about George
        PlayerSessionController controller = session.PlayerController;
        controller.SetPlayerEntity(BasementScenario.Bob);

        DialogueOutcome outcome = controller.Talk(new DialogueRequest(
            DialogueActionKind.AskAboutSubject,
            requester: BasementScenario.Bob,
            partner: BasementScenario.Anna,
            subject: BasementScenario.George));

        Assert.True(outcome.Succeeded);
        Assert.NotNull(outcome.TransferredMemory);
        Assert.Equal(MemoryKind.Social, outcome.TransferredMemory.Kind);
        Assert.Equal(BasementScenario.Anna, outcome.TransferredMemory.InformationSource);
        Assert.Equal(BasementScenario.George, outcome.TransferredMemory.Subject);
        Assert.NotNull(outcome.GeneratedEvent);
        Assert.Equal(EventType.AskInformation, outcome.GeneratedEvent.Type);

        // Verify Bob now has suspicion against George
        SuspicionSnapshot bobSuspicion = session.GetSuspicion(BasementScenario.Bob, BasementScenario.George);
        Assert.NotEmpty(bobSuspicion.Evidence);
        Assert.True(bobSuspicion.Vector.RoleDeviation > 0f);
    }

    [Fact]
    public void PlayerController_Dialogue_ShareRumor_TransfersMemoryToPartner()
    {
        BasementScenarioSession session = CreateSession();

        // Advance until Anna witnesses George in Basement
        _ = session.AdvanceOneTick();
        _ = session.AdvanceOneTick();
        MovementSnapshot annaMove = Assert.Single(session.PendingMovements);
        _ = session.AdvanceOneTick();
        _ = session.CompleteMovement(annaMove.RequestId);

        _ = session.AdvanceOneTick();
        MovementSnapshot georgeMove = Assert.Single(session.PendingMovements);
        _ = session.AdvanceOneTick();
        _ = session.CompleteMovement(georgeMove.RequestId);

        // Anna returns to Lobby where Charlie is present
        _ = session.AdvanceOneTick();
        MovementSnapshot annaReturn = Assert.Single(session.PendingMovements);
        _ = session.CompleteMovement(annaReturn.RequestId);

        // Anna is the player, shares memory with Charlie
        PlayerSessionController controller = session.PlayerController;
        controller.SetPlayerEntity(BasementScenario.Anna);

        MemoryRecord annaMemory = session.GetMemories(BasementScenario.Anna)
            .First(m => m.Subject == BasementScenario.George && m.Kind == MemoryKind.Episodic);

        DialogueOutcome outcome = controller.Talk(new DialogueRequest(
            DialogueActionKind.ShareRumor,
            requester: BasementScenario.Anna,
            partner: BasementScenario.Charlie,
            memoryToShare: annaMemory.Id));

        Assert.True(outcome.Succeeded);
        Assert.NotNull(outcome.TransferredMemory);
        Assert.Equal(MemoryKind.Social, outcome.TransferredMemory.Kind);
        Assert.Equal(annaMemory.RootEventId, outcome.TransferredMemory.RootEventId);
        Assert.NotNull(outcome.GeneratedEvent);
        Assert.Equal(EventType.ShareInformation, outcome.GeneratedEvent.Type);
        Assert.Equal(BasementScenario.Charlie, outcome.GeneratedEvent.Target);

        // If Anna tries to share the same rumor again with Charlie, Charlie already knows
        DialogueOutcome secondAttempt = controller.Talk(new DialogueRequest(
            DialogueActionKind.ShareRumor,
            requester: BasementScenario.Anna,
            partner: BasementScenario.Charlie,
            memoryToShare: annaMemory.Id));

        Assert.True(secondAttempt.Succeeded);
        Assert.Contains("already know", secondAttempt.Text);
    }

    [Fact]
    public void PlayerController_GetJournal_ReturnsKnownMemoriesAndSuspicion()
    {
        BasementScenarioSession session = CreateSession();
        PlayerSessionController controller = session.PlayerController;
        controller.SetPlayerEntity(BasementScenario.Anna);

        // Anna inspects lobby
        controller.Interact("lobby-desk");

        PlayerJournal journal = controller.GetJournal();

        Assert.Equal(BasementScenario.Anna, journal.PlayerEntity);
        Assert.Equal(BasementScenario.Lobby, journal.CurrentLocation);
        Assert.NotEmpty(journal.AdjacentLocations);
        Assert.Contains(Hallway, journal.AdjacentLocations);
        Assert.NotEmpty(journal.PresentEntities);
    }

    [Fact]
    public void PlayerController_Dialogue_FailsWhenNotInSameLocation()
    {
        BasementScenarioSession session = CreateSession();
        PlayerSessionController controller = session.PlayerController;
        controller.SetPlayerEntity(BasementScenario.George);

        // Move George to Basement while Anna is still in Lobby
        NpcMovementExecution move = controller.RequestMove(BasementScenario.Basement);
        Assert.NotNull(move.Movement);
        _ = session.CompleteMovement(move.Movement.RequestId);

        DialogueOutcome outcome = controller.Talk(new DialogueRequest(
            DialogueActionKind.InquireSchedule,
            requester: BasementScenario.George,
            partner: BasementScenario.Anna));

        Assert.False(outcome.Succeeded);
        Assert.Contains("not in the same location", outcome.FailureReason);
    }

    [Fact]
    public void PlayerController_Dialogue_InquireAboutObject_BobRevealsKeyLocation()
    {
        BasementScenarioSession session = CreateSession();
        PlayerSessionController controller = session.PlayerController;
        controller.SetPlayerEntity(BasementScenario.George);

        DialogueOutcome outcome = controller.InquireObject(BasementScenario.Bob, "kitchen-pantry-safe");

        Assert.True(outcome.Succeeded);
        Assert.Contains("statue in the Garden", outcome.Text);
        Assert.NotNull(outcome.TransferredMemory);
    }

    [Fact]
    public void PlayerController_Dialogue_ConfrontEvidence_ProducesDefensiveReaction()
    {
        BasementScenarioSession session = CreateSession();
        PlayerSessionController controller = session.PlayerController;
        controller.SetPlayerEntity(BasementScenario.George);

        // George inspects the guest registry, creating an episodic memory
        ObjectActionResult inspectResult = controller.InspectObject("lobby-guest-registry");
        Assert.True(inspectResult.Succeeded);
        Assert.NotNull(inspectResult.GeneratedEvent);

        PlayerJournal journal = controller.GetJournal();
        Assert.NotEmpty(journal.Entries);

        DialogueOutcome outcome = controller.ConfrontWithEvidence(BasementScenario.Anna, journal.Entries[0].Id);

        Assert.True(outcome.Succeeded);
        Assert.Contains("anna", outcome.Text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("george", outcome.Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PlayerController_Dialogue_InquireAboutUnknownObject_ProvidesEvasiveAnswer()
    {
        BasementScenarioSession session = CreateSession();
        PlayerSessionController controller = session.PlayerController;
        controller.SetPlayerEntity(BasementScenario.George);

        DialogueOutcome outcome = controller.InquireObject(BasementScenario.Anna, "non-existent-mystery-box");

        Assert.True(outcome.Succeeded);
        Assert.Contains("not familiar with that item", outcome.Text);
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
