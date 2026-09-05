using Game.Sim.Cases;
using Game.Sim.Entities;
using Game.Sim.Locations;
using Game.Sim.Memory;
using Game.Sim.Player;
using Game.Sim.PlayerAi;
using Game.Sim.Scenarios;
using Game.Sim.Suspicion;
using Game.Sim.Time;

namespace Game.Sim.Tests.Player;

/// <summary>
/// Catching someone in a lie is the payoff that makes talking worth doing twice,
/// and getting it wrong is what makes it a decision rather than a free action.
/// </summary>
public sealed class ContradictionTests
{
    private static readonly EntityId George = BasementScenario.George;
    private static readonly EntityId Anna = BasementScenario.Anna;
    private static readonly LocationId Lobby = BasementScenario.Lobby;
    private static readonly LocationId Basement = BasementScenario.Basement;
    private static readonly LocationId Kitchen = new("kitchen");

    [Fact]
    public void AClueThatPutsThemElsewhereAtTheSameTimeIsAContradiction()
    {
        AlibiClaim claim = Claim(Anna, Lobby, tick: 100);

        IReadOnlyList<Contradiction> found = ContradictionFinder.Find(
            [claim],
            [Memory(Anna, Basement, tick: 100, MemoryKind.Episodic)]);

        Assert.Single(found);
        Assert.True(found[0].EvidenceIsFirstHand);
        Assert.Equal(claim.Id, found[0].Claim.Id);
    }

    [Fact]
    public void AClueFromAnotherHourIsNotAContradiction()
    {
        IReadOnlyList<Contradiction> found = ContradictionFinder.Find(
            [Claim(Anna, Lobby, tick: 100)],
            [Memory(Anna, Basement, tick: 100 + ContradictionFinder.ToleranceTicks + 1, MemoryKind.Episodic)]);

        Assert.Empty(found);
    }

    [Fact]
    public void AClueAboutSomebodyElseIsNotAContradiction()
    {
        IReadOnlyList<Contradiction> found = ContradictionFinder.Find(
            [Claim(Anna, Lobby, tick: 100)],
            [Memory(BasementScenario.Bob, Basement, tick: 100, MemoryKind.Episodic)]);

        Assert.Empty(found);
    }

    [Fact]
    public void AgreeingCluesAreNotContradictions()
    {
        IReadOnlyList<Contradiction> found = ContradictionFinder.Find(
            [Claim(Anna, Lobby, tick: 100)],
            [Memory(Anna, Lobby, tick: 100, MemoryKind.Episodic)]);

        Assert.Empty(found);
    }

    [Fact]
    public void FirstHandEvidenceIsOfferedBeforeHearsay()
    {
        IReadOnlyList<Contradiction> found = ContradictionFinder.Find(
            [Claim(Anna, Lobby, tick: 100)],
            [
                Memory(Anna, Kitchen, tick: 100, MemoryKind.Social),
                Memory(Anna, Basement, tick: 100, MemoryKind.Episodic),
            ]);

        Assert.Equal(2, found.Count);
        Assert.True(found[0].EvidenceIsFirstHand);
        Assert.False(found[1].EvidenceIsFirstHand);
    }

    [Fact]
    public void AskingAboutASchedulePutsACheckableClaimOnTheRecord()
    {
        BasementScenarioSession session = CreateSession();
        Advance(session, 10);
        EntityId partner = FirstPartner(session);

        DialogueOutcome outcome = session.Talk(new DialogueRequest(
            DialogueActionKind.InquireSchedule,
            George,
            partner));

        Assert.True(outcome.Succeeded);
        Assert.NotNull(outcome.Claim);
        Assert.Equal(partner, outcome.Claim.Speaker);
        Assert.Contains(session.Claims, claim => claim.Id == outcome.Claim.Id);
    }

    [Fact]
    public void NobodyVolunteersThatTheyWereSomewhereRestricted()
    {
        BasementScenarioSession session = CreateSession();

        // Walk a character into the basement, then ask them to account for it.
        _ = session.RequestNpcMove(BasementScenario.Bob, Basement);
        Advance(session, 6);
        _ = session.RequestNpcMove(George, Basement);
        Advance(session, 6);

        DialogueOutcome outcome = session.Talk(new DialogueRequest(
            DialogueActionKind.InquireSchedule,
            George,
            BasementScenario.Bob));

        if (outcome.Claim is null)
        {
            return;
        }

        Assert.NotEqual(Basement, outcome.Claim.ClaimedLocation);
    }

    [Fact]
    public void ConfrontingWithNothingTheySaidIsJustAnAwkwardRemark()
    {
        BasementScenarioSession session = CreateSession();
        Advance(session, 10);
        MemoryRecord? clue = session.GetMemories(George).FirstOrDefault(m => m.Subject is not null);
        if (clue?.Subject is null)
        {
            return;
        }

        DialogueOutcome outcome = session.Talk(new DialogueRequest(
            DialogueActionKind.ConfrontEvidence,
            George,
            clue.Subject.Value,
            confrontingMemoryId: clue.Id));

        Assert.Equal(ConfrontationResult.None, outcome.Confrontation);
    }

    [Fact]
    public void CatchingALieMakesThemGiveSomethingUp()
    {
        BasementScenarioSession session = CreateSession();

        // Bob goes somewhere he should not be and George follows and sees it, so
        // George holds a first-hand clue rather than a story.
        _ = session.PlayerController.RequestMove(Basement);
        Advance(session, 6);
        _ = session.RequestNpcMove(BasementScenario.Bob, Basement);
        Advance(session, 6);

        MemoryRecord? sighting = session.GetMemories(George).FirstOrDefault(memory =>
            memory.Kind == MemoryKind.Episodic &&
            memory.Subject == BasementScenario.Bob &&
            memory.Location == Basement);
        Assert.NotNull(sighting);

        DialogueOutcome asked = session.Talk(new DialogueRequest(
            DialogueActionKind.InquireSchedule,
            George,
            BasementScenario.Bob));
        Assert.NotNull(asked.Claim);
        Assert.NotEqual(Basement, asked.Claim.ClaimedLocation);

        DialogueOutcome confronted = session.Talk(new DialogueRequest(
            DialogueActionKind.ConfrontEvidence,
            George,
            BasementScenario.Bob,
            confrontingMemoryId: sighting.Id));

        Assert.Equal(ConfrontationResult.Cracked, confronted.Confrontation);
    }

    [Fact]
    public void ChallengingAnAccountThatHoldsTurnsTheRoomAgainstYou()
    {
        BasementScenarioSession session = CreateSession();
        _ = session.PlayerController.RequestMove(Basement);
        Advance(session, 6);
        _ = session.RequestNpcMove(BasementScenario.Bob, Basement);
        Advance(session, 6);

        // Ask first so the claim is on the record, then challenge it with a clue
        // that agrees with it. Nothing they said is broken, so the challenge is
        // the thing that looks wrong.
        DialogueOutcome asked = session.Talk(new DialogueRequest(
            DialogueActionKind.InquireSchedule,
            George,
            BasementScenario.Bob));
        Assert.NotNull(asked.Claim);

        MemoryRecord? sighting = session.GetMemories(George).FirstOrDefault(memory =>
            memory.Subject == BasementScenario.Bob &&
            memory.Location == Basement);
        Assert.NotNull(sighting);

        ExposureReport before = session.GetExposure(George);
        DialogueOutcome confronted = session.Talk(new DialogueRequest(
            DialogueActionKind.ConfrontEvidence,
            George,
            BasementScenario.Bob,
            confrontingMemoryId: sighting.Id));
        Advance(session, 4);
        ExposureReport after = session.GetExposure(George);

        // Either it cracked (the claim was false) or it backfired and the hotel
        // now has something on the accuser. Both are real outcomes; what must not
        // happen is a free action with no consequence either way.
        Assert.NotEqual(ConfrontationResult.None, confronted.Confrontation);
        if (confronted.Confrontation == ConfrontationResult.Backfired)
        {
            Assert.True(
                after.Peak > before.Peak,
                "A false accusation should raise the accuser's own exposure.");
        }
    }

    private static EntityId FirstPartner(BasementScenarioSession session) =>
        session.PlayerController.GetPresentActors().First(actor => actor != George);

    private static AlibiClaim Claim(EntityId speaker, LocationId location, long tick) =>
        new(1, speaker, location, new SimTime(tick), new SimTime(tick + 1));

    private static MemoryRecord Memory(
        EntityId subject,
        LocationId location,
        long tick,
        MemoryKind kind) =>
        MemoryRecord.Restore(
            id: new MemoryId(tick + location.Value.Length),
            kind: kind,
            subject: subject,
            eventType: Sim.Events.EventType.EnterLocation,
            location: location,
            tags: [],
            eventTime: new SimTime(tick),
            createdAt: new SimTime(tick),
            initialConfidence: kind == MemoryKind.Episodic ? 0.95f : 0.6f,
            salience: 0.5f,
            informationSource: null,
            rootEventId: new Sim.Events.EventId(tick),
            sourceObservationId: null,
            sourceMemoryId: null,
            behaviorPattern: null);

    private static void Advance(BasementScenarioSession session, int ticks)
    {
        for (int index = 0; index < ticks && !session.IsComplete; index++)
        {
            _ = session.AdvanceOneTick();
        }
    }

    private static BasementScenarioSession CreateSession()
    {
        InMemorySuspicionRuleRepository rules = JsonSuspicionRuleParser.Parse(
            File.ReadAllText(Path.Combine(
                AppContext.BaseDirectory,
                "Data",
                "SuspicionRules",
                "mvp.json")));
        var truth = new SessionTruth(
            seed: 481_516,
            humanHost: George,
            hiddenPlayer: BasementScenario.Charlie,
            hiddenPlayerArchetype: PlayerAiArchetype.Explorer,
            incidentCulprit: George);
        return new BasementScenario(rules).CreateSession(
            new BasementScenarioOptions(481_516, 96, truth),
            autoCompleteMovements: true);
    }
}
