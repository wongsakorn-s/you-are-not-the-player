using Game.Sim.Cases;
using Game.Sim.Entities;
using Game.Sim.Events;
using Game.Sim.PlayerAi;
using Game.Sim.Scenarios;
using Game.Sim.Suspicion;

namespace Game.Sim.Tests.Cases;

/// <summary>
/// Whether the character being steered is deducible from what they do, rather
/// than only from the reality anomalies that happen around them.
/// </summary>
/// <remarks>
/// Fifteen played nights said no. On the seven where no anomaly was witnessed,
/// the case file held nothing at all on the hidden player and ranked at most one
/// person in the whole hotel, because the Player AI had one objective for the
/// night - or, as a Roleplayer, none ever - and blended in for the remaining
/// five hours.
/// </remarks>
public sealed class HiddenPlayerLeavesATrailTests
{
    private static readonly EntityId[] Roster =
    [
        BasementScenario.Anna, BasementScenario.Bob, BasementScenario.George,
        BasementScenario.Charlie, BasementScenario.Dana, BasementScenario.Evelyn,
    ];

    [Theory]
    [InlineData(PlayerAiArchetype.Explorer)]
    [InlineData(PlayerAiArchetype.Completionist)]
    [InlineData(PlayerAiArchetype.Roleplayer)]
    public void EveryArchetypeActsAllNightRatherThanOnce(PlayerAiArchetype archetype)
    {
        BasementScenarioSession session = RunNight(archetype, out SessionTruth truth);

        WorldEvent[] theirs =
        [
            .. session.Events.Where(worldEvent =>
                worldEvent.Actor == truth.HiddenPlayer &&
                worldEvent.Type is EventType.Interaction or EventType.BoundaryProbe),
        ];

        Assert.NotEmpty(theirs);

        // Spread across the shift, not emptied in the first few minutes. A plan
        // that finishes at 23:20 is a character who behaves for the rest of the
        // night, which is what made the answer undeducible.
        long last = theirs.Max(worldEvent => worldEvent.Time.Tick);
        Assert.True(last > 300, $"The last thing they did was at tick {last} of 360.");
    }

    [Theory]
    [InlineData(PlayerAiArchetype.Explorer)]
    [InlineData(PlayerAiArchetype.Completionist)]
    [InlineData(PlayerAiArchetype.Roleplayer)]
    public void WhatTheyDoIsThePatternTheirArchetypeIsNamedFor(PlayerAiArchetype archetype)
    {
        BasementScenarioSession session = RunNight(archetype, out SessionTruth truth);

        // The suspicion rules for these patterns were written long before
        // anything in the game produced one.
        Assert.Contains(
            session.Events,
            worldEvent => worldEvent.Type == EventType.BehaviorPattern &&
                worldEvent.Actor == truth.HiddenPlayer);
    }

    [Fact]
    public void EveryNightGivesThePlayerSomebodyElseToLookAt()
    {
        // Measured over seven nights, chance alone left three of them with the
        // hidden player as the only person in the hotel carrying any suspicion,
        // which makes the top of the case file correct by construction rather
        // than by deduction.
        for (ulong seed = 0; seed < 60; seed++)
        {
            SessionTruth truth = CaseGenerator.Generate(
                seed,
                new CaseGenerationOptions(
                    BasementScenario.George,
                    Roster,
                    shiftTicks: 360,
                    pinnedIncidentCulprit: BasementScenario.George));

            Assert.True(
                truth.Secrets.Count >= 2,
                $"Seed {seed} staged {truth.Secrets.Count} secrets.");
            Assert.DoesNotContain(truth.Secrets, secret => secret.Owner == truth.HiddenPlayer);
            Assert.Equal(
                truth.Secrets.Count,
                truth.Secrets.Select(secret => secret.Owner).Distinct().Count());
        }
    }

    private static BasementScenarioSession RunNight(
        PlayerAiArchetype archetype,
        out SessionTruth truth)
    {
        InMemorySuspicionRuleRepository rules = JsonSuspicionRuleParser.Parse(
            File.ReadAllText(Path.Combine(
                AppContext.BaseDirectory,
                "Data",
                "SuspicionRules",
                "mvp.json")));
        truth = CaseGenerator.Generate(
            481_516UL,
            new CaseGenerationOptions(
                BasementScenario.George,
                Roster,
                shiftTicks: 360,
                pinnedArchetype: archetype,
                pinnedIncidentCulprit: BasementScenario.George));
        BasementScenarioSession session = new BasementScenario(rules).CreateSession(
            new BasementScenarioOptions(481_516UL, 360, truth),
            autoCompleteMovements: true);
        for (int tick = 0; tick < 360 && !session.IsComplete; tick++)
        {
            _ = session.AdvanceOneTick();
        }

        return session;
    }
}
