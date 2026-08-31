using Game.Sim.Actions;
using Game.Sim.Events;
using Game.Sim.Scenarios;
using Game.Sim.Suspicion;

namespace Game.Sim.Tests.Scenarios;

public sealed class BasementScenarioSessionTests
{
    [Fact]
    public void MovementAndObserverEffectsWaitForAcknowledgement()
    {
        BasementScenarioSession session = CreateSession();
        _ = session.AdvanceOneTick();
        _ = session.AdvanceOneTick();
        MovementSnapshot annaMovement = Assert.Single(session.PendingMovements);

        Assert.Equal(BasementScenario.Anna, annaMovement.Actor);
        Assert.Equal(BasementScenario.Lobby, session.GetLogicalLocation(BasementScenario.Anna));
        Assert.DoesNotContain(session.Events, worldEvent =>
            worldEvent.Actor == BasementScenario.Anna &&
            worldEvent.Type == EventType.EnterLocation);

        _ = session.AdvanceOneTick();
        Assert.Equal(BasementSessionPhase.WaitingForWitness, session.Phase);
        _ = session.CompleteMovement(annaMovement.RequestId);

        Assert.Equal(BasementScenario.Basement, session.GetLogicalLocation(BasementScenario.Anna));
        Assert.Contains(session.Events, worldEvent =>
            worldEvent.Actor == BasementScenario.Anna &&
            worldEvent.Type == EventType.EnterLocation &&
            worldEvent.Time.Tick == 3);

        _ = session.AdvanceOneTick();
        MovementSnapshot georgeMovement = Assert.Single(session.PendingMovements);
        Assert.Equal(BasementScenario.George, georgeMovement.Actor);
        Assert.DoesNotContain(session.Events, worldEvent =>
            worldEvent.Actor == BasementScenario.George &&
            worldEvent.Type == EventType.BoundaryProbe);

        int decisionsBeforeWait = session.Decisions.Count;
        _ = session.AdvanceOneTick();
        Assert.Equal(decisionsBeforeWait, session.Decisions.Count);

        _ = session.CompleteMovement(georgeMovement.RequestId);

        Assert.Equal(BasementScenario.Basement, session.GetLogicalLocation(BasementScenario.George));
        Assert.Contains(session.Events, worldEvent =>
            worldEvent.Actor == BasementScenario.George &&
            worldEvent.Type == EventType.BoundaryProbe &&
            worldEvent.Time.Tick == 5);
        Assert.NotEmpty(session.GetSuspicion(
            BasementScenario.Anna,
            BasementScenario.George).Evidence);
    }

    [Fact]
    public void Session_CompletesFeedbackLoopWithExternalAcknowledgements()
    {
        BasementScenarioSession session = CreateSession();
        int safetyTicks = 0;

        while (!session.IsComplete && safetyTicks < 100)
        {
            _ = session.AdvanceOneTick();
            foreach (MovementSnapshot movement in session.PendingMovements.ToArray())
            {
                _ = session.CompleteMovement(movement.RequestId);
            }

            safetyTicks++;
        }

        BasementScenarioResult result = session.BuildResult();

        Assert.True(session.IsComplete);
        Assert.Equal(16, result.CompletedAt.Tick);
        Assert.Equal(BasementScenario.Basement, result.GeorgeFinalLocation);
        Assert.Equal(BasementScenario.Basement, result.BobFinalLocation);
        Assert.Equal(EventType.EnterLocation, result.RestrictedEntry.Type);
        Assert.Equal(result.RestrictedEntry.Id, result.BobRumor.RootEventId);
    }

    [Fact]
    public void FailedPhysicalMovementCanBeRetriedWithoutCommittingLocation()
    {
        BasementScenarioSession session = CreateSession();
        _ = session.AdvanceOneTick();
        _ = session.AdvanceOneTick();
        MovementSnapshot firstAttempt = Assert.Single(session.PendingMovements);

        MovementSnapshot failed = session.FailMovement(firstAttempt.RequestId);

        Assert.Equal(MovementStatus.Failed, failed.Status);
        Assert.Equal(BasementScenario.Lobby, session.GetLogicalLocation(BasementScenario.Anna));
        Assert.Equal(BasementSessionPhase.WitnessMovement, session.Phase);

        _ = session.AdvanceOneTick();
        MovementSnapshot retry = Assert.Single(session.PendingMovements);
        Assert.True(retry.RequestId.Value > firstAttempt.RequestId.Value);
    }

    [Fact]
    public void DelayedFeedbackMovementRunsObserverAtArrivalTime()
    {
        BasementScenarioSession session = CreateSession();
        CompleteUntilFeedbackLoop(session);

        _ = session.AdvanceOneTick();
        MovementSnapshot annaMovement = Assert.Single(session.PendingMovements);
        Assert.Equal(BasementScenario.Anna, annaMovement.Actor);
        long requestedAt = session.Now.Tick;
        _ = session.AdvanceOneTick();
        _ = session.AdvanceOneTick();

        _ = session.CompleteMovement(annaMovement.RequestId);

        WorldEvent share = Assert.Single(session.Events, worldEvent =>
            worldEvent.Actor == BasementScenario.Anna &&
            worldEvent.Type == EventType.ShareInformation);
        Assert.True(share.Time.Tick > requestedAt);
        Assert.Equal(session.Now.Tick, share.Time.Tick);
    }

    [Fact]
    public void Session_CompletesAtTickTwentyNineWithSixTickPhysicalLatency()
    {
        BasementScenarioSession session = CreateSession();
        var requestedAt = new Dictionary<MovementRequestId, long>();

        while (!session.IsComplete && session.Now.Tick < 40)
        {
            _ = session.AdvanceOneTick();
            foreach (MovementSnapshot movement in session.PendingMovements)
            {
                requestedAt.TryAdd(movement.RequestId, session.Now.Tick);
            }

            foreach (MovementSnapshot movement in session.PendingMovements
                         .Where(movement => session.Now.Tick - requestedAt[movement.RequestId] >= 6)
                         .ToArray())
            {
                _ = session.CompleteMovement(movement.RequestId);
            }
        }

        Assert.True(session.IsComplete);
        Assert.Equal(29, session.Now.Tick);
        Assert.Equal(BasementSessionPhase.Completed, session.Phase);
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

    private static void CompleteUntilFeedbackLoop(BasementScenarioSession session)
    {
        while (session.Phase != BasementSessionPhase.FeedbackLoop)
        {
            _ = session.AdvanceOneTick();
            foreach (MovementSnapshot movement in session.PendingMovements.ToArray())
            {
                _ = session.CompleteMovement(movement.RequestId);
            }
        }
    }
}
