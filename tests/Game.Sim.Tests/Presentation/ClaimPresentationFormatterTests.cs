using Game.Client.Godot.Presentation;
using Game.Sim.Entities;
using Game.Sim.Events;
using Game.Sim.Locations;
using Game.Sim.Memory;
using Game.Sim.Player;
using Game.Sim.Time;

namespace Game.Sim.Tests.Presentation;

public sealed class ClaimPresentationFormatterTests
{
    private static readonly EntityId Anna = new("anna");
    private static readonly LocationId Lobby = new("lobby");
    private static readonly LocationId Basement = new("basement");

    [Fact]
    public void AnEmptyLedgerExplainsHowToFillIt()
    {
        string page = Format([], []);

        Assert.Contains("Ask people about their shift", page, StringComparison.Ordinal);
    }

    [Fact]
    public void AClaimWithNothingAgainstItSaysSo()
    {
        AlibiClaim claim = Claim(Lobby, tick: 100);

        string page = Format([claim], []);

        Assert.Contains("said they were at Lobby", page, StringComparison.Ordinal);
        Assert.Contains("Nothing you have contradicts this", page, StringComparison.Ordinal);
    }

    [Fact]
    public void AContradictedClaimNamesTheConflictAndHowGoodTheClueIs()
    {
        AlibiClaim claim = Claim(Lobby, tick: 100);
        var contradiction = new Contradiction(
            claim,
            Memory(Basement, tick: 100, MemoryKind.Episodic),
            EvidenceIsFirstHand: true);

        string page = Format([claim], [contradiction]);

        Assert.Contains("Conflicts with", page, StringComparison.Ordinal);
        Assert.Contains("You saw this yourself", page, StringComparison.Ordinal);
    }

    [Fact]
    public void SecondHandEvidenceIsMarkedAsRisky()
    {
        AlibiClaim claim = Claim(Lobby, tick: 100);
        var contradiction = new Contradiction(
            claim,
            Memory(Basement, tick: 100, MemoryKind.Social),
            EvidenceIsFirstHand: false);

        string page = Format([claim], [contradiction]);

        Assert.Contains("second-hand", page, StringComparison.Ordinal);
    }

    [Fact]
    public void ThePageNeverStatesWhetherAClaimIsTrue()
    {
        // The player has to decide that from the evidence. Saying it outright
        // would hand them the deduction the game is asking them to make.
        AlibiClaim claim = Claim(Lobby, tick: 100);
        string page = Format(
            [claim],
            [new Contradiction(claim, Memory(Basement, 100, MemoryKind.Episodic), true)]);

        foreach (string verdict in new[] { "lying", "lie", "true", "false" })
        {
            Assert.DoesNotContain(verdict, page, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void ThaiPagesCarryNoEnglish()
    {
        AlibiClaim claim = Claim(Lobby, tick: 100);
        string page = ClaimPresentationFormatter.FormatClaims(
            [claim],
            [new Contradiction(claim, Memory(Basement, 100, MemoryKind.Social), false)],
            _ => "แอนนา",
            location => location == Lobby ? "ล็อบบี้" : "ชั้นใต้ดิน",
            useThai: true);

        Assert.DoesNotContain(page, character => character is >= 'a' and <= 'z');
        Assert.Contains("แอนนา", page, StringComparison.Ordinal);
    }

    private static string Format(
        IReadOnlyList<AlibiClaim> claims,
        IReadOnlyList<Contradiction> contradictions) =>
        ClaimPresentationFormatter.FormatClaims(
            claims,
            contradictions,
            entity => entity.Value == "anna" ? "Anna" : entity.Value,
            location => location == Lobby ? "Lobby" : "the Basement",
            useThai: false);

    private static AlibiClaim Claim(LocationId location, long tick) =>
        new(1, Anna, location, new SimTime(tick), new SimTime(tick + 5));

    private static MemoryRecord Memory(LocationId location, long tick, MemoryKind kind) =>
        MemoryRecord.Restore(
            id: new MemoryId(tick),
            kind: kind,
            subject: Anna,
            eventType: EventType.EnterLocation,
            location: location,
            tags: [],
            eventTime: new SimTime(tick),
            createdAt: new SimTime(tick),
            initialConfidence: 0.9f,
            salience: 0.5f,
            informationSource: null,
            rootEventId: new EventId(tick),
            sourceObservationId: null,
            sourceMemoryId: null,
            behaviorPattern: null);
}
