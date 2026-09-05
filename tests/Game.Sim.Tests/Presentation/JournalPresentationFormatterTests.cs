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
    public void Format_ShowsPlayerFacingTimeSourceReliabilityAndConcern()
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

        Assert.Contains("Now at Hotel Lobby", result, StringComparison.Ordinal);
        Assert.Contains("23:07", result, StringComparison.Ordinal);
        Assert.Contains("Heard from Bob", result, StringComparison.Ordinal);
        Assert.Contains("Likely reliable", result, StringComparison.Ordinal);
        Assert.Contains("Anna — Worth watching", result, StringComparison.Ordinal);
        Assert.DoesNotContain("T07", result, StringComparison.Ordinal);
        Assert.DoesNotContain("root event", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("85%", result, StringComparison.Ordinal);

        string filtered = JournalPresentationFormatter.Format(
            journal,
            id => id == anna ? "Anna" : id == bob ? "Bob" : id.Value,
            location => location.Value == "lobby" ? "Hotel Lobby" : location.Value,
            new TimelineFilter(Kind: MemoryKind.Episodic));

        Assert.Contains("Showing: what George saw", filtered, StringComparison.Ordinal);
        Assert.Contains("No clues match this view", filtered, StringComparison.Ordinal);

        string thai = JournalPresentationFormatter.Format(
            journal,
            id => id == anna ? "แอนนา" : id == bob ? "บ็อบ" : id.Value,
            location => location.Value == "lobby" ? "ล็อบบี้โรงแรม" : "ชั้นใต้ดิน",
            useThai: true);

        Assert.Contains("ตอนนี้อยู่ที่ ล็อบบี้โรงแรม", thai, StringComparison.Ordinal);
        Assert.Contains("ได้ยินจาก บ็อบ", thai, StringComparison.Ordinal);
        Assert.Contains("ค่อนข้างน่าเชื่อถือ", thai, StringComparison.Ordinal);
        Assert.Contains("แอนนา — ควรจับตา", thai, StringComparison.Ordinal);
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

        Assert.Contains("No clues yet", result, StringComparison.Ordinal);
        Assert.Contains("No clearly suspicious behavior", result, StringComparison.Ordinal);
    }

    [Fact]
    public void FormatPage_ShowsTwoCluesAtATimeAndKeepsPeopleSummarySeparate()
    {
        var anna = new EntityId("anna");
        var journal = new PlayerJournal(
            new EntityId("george"),
            new LocationId("lobby"),
            new SimTime(12),
            [
                CreateEntry(1, anna, 2),
                CreateEntry(2, anna, 5),
                CreateEntry(3, anna, 8),
                CreateEntry(4, anna, 11),
            ],
            [new SuspicionSnapshot(
                new EntityId("george"),
                anna,
                new SuspicionVector(0.0f, 0.0f, 6.0f, 0.0f, 0.0f, 0.0f),
                [])],
            [],
            []);

        JournalPage firstPage = JournalPresentationFormatter.FormatPage(
            journal,
            id => id == anna ? "Anna" : id.Value,
            _ => "Hotel Lobby",
            pageIndex: 0,
            pageSize: 2);
        JournalPage secondPage = JournalPresentationFormatter.FormatPage(
            journal,
            id => id == anna ? "Anna" : id.Value,
            _ => "Hotel Lobby",
            pageIndex: 1,
            pageSize: 2);
        string people = JournalPresentationFormatter.FormatPeopleToWatch(
            journal,
            id => id == anna ? "Anna" : id.Value);

        Assert.Equal(1, firstPage.PageNumber);
        Assert.Equal(2, firstPage.PageCount);
        Assert.Contains("Clues 1-2 of 4", firstPage.Text, StringComparison.Ordinal);
        Assert.Contains("23:08", firstPage.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("23:02", firstPage.Text, StringComparison.Ordinal);
        Assert.Equal(2, secondPage.PageNumber);
        Assert.Contains("Clues 3-4 of 4", secondPage.Text, StringComparison.Ordinal);
        Assert.Contains("23:02", secondPage.Text, StringComparison.Ordinal);
        Assert.Contains("Anna — Something feels off", people, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0, "23:00")]
    [InlineData(60, "00:00")]
    [InlineData(360, "05:00")]
    public void FormatClock_UsesInGameClockInsteadOfTechnicalTick(long tick, string expected)
    {
        Assert.Equal(expected, JournalPresentationFormatter.FormatClock(tick));
    }

    private static PlayerJournalEntry CreateEntry(long id, EntityId subject, long tick) =>
        new(
            new MemoryId(id),
            MemoryKind.Episodic,
            subject,
            EventType.EnterLocation,
            new LocationId("lobby"),
            new SimTime(tick),
            0.95f,
            null,
            new EventId(id),
            string.Empty);
}
