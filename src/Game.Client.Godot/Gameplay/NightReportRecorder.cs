using System.Globalization;
using System.Text;
using Game.Sim.Player;

namespace Game.Client.Godot.Gameplay;

/// <summary>
/// Groundwork for Milestone 6. Every pacing number in the build - the
/// nine-minute night, the exposure thresholds, the coalition score - was
/// calculated and never played, so a night is walked unattended and what the
/// game said back is written down against the clock it said it on.
/// </summary>
public sealed class NightReportRecorder
{
    private const int BlockTicks = 60;

    private readonly List<(long Tick, string Channel, string Text)> _entries = [];
    private readonly List<long> _toldTicks = [];
    private readonly List<long> _actionTicks = [];
    private readonly List<long> _clueTicks = [];

    private ExposureLevel _level = ExposureLevel.Unnoticed;
    private int _clues;
    private int _claims;
    private int _contradictions;
    private bool _netArmed;

    public float PeakExposure { get; private set; }

    public int Clues => _clues;

    public int Contradictions => _contradictions;

    /// <summary>Something the player chose to do.</summary>
    public void Action(long tick, string text)
    {
        _entries.Add((tick, "you", text));
        _actionTicks.Add(tick);
    }

    /// <summary>
    /// Something the game put in front of the player unprompted - a shift beat,
    /// an anomaly interrupt, a warning. The gaps between these are the thing
    /// this whole report exists to measure.
    /// </summary>
    public void Told(long tick, string text)
    {
        _entries.Add((tick, "game", text));
        _toldTicks.Add(tick);
    }

    /// <summary>
    /// Called every tick; records only the ticks where something the player can
    /// see actually changed, so the timeline stays a list of moments.
    /// </summary>
    public void Observe(
        long tick,
        ExposureReport exposure,
        int clues,
        int claims,
        int contradictions,
        bool netArmed)
    {
        ArgumentNullException.ThrowIfNull(exposure);

        PeakExposure = Math.Max(PeakExposure, exposure.Peak);
        if (exposure.Level != _level)
        {
            _entries.Add((tick, "meter",
                $"exposure {_level} -> {exposure.Level} (peak {exposure.Peak:F0}, " +
                $"{exposure.Observers.Count} watching)"));
            _level = exposure.Level;
        }

        if (clues > _clues)
        {
            for (int index = _clues; index < clues; index++)
            {
                _clueTicks.Add(tick);
            }

            _clues = clues;
        }

        if (claims != _claims)
        {
            _entries.Add((tick, "meter", $"statements on record: {claims}"));
            _claims = claims;
        }

        if (contradictions != _contradictions)
        {
            _entries.Add((tick, "meter",
                $"catchable contradictions: {_contradictions} -> {contradictions}"));
            _contradictions = contradictions;
        }

        if (netArmed != _netArmed)
        {
            _entries.Add((tick, "meter", netArmed
                ? "the hotel has enough on you to move"
                : "the case against you fell back below the bar"));
            _netArmed = netArmed;
        }
    }

    public string Build(
        IReadOnlyList<string> headerLines,
        Func<long, string> clock,
        long deadlineTick)
    {
        ArgumentNullException.ThrowIfNull(headerLines);
        ArgumentNullException.ThrowIfNull(clock);

        var text = new StringBuilder();
        _ = text.AppendLine("# Night report");
        _ = text.AppendLine();
        foreach (string line in headerLines)
        {
            _ = text.AppendLine(CultureInfo.InvariantCulture, $"- {line}");
        }

        _ = text.AppendLine();
        _ = text.AppendLine("## Was anything happening?");
        _ = text.AppendLine();
        _ = text.AppendLine("| clock | game spoke | you acted | clues gained |");
        _ = text.AppendLine("|---|---|---|---|");
        for (long start = 0; start < deadlineTick; start += BlockTicks)
        {
            long end = start + BlockTicks;
            _ = text.AppendLine(CultureInfo.InvariantCulture, $"| {clock(start)}-{clock(end - 1)} | {CountIn(_toldTicks, start, end)} | " +
                $"{CountIn(_actionTicks, start, end)} | {CountIn(_clueTicks, start, end)} |");
        }

        _ = text.AppendLine();
        (long gap, long from) = LongestSilence(deadlineTick);
        _ = text.AppendLine(CultureInfo.InvariantCulture, $"Longest stretch where the game said nothing on its own: **{gap} min** " +
            $"({clock(from)} -> {clock(from + gap)}).");

        _ = text.AppendLine();
        _ = text.AppendLine("## What happened, in order");
        _ = text.AppendLine();
        foreach ((long tick, string channel, string entry) in _entries)
        {
            _ = text.AppendLine(CultureInfo.InvariantCulture, $"`{clock(tick)}` **{channel}** {entry}");
        }

        return text.ToString();
    }

    private static int CountIn(List<long> ticks, long start, long end)
    {
        int count = 0;
        foreach (long tick in ticks)
        {
            if (tick >= start && tick < end)
            {
                count++;
            }
        }

        return count;
    }

    private (long Gap, long From) LongestSilence(long deadlineTick)
    {
        long gap = 0;
        long from = 0;
        long previous = 0;
        foreach (long tick in _toldTicks)
        {
            if (tick - previous > gap)
            {
                gap = tick - previous;
                from = previous;
            }

            previous = tick;
        }

        if (deadlineTick - previous > gap)
        {
            gap = deadlineTick - previous;
            from = previous;
        }

        return (gap, from);
    }
}
