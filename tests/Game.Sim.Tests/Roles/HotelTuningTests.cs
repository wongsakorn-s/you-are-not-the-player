using Game.Sim.Anomalies;
using Game.Sim.Behaviors;
using Game.Sim.Cases;
using Game.Sim.Entities;
using Game.Sim.Events;
using Game.Sim.Needs;
using Game.Sim.Objects;
using Game.Sim.Patterns;
using Game.Sim.Roles;
using Game.Sim.Routines;
using Game.Sim.Scenarios;
using Game.Sim.Schedules;
using Game.Sim.Suspicion;
using Game.Sim.Time;

namespace Game.Sim.Tests.Roles;

/// <summary>
/// The numbers that decide whether a system does anything. Each of these was
/// sized for a world that no longer exists - a quarter-second tick, a bigger
/// building, or a cast that never suspected anybody.
/// </summary>
public sealed class HotelTuningTests
{
    private static readonly EntityId[] Roster =
    [
        BasementScenario.Anna, BasementScenario.Bob, BasementScenario.George,
        BasementScenario.Charlie, BasementScenario.Dana, BasementScenario.Evelyn,
    ];

    private static readonly RoleId[] AllRoles =
    [
        HotelNightRoutines.Receptionist, HotelNightRoutines.Cleaner,
        HotelNightRoutines.Security, HotelNightRoutines.Cook,
        HotelNightRoutines.Manager, HotelNightRoutines.Guest,
    ];

    [Fact]
    public void ALootSweepIsReachableInAHotelThisSize()
    {
        // The default asked for ten distinct containers, and the hotel holds
        // eleven - so the pattern the design names as its central example could
        // only fire if the player searched all but one thing in the building.
        int objects = HotelObjectRegistry.CreateDefaultHotelObjects().AllObjects.Count;
        BehaviorPatternPolicy policy = HotelNightRoutines.PatternPolicy();

        Assert.True(
            policy.LootSweepDistinctInteractions * 2 <= objects,
            $"A sweep needs {policy.LootSweepDistinctInteractions} of {objects} objects, " +
            "which is most of the hotel.");
    }

    [Fact]
    public void EveryPatternWindowFitsInsideASingleNight()
    {
        // One tick is one minute now. A role-neglect window of 3600 was sixty
        // hours: ten times the shift it was meant to describe.
        BehaviorPatternPolicy policy = HotelNightRoutines.PatternPolicy();

        Assert.InRange(policy.LootSweepWindowSeconds, 1, 360);
        Assert.InRange(policy.RepeatInteractionWindowSeconds, 1, 360);
        Assert.InRange(policy.RoleNeglectWindowSeconds, 1, 360);
        Assert.InRange(policy.BoundaryTestingWindowSeconds, 1, 360);
    }

    [Fact]
    public void HungerArrivesDuringTheShiftAndFatigueArrivesNearItsEnd()
    {
        NeedProfile profile = HotelNeeds.Profile();
        var needs = new NeedState();

        int hungryAt = 0;
        int tiredAt = 0;
        for (int tick = 1; tick <= 360; tick++)
        {
            needs.Advance(SimDelta.OneTick, ticksPerSecond: 1, profile.GrowthRates);
            if (hungryAt == 0 && needs.GetUrgency(NeedType.Hunger) >= 0.65f)
            {
                hungryAt = tick;
            }

            if (tiredAt == 0 && needs.GetUrgency(NeedType.Fatigue) >= 0.75f)
            {
                tiredAt = tick;
            }
        }

        Assert.InRange(hungryAt, 120, 260);
        Assert.InRange(tiredAt, 260, 360);
        Assert.True(tiredAt > hungryAt, "Fatigue should arrive after hunger, not before.");
    }

    [Fact]
    public void NobodyGetsLonelyEnoughToAbandonTheirNight()
    {
        // Social is deliberately out of reach in one shift; a cast wandering off
        // to chat would drown the signal the player is reading.
        NeedProfile profile = HotelNeeds.Profile();
        var needs = new NeedState();
        for (int tick = 0; tick < 360; tick++)
        {
            needs.Advance(SimDelta.OneTick, ticksPerSecond: 1, profile.GrowthRates);
        }

        Assert.True(needs.GetUrgency(NeedType.Social) < 0.80f);
    }

    [Fact]
    public void EveryNeedSendsPeopleSomewhereTheyAreAllowedToGo()
    {
        // Same guardrail that caught two staging bugs: a goal pointing at a
        // forbidden room is a goal that quietly never happens.
        foreach (RoleId role in AllRoles)
        {
            NeedDestinations destinations = HotelNeeds.Destinations(role);
            RolePermissions permissions = HotelNightRoutines.Permissions(role);
            Assert.True(
                permissions.CanEnter(destinations.MealLocation),
                $"{role} eats at {destinations.MealLocation}, which {role} cannot enter.");
            Assert.True(
                permissions.CanEnter(destinations.RestLocation),
                $"{role} rests at {destinations.RestLocation}, which {role} cannot enter.");
            Assert.True(
                permissions.CanEnter(destinations.SocialLocation),
                $"{role} socialises at {destinations.SocialLocation}, which {role} cannot enter.");
        }
    }

    [Fact]
    public void EachKindOfAnomalyMatchesExactlyOneSuspicionRule()
    {
        // The two anomaly rules are alternatives, but one shared tag set made both
        // fire on every anomaly, so a single sighting scored roughly double what
        // either rule was written to award.
        BasementScenarioSession session = CreateSession();
        for (int tick = 0; tick < 360 && !session.IsComplete; tick++)
        {
            _ = session.AdvanceOneTick();
        }

        WorldEvent[] anomalies = session.Events
            .Where(worldEvent => worldEvent.Type == EventType.RealityAnomaly)
            .ToArray();
        Assert.NotEmpty(anomalies);
        Assert.All(anomalies, anomaly => Assert.False(
            anomaly.Tags.Contains(EventTag.Pattern) && anomaly.Tags.Contains(EventTag.Suspicious),
            $"An anomaly carried both rule tags: {string.Join(",", anomaly.Tags)}."));
    }

    [Fact]
    public void SuspicionWeighsOnADecisionWithoutReplacingIt()
    {
        // The concern score is unbounded and feeds straight into utility. Once
        // anomalies started scoring in the hundreds, following the person you
        // suspect outranked every shift, need and secret in the building.
        SuspicionBehaviorPolicy hotel = HotelNightRoutines.BehaviorPolicy();

        // The cap is added to a goal's base utility rather than compared against
        // other goals directly, so what matters is that it is bounded at all and
        // still large enough to move a decision.
        Assert.True(float.IsFinite(hotel.MaxBeliefWeight));
        Assert.InRange(hotel.MaxBeliefWeight, 20.0f, 100.0f);

        // The default stays unbounded, so the hotel's cap reads as a deliberate
        // override rather than something every caller silently inherits.
        Assert.Equal(float.MaxValue, new SuspicionBehaviorPolicy().MaxBeliefWeight);
    }

    [Fact]
    public void ACastWithNeedsStillSpendsMostOfTheNightWorking()
    {
        BasementScenarioSession session = CreateSession();
        for (int tick = 0; tick < 360 && !session.IsComplete; tick++)
        {
            _ = session.AdvanceOneTick();
        }

        int working = session.Decisions.Count(decision =>
            decision.Goal.Type == Sim.Brain.GoalType.Work);
        Assert.True(
            working > session.Decisions.Count / 4,
            $"Only {working} of {session.Decisions.Count} decisions were the job.");
    }

    private static BasementScenarioSession CreateSession()
    {
        InMemorySuspicionRuleRepository rules = JsonSuspicionRuleParser.Parse(
            File.ReadAllText(Path.Combine(
                AppContext.BaseDirectory,
                "Data",
                "SuspicionRules",
                "mvp.json")));
        SessionTruth truth = CaseGenerator.Generate(
            481_516UL,
            new CaseGenerationOptions(
                BasementScenario.George,
                Roster,
                shiftTicks: 360,
                pinnedIncidentCulprit: BasementScenario.George));
        return new BasementScenario(rules).CreateSession(
            new BasementScenarioOptions(481_516UL, 360, truth),
            autoCompleteMovements: true);
    }
}
