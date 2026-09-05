using Game.Sim.Cases;
using Game.Sim.Entities;
using Game.Sim.Events;
using Game.Sim.PlayerAi;
using Game.Sim.Scenarios;
using Game.Sim.Suspicion;

namespace Game.Sim.Tests.Cases;

/// <summary>
/// End-to-end cover for the seed-variation gap: the same seed has to replay
/// identically, and a different seed has to steer a different character.
/// </summary>
public sealed class SeedDrivenSessionTests
{
    private const int Ticks = 48;

    private static readonly EntityId[] Roster =
    [
        BasementScenario.Anna,
        BasementScenario.Bob,
        BasementScenario.George,
        BasementScenario.Charlie,
        BasementScenario.Dana,
        BasementScenario.Evelyn,
    ];

    [Fact]
    public void SessionWithoutTruthKeepsTheScriptedArrangement()
    {
        // The four regression scenarios rely on this: no truth, no behaviour change.
        BasementScenarioSession session = CreateSession(truth: null);
        RunTo(session, Ticks);

        Assert.Contains(
            session.Decisions,
            decision => decision.Entity == BasementScenario.George);
    }

    [Fact]
    public void SameSeedProducesTheSameEventStream()
    {
        string[] first = RunAndDescribe(seed: 4242UL);
        string[] second = RunAndDescribe(seed: 4242UL);

        Assert.Equal(first, second);
    }

    [Fact]
    public void DifferentSeedsSteerDifferentCharacters()
    {
        var steered = new HashSet<EntityId>();
        for (ulong seed = 0; seed < 12; seed++)
        {
            SessionTruth truth = CaseGenerator.Generate(seed, CreateOptions());
            BasementScenarioSession session = CreateSession(truth);
            RunTo(session, Ticks);

            // The Player AI is the only routine driving the hidden player, so any
            // decision recorded for them came from the archetype the seed picked.
            if (session.Decisions.Any(decision => decision.Entity == truth.HiddenPlayer))
            {
                _ = steered.Add(truth.HiddenPlayer);
            }
        }

        Assert.True(
            steered.Count >= 3,
            $"Twelve seeds should steer at least three characters, saw {steered.Count}.");
    }

    [Fact]
    public void TheHiddenPlayerIsNeverDrivenByBothRoutineSystems()
    {
        for (ulong seed = 0; seed < 12; seed++)
        {
            SessionTruth truth = CaseGenerator.Generate(seed, CreateOptions());
            BasementScenarioSession session = CreateSession(truth);
            RunTo(session, Ticks);

            // A double-driven actor would show cancelled movement requests piling
            // up; the executor only ever tracks one live request per character.
            Assert.All(
                session.PendingMovements,
                movement => Assert.NotEqual(MovementStatusCancelled, movement.Status.ToString()));
        }
    }

    [Fact]
    public void HiddenTruthNeverReachesTheEventStream()
    {
        SessionTruth truth = CaseGenerator.Generate(777UL, CreateOptions());
        BasementScenarioSession session = CreateSession(truth);
        RunTo(session, Ticks);

        // Nothing in the world may announce who the Player is; NPCs have to infer
        // it from behaviour alone.
        Assert.All(session.Events, worldEvent => Assert.DoesNotContain(
            worldEvent.Tags,
            tag => tag.ToString().Contains("Player", StringComparison.OrdinalIgnoreCase)));
        // Memories record what was perceived, never who the observer's target
        // really is; every tag on them has to come from the world event itself.
        Assert.All(
            session.GetMemories(BasementScenario.Anna),
            memory => Assert.DoesNotContain(
                memory.Tags,
                tag => tag.ToString().Contains("Player", StringComparison.OrdinalIgnoreCase)));
    }

    private const string MovementStatusCancelled = "Cancelled";

    private static CaseGenerationOptions CreateOptions() =>
        new(BasementScenario.George, Roster, Ticks);

    private static string[] RunAndDescribe(ulong seed)
    {
        SessionTruth truth = CaseGenerator.Generate(seed, CreateOptions());
        BasementScenarioSession session = CreateSession(truth);
        RunTo(session, Ticks);
        return session.Events
            .Select(worldEvent =>
                $"{worldEvent.Time.Tick}:{worldEvent.Type}:{worldEvent.Actor}:{worldEvent.Location}")
            .ToArray();
    }

    private static BasementScenarioSession CreateSession(SessionTruth? truth) =>
        new BasementScenario(LoadRules())
            .CreateSession(
                new BasementScenarioOptions(481516UL, Ticks, truth),
                autoCompleteMovements: true);

    private static InMemorySuspicionRuleRepository LoadRules() =>
        JsonSuspicionRuleParser.Parse(File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "Data",
            "SuspicionRules",
            "mvp.json")));

    private static void RunTo(BasementScenarioSession session, int ticks)
    {
        // Bounded on purpose: a generated truth can legitimately fail the scripted
        // completion milestone, and this must not turn into an endless loop.
        while (!session.IsComplete && session.Now.Tick < ticks)
        {
            _ = session.AdvanceOneTick();
        }
    }
}
