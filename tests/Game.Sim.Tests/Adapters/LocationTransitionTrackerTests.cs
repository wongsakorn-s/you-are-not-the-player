using Game.Client.Godot.Adapters;
using Game.Sim.Entities;
using Game.Sim.Locations;

namespace Game.Sim.Tests.Adapters;

public sealed class LocationTransitionTrackerTests
{
    private static readonly EntityId Anna = new("anna");
    private static readonly LocationId Lobby = new("lobby");
    private static readonly LocationId Basement = new("basement");

    [Fact]
    public void Request_DoesNotChangeConfirmedLocationUntilArrival()
    {
        var tracker = new LocationTransitionTracker();
        tracker.Initialize(Anna, Lobby);

        tracker.Request(Anna, Basement);

        Assert.Equal(Lobby, tracker.ConfirmedLocations[Anna]);
        Assert.Equal(Basement, tracker.GetRequestedLocation(Anna));
        Assert.True(tracker.IsInTransit(Anna));

        Assert.True(tracker.Confirm(Anna, Basement));
        Assert.Equal(Basement, tracker.ConfirmedLocations[Anna]);
        Assert.False(tracker.IsInTransit(Anna));
    }

    [Fact]
    public void Confirm_RejectsStaleArrivalAfterDestinationChanged()
    {
        var tracker = new LocationTransitionTracker();
        tracker.Initialize(Anna, Lobby);
        tracker.Request(Anna, Basement);
        tracker.Request(Anna, Lobby);

        bool confirmed = tracker.Confirm(Anna, Basement);

        Assert.False(confirmed);
        Assert.Equal(Lobby, tracker.ConfirmedLocations[Anna]);
    }

    [Fact]
    public void Request_RejectsActorThatWasNotInitialized()
    {
        var tracker = new LocationTransitionTracker();

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(
            () => tracker.Request(Anna, Basement));

        Assert.Contains("not been initialized", error.Message, StringComparison.Ordinal);
    }
}
