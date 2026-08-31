using Game.Sim.Anomalies;
using Game.Sim.Entities;
using Game.Sim.Events;
using Game.Sim.Locations;
using Game.Sim.Memory;
using Game.Sim.Scenarios;
using Game.Sim.Snapshots;
using Game.Sim.Suspicion;

namespace Game.Sim.Tests.Anomalies;

public sealed class RealityAnomalyTests
{
    [Fact]
    public void SaveReloadAnomaly_ProducesEvent_AndIncreasesMetaAndImpossibleSuspicion()
    {
        BasementScenarioSession session = CreateSession();

        // George and Anna are both in the Lobby
        WorldEvent anomalyEvent = session.TriggerSaveReloadAnomaly(BasementScenario.George);

        Assert.NotNull(anomalyEvent);
        Assert.Equal(EventType.RealityAnomaly, anomalyEvent.Type);
        Assert.IsType<RealityAnomalyPayload>(anomalyEvent.Payload);

        var payload = (RealityAnomalyPayload)anomalyEvent.Payload;
        Assert.Equal(AnomalyKind.SaveReload, payload.Anomaly);

        // Anna perceives the unnatural temporal anomaly and becomes suspicious of George's meta-behavior
        SuspicionSnapshot annaSuspicion = session.GetSuspicion(BasementScenario.Anna, BasementScenario.George);
        Assert.NotEmpty(annaSuspicion.Evidence);
        Assert.True(annaSuspicion.Vector.ImpossibleBehavior > 0);
        Assert.True(annaSuspicion.Vector.MetaBehavior > 0);
    }

    [Fact]
    public void FastTravelAnomaly_ProducesTheBlinkEvent_AndIncreasesImpossibleSuspicion()
    {
        BasementScenarioSession session = CreateSession();

        // Charlie is in Lobby, George fast-travels / blinks into Lobby
        WorldEvent blinkEvent = session.TriggerFastTravelAnomaly(BasementScenario.George, BasementScenario.Lobby);

        Assert.NotNull(blinkEvent);
        Assert.Equal(EventType.RealityAnomaly, blinkEvent.Type);

        var payload = (RealityAnomalyPayload)blinkEvent.Payload;
        Assert.Equal(AnomalyKind.TheBlink, payload.Anomaly);

        // Charlie perceives the impossible movement
        SuspicionSnapshot charlieSuspicion = session.GetSuspicion(BasementScenario.Charlie, BasementScenario.George);
        Assert.NotEmpty(charlieSuspicion.Evidence);
        Assert.True(charlieSuspicion.Vector.ImpossibleBehavior >= 30);
    }

    [Fact]
    public void RealityAnomaly_SnapshotRoundtrip_PreservesPayload()
    {
        BasementScenarioSession session = CreateSession();
        _ = session.TriggerSaveReloadAnomaly(BasementScenario.George);

        SessionSnapshot snapshot = session.CaptureSnapshot();
        string json = SessionSnapshotSerializer.ToJson(snapshot);
        SessionSnapshot restored = SessionSnapshotSerializer.FromJson(json);

        WorldEventSnapshot anomalySnapshot = Assert.Single(
            restored.Events,
            e => e.Type == "RealityAnomaly");

        Assert.Equal("realityAnomaly", anomalySnapshot.Payload.Type);
        Assert.Equal("SaveReload", anomalySnapshot.Payload.Anomaly);
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
