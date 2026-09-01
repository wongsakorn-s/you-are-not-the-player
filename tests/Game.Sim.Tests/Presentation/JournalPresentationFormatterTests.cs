using Game.Client.Godot.Presentation;
using Game.Sim.Entities;
using Game.Sim.Events;
using Game.Sim.Locations;
using Game.Sim.Memory;
using Game.Sim.Player;
using Game.Sim.Suspicion;
using Game.Sim.Time;

namespace Game.Sim.Tests.Presentation;

public sealed class JournalPresentationFormatterTests
{
    [Fact]
    public void Format_ShowsReadableSourceConfidenceAndSuspicionEvidence()
    {
        var anna = new EntityId("anna");
        var bob = new EntityId("bob");
        var journal = new PlayerJournal(
            new EntityId("george"),
            new LocationId("lobby"),
            new SimTime(9),
            [new PlayerJournalEntry(
                new MemoryId(4),
                MemoryKind.Social,
                anna,
                EventType.EnterLocation,
                new LocationId("basement"),
                new SimTime(7),
                0.85f,
                bob,
                new EventId(3),
                "bob told you: saw anna at basement.")],
            [new SuspicionSnapshot(
                new EntityId("george"),
                anna,
                new SuspicionVector(2.0f, 3.0f, 4.0f, 0.0f, 1.0f, 0.0f),
                [])],
            [],
            []);

        string result = JournalPresentationFormatter.Format(
            journal,
            id => id == anna ? "Anna" : id == bob ? "Bob" : id.Value,
            location => location.Value == "lobby" ? "Hotel Lobby" : location.Value);

        Assert.Contains("LOCATION: Hotel Lobby", result, StringComparison.Ordinal);
        Assert.Contains("confidence: 85%", result, StringComparison.Ordinal);
        Assert.Contains("source: Bob", result, StringComparison.Ordinal);
        Assert.Contains("Anna — score 10.0", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Format_ExplainsWhenJournalHasNoEvidence()
    {
        var journal = new PlayerJournal(
            new EntityId("george"),
            new LocationId("lobby"),
            SimTime.Zero,
            [],
            [],
            [],
            []);

        string result = JournalPresentationFormatter.Format(
            journal,
            id => id.Value,
            location => location.Value);

        Assert.Contains("No reliable observations", result, StringComparison.Ordinal);
        Assert.Contains("No suspicion supported", result, StringComparison.Ordinal);
    }
}
