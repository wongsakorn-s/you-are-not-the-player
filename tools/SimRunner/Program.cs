using System.Text.Json;
using Game.Sim.Random;
using Game.Sim.Time;

const ulong defaultSeed = 481_516;
const int defaultTicks = 16;

try
{
    (ulong seed, int ticks) = ParseArguments(args);
    var clock = new SimClock();
    var random = new Pcg32SimRandom(seed);
    var samples = new List<SimSample>(ticks);

    for (int index = 0; index < ticks; index++)
    {
        SimTime now = clock.AdvanceOneTick();
        samples.Add(new SimSample(now.Tick, random.NextInt(0, 1_000)));
    }

    var result = new SimRunResult(seed, clock.TicksPerSecond, samples);
    Console.WriteLine(JsonSerializer.Serialize(result, JsonOptions));
    return 0;
}
catch (ArgumentException exception)
{
    Console.Error.WriteLine(exception.Message);
    Console.Error.WriteLine("Usage: dotnet run --project tools/SimRunner -- [--seed <ulong>] [--ticks <positive-int>]");
    return 2;
}

static (ulong Seed, int Ticks) ParseArguments(string[] arguments)
{
    ulong seed = defaultSeed;
    int ticks = defaultTicks;

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
            case "--seed" when ulong.TryParse(value, out ulong parsedSeed):
                seed = parsedSeed;
                break;
            case "--ticks" when int.TryParse(value, out int parsedTicks) && parsedTicks > 0:
                ticks = parsedTicks;
                break;
            default:
                throw new ArgumentException($"Invalid option or value: '{option} {value}'.");
        }
    }

    return (seed, ticks);
}

internal sealed record SimSample(long Tick, int RandomValue);

internal sealed record SimRunResult(
    ulong Seed,
    int TicksPerSecond,
    IReadOnlyList<SimSample> Samples);

internal static partial class Program
{
    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };
}
