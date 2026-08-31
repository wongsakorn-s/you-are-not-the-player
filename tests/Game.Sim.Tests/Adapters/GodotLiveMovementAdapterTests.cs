using Game.Client.Godot.Adapters;
using Game.Client.Godot.Configuration;
using Game.Sim.Actions;
using Game.Sim.Events;
using Game.Sim.Scenarios;
using Game.Sim.Suspicion;

namespace Game.Sim.Tests.Adapters;

public sealed class GodotLiveMovementAdapterTests
{
    [Fact]
    public void CompleteMove_EmitsCoreTransitionOnlyAfterPhysicalAcknowledgement()
    {
        BasementScenarioResult result = CreateScenarioResult();
        HotelWorldDefinition hotel = LoadHotel();
        long firstEventId = result.Events.Max(worldEvent => worldEvent.Id.Value) + 1;
        var adapter = new GodotLiveMovementAdapter(
            result,
            hotel,
            new SequentialEventIdGenerator(firstEventId));

        MovementSnapshot denied = adapter.RequestMove(
            BasementScenario.George,
            BasementScenario.Basement,
            tick: 2);

        Assert.Equal(MovementStatus.Failed, denied.Status);
        Assert.Equal(MovementFailureReason.AccessDenied, denied.FailureReason);
        Assert.Equal(
            BasementScenario.Lobby,
            adapter.GetLogicalLocation(BasementScenario.George));

        adapter.SetPortalAccess("basement-door", isAccessible: true);
        MovementSnapshot navigating = adapter.RequestMove(
            BasementScenario.George,
            BasementScenario.Basement,
            tick: 3);

        Assert.Equal(MovementStatus.Navigating, navigating.Status);
        Assert.Equal(
            BasementScenario.Lobby,
            adapter.GetLogicalLocation(BasementScenario.George));

        IReadOnlyList<WorldEvent> events = adapter.CompleteMove(
            BasementScenario.George,
            BasementScenario.Basement,
            tick: 4);

        Assert.Equal(
            BasementScenario.Basement,
            adapter.GetLogicalLocation(BasementScenario.George));
        Assert.Equal([EventType.LeaveLocation, EventType.EnterLocation], events.Select(evt => evt.Type));
        Assert.Equal([new EventId(firstEventId), new EventId(firstEventId + 1)], events.Select(evt => evt.Id));
        Assert.All(events, worldEvent => Assert.Equal(4, worldEvent.Time.Tick));
    }

    private static BasementScenarioResult CreateScenarioResult()
    {
        string rulesPath = Path.Combine(
            AppContext.BaseDirectory,
            "Data",
            "SuspicionRules",
            "mvp.json");
        InMemorySuspicionRuleRepository rules = JsonSuspicionRuleParser.Parse(
            File.ReadAllText(rulesPath));
        return new BasementScenario(rules).Run(new BasementScenarioOptions(481_516, 16));
    }

    private static HotelWorldDefinition LoadHotel()
    {
        string hotelPath = Path.Combine(
            AppContext.BaseDirectory,
            "Data",
            "Hotel",
            "hotel-world.json");
        return HotelWorldDefinitionParser.Parse(File.ReadAllText(hotelPath));
    }
}
