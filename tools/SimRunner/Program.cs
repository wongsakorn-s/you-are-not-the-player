using System.Globalization;
using System.Text;
using System.Text.Json;
using Game.Sim.Events;
using Game.Sim.Logging;
using Game.Sim.Memory;
using Game.Sim.Scenarios;
using Game.Sim.Suspicion;

const string defaultScenario = "basement";
const ulong defaultSeed = 481_516;
const int defaultTicks = 16;

try
{
    RunnerOptions options = ParseArguments(args);
    InMemorySuspicionRuleRepository rules = LoadRules();
    var scenario = new BasementScenario(rules);
    var runs = new List<SimulationSummary>(options.Repeat);
    for (int runIndex = 0; runIndex < options.Repeat; runIndex++)
    {
        ulong runSeed = checked(options.Seed + (ulong)runIndex);
        BasementScenarioResult result = scenario.Run(
            new BasementScenarioOptions(runSeed, options.Ticks));
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
        "[--scenario basement] [--seed <ulong>] [--ticks <int>=4>] " +
        "[--repeat <positive-int>] [--trace <jsonl-path>]");
    return 2;
}

static RunnerOptions ParseArguments(string[] arguments)
{
    string scenario = defaultScenario;
    ulong seed = defaultSeed;
    int ticks = defaultTicks;
    int repeat = 1;
    string? tracePath = null;

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
            case "--scenario" when string.Equals(
                value,
                defaultScenario,
                StringComparison.OrdinalIgnoreCase):
                scenario = defaultScenario;
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

    return new RunnerOptions(scenario, seed, ticks, repeat, tracePath);
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
    IReadOnlyDictionary<string, int> eventTypes = result.Events
        .GroupBy(worldEvent => worldEvent.Type.ToString(), StringComparer.Ordinal)
        .OrderBy(group => group.Key, StringComparer.Ordinal)
        .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
    IReadOnlyDictionary<string, int> goals = result.Decisions
        .GroupBy(decision => decision.Goal.Type.ToString(), StringComparer.Ordinal)
        .OrderBy(group => group.Key, StringComparer.Ordinal)
        .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
    int episodicMemories = result.Memories.Count(item => item.Memory.Kind == MemoryKind.Episodic);
    int socialMemories = result.Memories.Count(item => item.Memory.Kind == MemoryKind.Social);

    var metrics = new SimulationMetrics(
        result.Actors.Count,
        result.Events.Count,
        eventTypes,
        result.ObservationCount,
        result.Memories.Count,
        episodicMemories,
        socialMemories,
        result.Decisions.Count,
        goals);
    var milestone = new BasementMilestone(
        result.RestrictedEntry.Id.Value,
        result.BobRumor.RootEventId.Value,
        result.BobRumor.InformationSource?.Value,
        result.AnnaFirstSuspicionAt.Tick,
        result.BobFirstSuspicionAt.Tick,
        FormatDecision(result.AnnaInitialDecision),
        FormatDecision(result.BobInitialDecision),
        result.Events.Any(worldEvent =>
            worldEvent.Type == EventType.ShareInformation &&
            worldEvent.Actor == BasementScenario.Anna &&
            worldEvent.Target == BasementScenario.Bob),
        result.Events.Any(worldEvent =>
            worldEvent.Type == EventType.EnterLocation &&
            worldEvent.Actor == BasementScenario.Bob &&
            worldEvent.Location == BasementScenario.Basement));

    return new SimulationSummary(
        scenario,
        result.Seed,
        result.CompletedAt.Tick,
        WorldEventTrace.ComputeSha256(result.Events),
        tracePath,
        metrics,
        milestone,
        new SuspicionSummary(
            ToScores(result.AnnaSuspicion.Vector),
            ToScores(result.BobSuspicion.Vector)),
        new FinalLocations(
            result.AnnaFinalLocation.Value,
            result.BobFinalLocation.Value,
            result.GeorgeFinalLocation.Value));
}

static BatchSimulationSummary CreateBatchSummary(
    RunnerOptions options,
    IReadOnlyList<SimulationSummary> runs)
{
    SimulationSummary first = runs[0];
    SimulationSummary last = runs[^1];
    return new BatchSimulationSummary(
        options.Scenario,
        options.Seed,
        options.Ticks,
        runs.Count,
        options.TracePath,
        new BatchMetrics(
            runs.Select(run => run.EventFingerprint).Distinct(StringComparer.Ordinal).Count(),
            runs.Min(run => run.Metrics.WorldEvents),
            runs.Max(run => run.Metrics.WorldEvents),
            runs.Average(run => run.Metrics.WorldEvents),
            runs.Average(run => run.Metrics.Observations),
            runs.Average(run => run.Metrics.Memories)),
        first,
        last);
}

static DecisionSummary FormatDecision(Game.Sim.Routines.NpcRoutineDecision decision) =>
    new(
        decision.Time.Tick,
        decision.Entity.Value,
        decision.Goal.Type.ToString(),
        decision.Goal.Destination.Value,
        decision.Goal.Target?.Value,
        decision.Goal.InteractionPartner?.Value,
        decision.Goal.TotalUtility,
        decision.Moved);

static SuspicionScores ToScores(SuspicionVector vector) =>
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
    string? TracePath);

internal sealed record BatchSimulationSummary(
    string Scenario,
    ulong InitialSeed,
    int TicksPerRun,
    int Runs,
    string? TracePathPattern,
    BatchMetrics Aggregate,
    SimulationSummary FirstRun,
    SimulationSummary LastRun);

internal sealed record BatchMetrics(
    int UniqueEventFingerprints,
    int MinimumWorldEvents,
    int MaximumWorldEvents,
    double AverageWorldEvents,
    double AverageObservations,
    double AverageMemories);

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
