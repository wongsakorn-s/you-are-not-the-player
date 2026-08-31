using System.Globalization;
using System.Text;
using System.Text.Json;
using Game.Sim.Events;
using Game.Sim.Logging;
using Game.Sim.Memory;
using Game.Sim.Routines;
using Game.Sim.Scenarios;
using Game.Sim.Snapshots;
using Game.Sim.Suspicion;

const string defaultScenario = "basement";
const ulong defaultSeed = 481_516;
const int defaultTicks = 16;

try
{
    RunnerOptions options = ParseArguments(args);
    InMemorySuspicionRuleRepository rules = LoadRules();
    var runs = new List<SimulationSummary>(options.Repeat);

    for (int runIndex = 0; runIndex < options.Repeat; runIndex++)
    {
        ulong runSeed = checked(options.Seed + (ulong)runIndex);
        BasementScenarioResult result;

        if (!string.IsNullOrEmpty(options.LoadSnapshotPath))
        {
            SessionSnapshot snapshot = SessionSnapshotSerializer.LoadFromFile(options.LoadSnapshotPath);
            BasementScenarioSession session = BasementScenarioSession.FromSnapshot(snapshot, rules, autoCompleteMovements: true);
            while (!session.IsComplete && session.Now.Tick < options.Ticks)
            {
                _ = session.AdvanceOneTick();
            }

            if (!string.IsNullOrEmpty(options.SaveSnapshotPath))
            {
                SessionSnapshot snapshotToSave = session.CaptureSnapshot();
                SessionSnapshotSerializer.SaveToFile(
                    snapshotToSave,
                    ResolveTracePath(options.SaveSnapshotPath, runIndex, options.Repeat));
            }

            result = session.BuildResult();
        }
        else
        {
            var scenarioOptions = new BasementScenarioOptions(runSeed, options.Ticks);
            result = options.Scenario.ToLowerInvariant() switch
            {
                "rumor-cascade" => new RumorCascadeScenario(rules).Run(scenarioOptions),
                "deceptive-alibi" => new DeceptiveAlibiScenario(rules).Run(scenarioOptions),
                "reality-breach" => new RealityBreachScenario(rules).Run(scenarioOptions),
                _ => new BasementScenario(rules).Run(scenarioOptions),
            };

            if (!string.IsNullOrEmpty(options.SaveSnapshotPath))
            {
                BasementScenarioSession session = new BasementScenario(rules).CreateSession(scenarioOptions, autoCompleteMovements: true);
                while (!session.IsComplete && session.Now.Tick < options.Ticks)
                {
                    _ = session.AdvanceOneTick();
                }

                SessionSnapshot snapshotToSave = session.CaptureSnapshot();
                SessionSnapshotSerializer.SaveToFile(
                    snapshotToSave,
                    ResolveTracePath(options.SaveSnapshotPath, runIndex, options.Repeat));
            }
        }

        string? tracePath = options.TracePath is null
            ? null
            : WriteTrace(ResolveTracePath(options.TracePath, runIndex, options.Repeat), result.Events);
        runs.Add(CreateSummary(options.Scenario, result, tracePath));
    }

    object report = runs.Count == 1
        ? runs[0]
        : CreateBatchSummary(options, runs);
    Console.WriteLine(JsonSerializer.Serialize(report, report.GetType(), JsonOptions));
    return 0;
}
catch (Exception exception) when (
    exception is ArgumentException or IOException or UnauthorizedAccessException)
{
    Console.Error.WriteLine(exception.Message);
    Console.Error.WriteLine(
        "Usage: dotnet run --project tools/SimRunner -- " +
        "[--scenario basement|rumor-cascade|deceptive-alibi|reality-breach] " +
        "[--seed <ulong>] [--ticks <int>=4>] [--repeat <positive-int>] " +
        "[--trace <jsonl-path>] [--save-snapshot <json-path>] [--load-snapshot <json-path>]");
    return 2;
}

static RunnerOptions ParseArguments(string[] arguments)
{
    string scenario = defaultScenario;
    ulong seed = defaultSeed;
    int ticks = defaultTicks;
    int repeat = 1;
    string? tracePath = null;
    string? saveSnapshotPath = null;
    string? loadSnapshotPath = null;

    for (int index = 0; index < arguments.Length; index += 2)
    {
        if (index + 1 >= arguments.Length)
        {
            throw new ArgumentException($"Missing value for '{arguments[index]}'.");
        }

        string option = arguments[index];
        string value = arguments[index + 1];
        switch (option)
        {
            case "--scenario" when string.Equals(value, "basement", StringComparison.OrdinalIgnoreCase):
                scenario = "basement";
                break;
            case "--scenario" when string.Equals(value, "rumor-cascade", StringComparison.OrdinalIgnoreCase):
                scenario = "rumor-cascade";
                break;
            case "--scenario" when string.Equals(value, "deceptive-alibi", StringComparison.OrdinalIgnoreCase):
                scenario = "deceptive-alibi";
                break;
            case "--scenario" when string.Equals(value, "reality-breach", StringComparison.OrdinalIgnoreCase):
                scenario = "reality-breach";
                break;
            case "--seed" when ulong.TryParse(
                value,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out ulong parsedSeed):
                seed = parsedSeed;
                break;
            case "--ticks" when int.TryParse(
                value,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out int parsedTicks) && parsedTicks >= BasementScenarioOptions.MinimumTicks:
                ticks = parsedTicks;
                break;
            case "--repeat" when int.TryParse(
                value,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out int parsedRepeat) && parsedRepeat > 0:
                repeat = parsedRepeat;
                break;
            case "--trace" when !string.IsNullOrWhiteSpace(value):
                tracePath = value.Trim();
                break;
            case "--save-snapshot" when !string.IsNullOrWhiteSpace(value):
                saveSnapshotPath = value.Trim();
                break;
            case "--load-snapshot" when !string.IsNullOrWhiteSpace(value):
                loadSnapshotPath = value.Trim();
                break;
            default:
                throw new ArgumentException($"Invalid option or value: '{option} {value}'.");
        }
    }

    if ((ulong)(repeat - 1) > ulong.MaxValue - seed)
    {
        throw new ArgumentException("Seed range overflows UInt64 for the requested repeat count.");
    }

    if (repeat > 1 && tracePath is not null && !tracePath.Contains("{run}", StringComparison.Ordinal))
    {
        throw new ArgumentException(
            "A repeated run trace path must contain the '{run}' filename placeholder.");
    }

    return new RunnerOptions(
        scenario,
        seed,
        ticks,
        repeat,
        tracePath,
        saveSnapshotPath,
        loadSnapshotPath);
}

static InMemorySuspicionRuleRepository LoadRules()
{
    string path = Path.Combine(
        AppContext.BaseDirectory,
        "Data",
        "SuspicionRules",
        "mvp.json");
    return JsonSuspicionRuleParser.Parse(File.ReadAllText(path));
}

static string WriteTrace(string requestedPath, IReadOnlyList<WorldEvent> worldEvents)
{
    string path = Path.GetFullPath(requestedPath);
    string? directory = Path.GetDirectoryName(path);
    if (!string.IsNullOrEmpty(directory))
    {
        Directory.CreateDirectory(directory);
    }

    using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
    using var output = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    WorldEventTrace.WriteJsonl(worldEvents, output);
    return path;
}

static string ResolveTracePath(string tracePath, int runIndex, int repeat) =>
    repeat == 1
        ? tracePath
        : tracePath.Replace(
            "{run}",
            (runIndex + 1).ToString("D4", CultureInfo.InvariantCulture),
            StringComparison.Ordinal);

static SimulationSummary CreateSummary(
    string scenario,
    BasementScenarioResult result,
    string? tracePath)
{
    var eventCounts = result.Events
        .GroupBy(worldEvent => worldEvent.Type.ToString())
        .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
    var goalCounts = result.Decisions
        .GroupBy(decision => decision.Goal.Type.ToString())
        .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
    int episodic = result.Memories.Count(memory => memory.Memory.Kind == MemoryKind.Episodic);
    int social = result.Memories.Count(memory => memory.Memory.Kind == MemoryKind.Social);

    return new SimulationSummary(
        scenario,
        result.Seed,
        result.CompletedAt.Tick,
        WorldEventTrace.ComputeSha256(result.Events),
        tracePath,
        new SimulationMetrics(
            result.Actors.Count,
            result.Events.Count,
            eventCounts,
            result.ObservationCount,
            result.Memories.Count,
            episodic,
            social,
            result.Decisions.Count,
            goalCounts),
        new BasementMilestone(
            result.RestrictedEntry.Id.Value,
            result.BobRumor.RootEventId.Value,
            result.BobRumor.InformationSource?.Value,
            result.AnnaFirstSuspicionAt.Tick,
            result.BobFirstSuspicionAt.Tick,
            CreateDecisionSummary(result.AnnaInitialDecision),
            CreateDecisionSummary(result.BobInitialDecision),
            result.Events.Any(e => e.Actor == BasementScenario.Anna && e.Type == EventType.ShareInformation),
            result.Events.Any(e => e.Actor == BasementScenario.Bob && e.Type == EventType.EnterLocation && e.Location == BasementScenario.Basement)),
        new SuspicionSummary(
            CreateSuspicionScores(result.AnnaSuspicion.Vector),
            CreateSuspicionScores(result.BobSuspicion.Vector)),
        new FinalLocations(
            result.AnnaFinalLocation.Value,
            result.BobFinalLocation.Value,
            result.GeorgeFinalLocation.Value));
}

static BatchSimulationSummary CreateBatchSummary(
    RunnerOptions options,
    IReadOnlyList<SimulationSummary> runs)
{
    HashSet<string> uniqueFingerprints = runs
        .Select(run => run.EventFingerprint)
        .ToHashSet(StringComparer.Ordinal);
    double averageTicks = runs.Average(run => (double)run.CompletedTicks);
    double averageEvents = runs.Average(run => (double)run.Metrics.WorldEvents);
    double averageObservations = runs.Average(run => (double)run.Metrics.Observations);
    double averageDecisions = runs.Average(run => (double)run.Metrics.Decisions);
    double averageAnnaSuspicionTicks = runs.Average(run => (double)run.Milestone.AnnaFirstSuspicionTick);
    double averageBobSuspicionTicks = runs.Average(run => (double)run.Milestone.BobFirstSuspicionTick);

    return new BatchSimulationSummary(
        options.Scenario,
        options.Seed,
        runs.Count,
        uniqueFingerprints.Count,
        uniqueFingerprints.Count == 1,
        averageTicks,
        averageEvents,
        averageObservations,
        averageDecisions,
        averageAnnaSuspicionTicks,
        averageBobSuspicionTicks,
        runs);
}

static DecisionSummary CreateDecisionSummary(NpcRoutineDecision decision) =>
    new(
        decision.Time.Tick,
        decision.Entity.Value,
        decision.Goal.Type.ToString(),
        decision.Goal.Destination.Value,
        decision.Goal.Target?.Value,
        decision.Goal.InteractionPartner?.Value,
        decision.Goal.TotalUtility,
        decision.Moved);

static SuspicionScores CreateSuspicionScores(SuspicionVector vector) =>
    new(
        vector.Criminality,
        vector.Secrecy,
        vector.RoleDeviation,
        vector.MetaBehavior,
        vector.ImpossibleBehavior,
        vector.Deception);

internal sealed record RunnerOptions(
    string Scenario,
    ulong Seed,
    int Ticks,
    int Repeat,
    string? TracePath,
    string? SaveSnapshotPath,
    string? LoadSnapshotPath);

internal sealed record BatchSimulationSummary(
    string Scenario,
    ulong StartSeed,
    int TotalRuns,
    int UniqueFingerprints,
    bool DeterministicAcrossSeeds,
    double AverageCompletedTicks,
    double AverageWorldEvents,
    double AverageObservations,
    double AverageDecisions,
    double AverageAnnaFirstSuspicionTick,
    double AverageBobFirstSuspicionTick,
    IReadOnlyList<SimulationSummary> Runs);

internal sealed record SimulationSummary(
    string Scenario,
    ulong Seed,
    long CompletedTicks,
    string EventFingerprint,
    string? TracePath,
    SimulationMetrics Metrics,
    BasementMilestone Milestone,
    SuspicionSummary Suspicion,
    FinalLocations FinalLocations);

internal sealed record SimulationMetrics(
    int Actors,
    int WorldEvents,
    IReadOnlyDictionary<string, int> EventTypes,
    int Observations,
    int Memories,
    int EpisodicMemories,
    int SocialMemories,
    int Decisions,
    IReadOnlyDictionary<string, int> Goals);

internal sealed record BasementMilestone(
    long RestrictedEntryRootEventId,
    long BobRumorRootEventId,
    string? BobInformationSource,
    long AnnaFirstSuspicionTick,
    long BobFirstSuspicionTick,
    DecisionSummary AnnaInitialDecision,
    DecisionSummary BobInitialDecision,
    bool ShareEventCreated,
    bool FollowEnterEventCreated);

internal sealed record DecisionSummary(
    long Tick,
    string Actor,
    string Goal,
    string Destination,
    string? Target,
    string? InteractionPartner,
    float Utility,
    bool Moved);

internal sealed record SuspicionSummary(SuspicionScores AnnaAboutGeorge, SuspicionScores BobAboutGeorge);

internal sealed record SuspicionScores(
    float Criminality,
    float Secrecy,
    float RoleDeviation,
    float MetaBehavior,
    float ImpossibleBehavior,
    float Deception);

internal sealed record FinalLocations(string Anna, string Bob, string George);

internal static partial class Program
{
    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };
}
