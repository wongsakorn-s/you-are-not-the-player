using Game.Sim.Entities;
using Game.Sim.Events;
using Game.Sim.Locations;
using Game.Sim.Objects;
using Game.Sim.Player;
using Game.Sim.Scenarios;
using Game.Sim.Suspicion;

namespace Game.Sim.Tests.Objects;

public sealed class InteractiveObjectTests
{
    [Fact]
    public void Registry_ContainsDefaultHotelObjects()
    {
        HotelObjectRegistry registry = HotelObjectRegistry.CreateDefaultHotelObjects();

        Assert.NotEmpty(registry.AllObjects);
        Assert.NotNull(registry.GetObject("lobby-reception-bell"));
        Assert.NotNull(registry.GetObject("kitchen-pantry-safe"));
        Assert.NotNull(registry.GetObject("room201-briefcase"));
        Assert.NotNull(registry.GetObject("basement-incriminating-ledger"));
        Assert.NotNull(registry.GetObject("garden-statue-stash"));
        Assert.NotNull(registry.GetObject("security-cctv-terminal"));

        IReadOnlyList<InteractiveObject> lobbyObjects = registry.GetObjectsInLocation(new LocationId("lobby"));
        Assert.Equal(2, lobbyObjects.Count);
    }

    [Fact]
    public void Inspect_UnlockedObject_SucceedsAndDiscoversClue()
    {
        BasementScenarioSession session = CreateSession();
        PlayerSessionController controller = session.PlayerController;
        controller.SetPlayerEntity(BasementScenario.George);

        ObjectActionResult result = controller.InspectObject("lobby-guest-registry");

        Assert.True(result.Succeeded);
        Assert.NotNull(result.DiscoveredClue);
        Assert.Contains("registry lists guests", result.DiscoveredClue);
        Assert.NotNull(result.GeneratedEvent);
        Assert.Equal(EventType.Interaction, result.GeneratedEvent.Type);
    }

    [Fact]
    public void Inspect_LockedObject_FailsWithLockRequirement()
    {
        BasementScenarioSession session = CreateSession();
        PlayerSessionController controller = session.PlayerController;
        controller.SetPlayerEntity(BasementScenario.George);

        // Move to kitchen
        _ = controller.RequestMove(new LocationId("kitchen"));
        foreach (var movement in session.PendingMovements.ToArray())
        {
            _ = session.CompleteMovement(movement.RequestId);
        }

        ObjectActionResult result = controller.InspectObject("kitchen-pantry-safe");

        Assert.False(result.Succeeded);
        Assert.Contains("securely locked", result.Message);
        Assert.Contains("chef-key", result.Message);
    }

    [Fact]
    public void Tamper_WithCorrectKey_UnlocksAndDiscoversClue()
    {
        BasementScenarioSession session = CreateSession();
        PlayerSessionController controller = session.PlayerController;
        controller.SetPlayerEntity(BasementScenario.George);

        // Move to kitchen
        _ = controller.RequestMove(new LocationId("kitchen"));
        foreach (var movement in session.PendingMovements.ToArray())
        {
            _ = session.CompleteMovement(movement.RequestId);
        }

        // Tamper with key
        ObjectActionResult result = controller.TamperObject("kitchen-pantry-safe", "chef-key");

        Assert.True(result.Succeeded);
        Assert.NotNull(result.DiscoveredClue);
        Assert.Contains("BASEMENT MASTER", result.DiscoveredClue);

        // Now inspect succeeds
        ObjectActionResult inspectResult = controller.InspectObject("kitchen-pantry-safe");
        Assert.True(inspectResult.Succeeded);
    }

    [Fact]
    public void Tamper_WithWrongKey_FailsToUnlock()
    {
        BasementScenarioSession session = CreateSession();
        PlayerSessionController controller = session.PlayerController;
        controller.SetPlayerEntity(BasementScenario.George);

        // Move to kitchen
        _ = controller.RequestMove(new LocationId("kitchen"));
        foreach (var movement in session.PendingMovements.ToArray())
        {
            _ = session.CompleteMovement(movement.RequestId);
        }

        ObjectActionResult result = controller.TamperObject("kitchen-pantry-safe", "wrong-key");

        Assert.False(result.Succeeded);
        Assert.Contains("Failed to unlock", result.Message);
    }

    [Fact]
    public void Inspect_FromDifferentLocation_Fails()
    {
        BasementScenarioSession session = CreateSession();
        PlayerSessionController controller = session.PlayerController;
        controller.SetPlayerEntity(BasementScenario.George);

        // George is in Lobby, tries to inspect safe in Kitchen
        ObjectActionResult result = controller.InspectObject("kitchen-pantry-safe");

        Assert.False(result.Succeeded);
        Assert.Contains("Cannot inspect", result.Message);
    }

    [Fact]
    public void Tamper_SuspiciousObject_TriggersWitnessSuspicion()
    {
        BasementScenarioSession session = CreateSession();

        // George and Anna are both in Lobby
        // George tampers with the guest registry (which is suspicious)
        ObjectActionResult result = session.TamperObject("lobby-guest-registry");
        Assert.True(result.Succeeded);

        // Anna witnesses George tampering with registry
        SuspicionSnapshot annaSuspicion = session.GetSuspicion(BasementScenario.Anna, BasementScenario.George);
        Assert.NotEmpty(annaSuspicion.Evidence);
        Assert.True(annaSuspicion.Vector.RoleDeviation > 0 || annaSuspicion.Vector.Criminality > 0);
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
