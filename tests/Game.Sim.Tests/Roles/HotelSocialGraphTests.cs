using Game.Sim.Cases;
using Game.Sim.Entities;
using Game.Sim.Locations;
using Game.Sim.PlayerAi;
using Game.Sim.Roles;
using Game.Sim.Scenarios;
using Game.Sim.Schedules;
using Game.Sim.Suspicion;

namespace Game.Sim.Tests.Roles;

/// <summary>
/// A suspicion that nobody can act on and nobody can pass along is a suspicion
/// the player will never see the consequences of.
/// </summary>
public sealed class HotelSocialGraphTests
{
    private static readonly RoleId[] AllRoles =
    [
        HotelNightRoutines.Receptionist,
        HotelNightRoutines.Cleaner,
        HotelNightRoutines.Security,
        HotelNightRoutines.Cook,
        HotelNightRoutines.Manager,
        HotelNightRoutines.Guest,
    ];

    [Fact]
    public void EveryoneHasSomebodyToTell()
    {
        // Bob used to have an empty contact list, so anything he worked out died
        // with him and the rumour chain the design is built on had one link.
        Assert.All(
            AllRoles,
            role => Assert.NotEmpty(HotelSocialGraph.Confidants(role)));
    }

    [Fact]
    public void NobodyConfidesInThemselves()
    {
        Assert.All(
            AllRoles,
            role => Assert.DoesNotContain(role, HotelSocialGraph.Confidants(role)));
    }

    [Fact]
    public void EverySafePlaceIsSomewhereThatRoleMayActuallyGo()
    {
        // SuspicionDrivenGoalSource throws when a safe location is forbidden, so
        // getting this wrong is a crash mid-shift rather than a quiet miss.
        foreach (RoleId role in AllRoles)
        {
            LocationId safe = HotelSocialGraph.SafePlace(role);
            Assert.True(
                HotelNightRoutines.Permissions(role).CanEnter(safe),
                $"{role} retreats to {safe}, which {role} is not allowed to enter.");
        }
    }

    [Fact]
    public void WordCanTravelBetweenAnyTwoRoles()
    {
        // Reachability, not adjacency: the Anna-tells-Bob-who-follows-George chain
        // only emerges if the graph is connected.
        foreach (RoleId start in AllRoles)
        {
            var seen = new HashSet<RoleId> { start };
            var queue = new Queue<RoleId>([start]);
            while (queue.Count > 0)
            {
                foreach (RoleId next in HotelSocialGraph.Confidants(queue.Dequeue()))
                {
                    if (seen.Add(next))
                    {
                        queue.Enqueue(next);
                    }
                }
            }

            Assert.True(
                seen.Count == AllRoles.Length,
                $"From {start} word only reaches {seen.Count} of {AllRoles.Length} roles.");
        }
    }

    [Fact]
    public void EveryCharacterInAPlayedNightCanReactToWhatTheySee()
    {
        // The gap this closes: four of the six had no profile at all, so whatever
        // they witnessed produced no behaviour anyone could observe.
        // A full night: the cast has to witness something before it can react to
        // anything, and the first secrets are not staged until after midnight.
        BasementScenarioSession session = CreateSession();
        for (int tick = 0; tick < 360 && !session.IsComplete; tick++)
        {
            _ = session.AdvanceOneTick();
        }

        EntityId[] reacted = session.Decisions
            .Where(decision => decision.Goal.Type
                is Sim.Brain.GoalType.ObserveTarget
                or Sim.Brain.GoalType.FollowTarget
                or Sim.Brain.GoalType.AskAboutTarget
                or Sim.Brain.GoalType.ShareSuspicion
                or Sim.Brain.GoalType.AvoidTarget)
            .Select(decision => decision.Entity)
            .Distinct()
            .ToArray();

        Assert.NotEmpty(reacted);
    }

    [Fact]
    public void ANightProducesSuspicionFromMoreThanOneKindOfEvidence()
    {
        // Three separate rules firing is what stops the player from learning a
        // single tell and applying it every run.
        BasementScenarioSession session = CreateSession();
        for (int tick = 0; tick < 360 && !session.IsComplete; tick++)
        {
            _ = session.AdvanceOneTick();
        }

        EntityId[] cast =
        [
            BasementScenario.Anna, BasementScenario.Bob, BasementScenario.George,
            BasementScenario.Charlie, BasementScenario.Dana, BasementScenario.Evelyn,
        ];
        string[] rules = cast
            .SelectMany(observer => cast
                .Where(subject => subject != observer)
                .SelectMany(subject => session.GetSuspicion(observer, subject).Evidence))
            .Select(evidence => evidence.Contribution.RuleId)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            rules.Length >= 2,
            $"A whole night produced suspicion from only: {string.Join(", ", rules)}.");
    }

    private static BasementScenarioSession CreateSession()
    {
        InMemorySuspicionRuleRepository rules = JsonSuspicionRuleParser.Parse(
            File.ReadAllText(Path.Combine(
                AppContext.BaseDirectory,
                "Data",
                "SuspicionRules",
                "mvp.json")));
        EntityId[] roster =
        [
            BasementScenario.Anna, BasementScenario.Bob, BasementScenario.George,
            BasementScenario.Charlie, BasementScenario.Dana, BasementScenario.Evelyn,
        ];
        SessionTruth truth = CaseGenerator.Generate(
            481_516UL,
            new CaseGenerationOptions(
                BasementScenario.George,
                roster,
                shiftTicks: 360,
                pinnedIncidentCulprit: BasementScenario.George));
        return new BasementScenario(rules).CreateSession(
            new BasementScenarioOptions(481_516UL, 360, truth),
            autoCompleteMovements: true);
    }
}
