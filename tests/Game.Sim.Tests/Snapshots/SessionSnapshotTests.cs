using Game.Sim.Actions;
using Game.Sim.Logging;
using Game.Sim.Scenarios;
using Game.Sim.Snapshots;
using Game.Sim.Suspicion;

namespace Game.Sim.Tests.Snapshots;

public sealed class SessionSnapshotTests
{
    [Fact]
    public void Snapshot_SerializeAndDeserialize_PreservesAllFields()
    {
        BasementScenarioSession session = CreateSession();
        _ = session.AdvanceOneTick();
        _ = session.AdvanceOneTick();

        foreach (MovementSnapshot movement in session.PendingMovements.ToArray())
        {
            _ = session.CompleteMovement(movement.RequestId);
        }

        SessionSnapshot snapshot = session.CaptureSnapshot();
        string json = SessionSnapshotSerializer.ToJson(snapshot);
        SessionSnapshot roundtrip = SessionSnapshotSerializer.FromJson(json);

        Assert.Equal(snapshot.Metadata.Seed, roundtrip.Metadata.Seed);
        Assert.Equal(snapshot.Metadata.CurrentTick, roundtrip.Metadata.CurrentTick);
        Assert.Equal(snapshot.Metadata.Scenario, roundtrip.Metadata.Scenario);
        Assert.Equal(snapshot.Metadata.Phase, roundtrip.Metadata.Phase);
        Assert.Equal(snapshot.Metadata.ActivePlayerActor, roundtrip.Metadata.ActivePlayerActor);
        Assert.Equal(snapshot.Entities.Count, roundtrip.Entities.Count);
        Assert.Equal(snapshot.Events.Count, roundtrip.Events.Count);
        Assert.Equal(snapshot.Memories.Count, roundtrip.Memories.Count);
    }

    [Fact]
    public void Session_CaptureAndRestore_PreservesWorldAndSuspicionState()
    {
        BasementScenarioSession session = CreateSession();

        // Advance 6 ticks completing all movements
        for (int i = 0; i < 6; i++)
        {
            _ = session.AdvanceOneTick();
            foreach (MovementSnapshot movement in session.PendingMovements.ToArray())
            {
                _ = session.CompleteMovement(movement.RequestId);
            }
        }

        SessionSnapshot snapshot = session.CaptureSnapshot();
        string json = SessionSnapshotSerializer.ToJson(snapshot);
        SessionSnapshot deserialized = SessionSnapshotSerializer.FromJson(json);

        string rulesPath = Path.Combine(AppContext.BaseDirectory, "Data", "SuspicionRules", "mvp.json");
        InMemorySuspicionRuleRepository rules = JsonSuspicionRuleParser.Parse(File.ReadAllText(rulesPath));
        BasementScenarioSession restored = BasementScenarioSession.FromSnapshot(deserialized, rules);

        Assert.Equal(session.Now.Tick, restored.Now.Tick);
        Assert.Equal(session.Phase, restored.Phase);
        Assert.Equal(session.Events.Count, restored.Events.Count);
        Assert.Equal(session.GetLogicalLocation(BasementScenario.George), restored.GetLogicalLocation(BasementScenario.George));
        Assert.Equal(session.GetLogicalLocation(BasementScenario.Anna), restored.GetLogicalLocation(BasementScenario.Anna));

        SuspicionSnapshot originalSuspicion = session.GetSuspicion(BasementScenario.Anna, BasementScenario.George);
        SuspicionSnapshot restoredSuspicion = restored.GetSuspicion(BasementScenario.Anna, BasementScenario.George);
        Assert.Equal(originalSuspicion.Evidence.Count, restoredSuspicion.Evidence.Count);
        Assert.Equal(originalSuspicion.Vector.RoleDeviation, restoredSuspicion.Vector.RoleDeviation);
    }

    [Fact]
    public void Session_RestoreAtTick_ContinuesDeterministicExecution_MatchesContinuousRun()
    {
        // 1. Run continuous session for 16 ticks with auto-complete movements
        string rulesPath = Path.Combine(AppContext.BaseDirectory, "Data", "SuspicionRules", "mvp.json");
        InMemorySuspicionRuleRepository rules = JsonSuspicionRuleParser.Parse(File.ReadAllText(rulesPath));

        BasementScenarioSession continuousSession = new BasementScenario(rules).CreateSession(
            new BasementScenarioOptions(seed: 481_516, ticks: 16),
            autoCompleteMovements: true);

        while (!continuousSession.IsComplete && continuousSession.Now.Tick < 20)
        {
            _ = continuousSession.AdvanceOneTick();
        }

        string continuousFingerprint = WorldEventTrace.ComputeSha256(continuousSession.Events);

        // 2. Run branched session up to tick 6, capture snapshot, and restore into new session
        BasementScenarioSession branchSession = new BasementScenario(rules).CreateSession(
            new BasementScenarioOptions(seed: 481_516, ticks: 16),
            autoCompleteMovements: true);

        for (int i = 0; i < 6; i++)
        {
            _ = branchSession.AdvanceOneTick();
        }

        SessionSnapshot snapshot = branchSession.CaptureSnapshot();
        string json = SessionSnapshotSerializer.ToJson(snapshot);
        SessionSnapshot loadedSnapshot = SessionSnapshotSerializer.FromJson(json);

        BasementScenarioSession restoredSession = BasementScenarioSession.FromSnapshot(
            loadedSnapshot,
            rules,
            autoCompleteMovements: true);

        while (!restoredSession.IsComplete && restoredSession.Now.Tick < 20)
        {
            _ = restoredSession.AdvanceOneTick();
        }

        string restoredFingerprint = WorldEventTrace.ComputeSha256(restoredSession.Events);

        // 3. Verify exact 100% deterministic event hash match
        Assert.Equal(continuousFingerprint, restoredFingerprint);
        Assert.Equal(continuousSession.Events.Count, restoredSession.Events.Count);
        Assert.Equal(continuousSession.Now.Tick, restoredSession.Now.Tick);
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
