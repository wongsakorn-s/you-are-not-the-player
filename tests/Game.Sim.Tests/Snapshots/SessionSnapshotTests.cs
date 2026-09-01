using Game.Sim.Actions;
using Game.Sim.Conspiracy;
using Game.Sim.Locations;
using Game.Sim.Logging;
using Game.Sim.Objects;
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
        Assert.Equal(snapshot.Objects?.Count, roundtrip.Objects?.Count);
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

    [Fact]
    public void Session_Restore_PreservesObjectAndConfrontationState()
    {
        BasementScenarioSession session = CreateSession();
        AdvanceToConsensus(session);
        AccusationCoalition coalition = Assert.IsType<AccusationCoalition>(
            session.EvaluateConspiracy(BasementScenario.George));
        Assert.True(coalition.ConsensusReached);

        session.PlayerController.SetPlayerEntity(BasementScenario.George);
        _ = session.PlayerController.RequestMove(new LocationId("kitchen"));
        while (session.PendingMovements.Count > 0)
        {
            _ = session.CompleteMovement(session.PendingMovements[0].RequestId);
        }

        ObjectActionResult objectResult = session.TamperObject("kitchen-pantry-safe", "chef-key");
        Assert.True(objectResult.Succeeded);
        Assert.NotNull(session.TriggerConfrontation(BasementScenario.Lobby));

        string json = SessionSnapshotSerializer.ToJson(session.CaptureSnapshot());
        BasementScenarioSession restored = BasementScenarioSession.FromSnapshot(
            SessionSnapshotSerializer.FromJson(json),
            LoadRules());

        InteractiveObject restoredSafe = Assert.IsType<InteractiveObject>(
            restored.Objects.GetObject("kitchen-pantry-safe"));
        Assert.False(restoredSafe.IsLocked);
        Assert.True(restoredSafe.IsTampered);
        Assert.Equal(CoalitionStage.Confronting, restored.ActiveCoalition?.Stage);
        Assert.True(restored.CanResolveClimax(BasementScenario.George));
        Assert.Equal(BasementScenario.George, restored.PlayerController.PlayerEntity);
    }

    [Fact]
    public void SnapshotValidator_RejectsUnknownActivePlayer()
    {
        SessionSnapshot snapshot = CreateSession().CaptureSnapshot();
        SessionSnapshot invalid = snapshot with
        {
            Metadata = snapshot.Metadata with { ActivePlayerActor = "missing-player" },
        };

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            SessionSnapshotSerializer.ToJson(invalid));

        Assert.Contains("active player", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SnapshotFile_SaveTwice_ReplacesFileAndLeavesNoTemporaryFile()
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            $"yanp-snapshot-tests-{Guid.NewGuid():N}");
        string savePath = Path.Combine(directory, "quicksave.json");

        try
        {
            BasementScenarioSession session = CreateSession();
            SessionSnapshotSerializer.SaveToFile(session.CaptureSnapshot(), savePath);
            _ = session.AdvanceOneTick();
            SessionSnapshotSerializer.SaveToFile(session.CaptureSnapshot(), savePath);

            SessionSnapshot loaded = SessionSnapshotSerializer.LoadFromFile(savePath);
            Assert.Equal(1, loaded.Metadata.CurrentTick);
            Assert.Empty(Directory.GetFiles(directory, "*.tmp"));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    private static void AdvanceToConsensus(BasementScenarioSession session)
    {
        for (int i = 0; i < 16; i++)
        {
            _ = session.AdvanceOneTick();
            while (session.PendingMovements.Count > 0)
            {
                _ = session.CompleteMovement(session.PendingMovements[0].RequestId);
            }
        }
    }

    private static InMemorySuspicionRuleRepository LoadRules()
    {
        string rulesPath = Path.Combine(AppContext.BaseDirectory, "Data", "SuspicionRules", "mvp.json");
        return JsonSuspicionRuleParser.Parse(File.ReadAllText(rulesPath));
    }

    private static BasementScenarioSession CreateSession()
    {
        return new BasementScenario(LoadRules()).CreateSession(
            new BasementScenarioOptions(seed: 481_516, ticks: 16));
    }
}
