using System.Globalization;
using System.Text;
using Game.Sim.Entities;
using Game.Sim.Locations;
using Game.Sim.Player;
using Game.Sim.Suspicion;

namespace Game.Client.Godot.Presentation;

public static class JournalPresentationFormatter
{
    public static string Format(
        PlayerJournal journal,
        Func<EntityId, string> displayEntity,
        Func<LocationId, string> displayLocation)
    {
        ArgumentNullException.ThrowIfNull(journal);
        ArgumentNullException.ThrowIfNull(displayEntity);
        ArgumentNullException.ThrowIfNull(displayLocation);

        var text = new StringBuilder();
        _ = text.AppendLine(CultureInfo.InvariantCulture, $"LOCATION: {displayLocation(journal.CurrentLocation)}   TIME: T{journal.CurrentTime.Tick:00}");
        _ = text.AppendLine();
        _ = text.AppendLine("KNOWN EVENTS");

        if (journal.Entries.Count == 0)
        {
            _ = text.AppendLine("No reliable observations or rumors recorded yet.");
        }
        else
        {
            foreach (PlayerJournalEntry entry in journal.Entries)
            {
                string source = entry.InformationSource is { } informationSource
                    ? $"source: {displayEntity(informationSource)}"
                    : "source: direct observation";
                int confidence = (int)MathF.Round(entry.Confidence * 100.0f);
                _ = text.AppendLine(CultureInfo.InvariantCulture, $"• T{entry.EventTime.Tick:00} {LocalizeSummary(entry.Summary, displayEntity)}");
                _ = text.AppendLine(CultureInfo.InvariantCulture, $"  confidence: {confidence}% | {source} | root event: {entry.RootEventId.Value}");
            }
        }

        _ = text.AppendLine();
        _ = text.AppendLine("SUSPICION NOTES");
        if (journal.SuspicionSnapshots.Count == 0)
        {
            _ = text.AppendLine("No suspicion supported by evidence yet.");
        }
        else
        {
            foreach (SuspicionSnapshot suspicion in journal.SuspicionSnapshots
                         .OrderByDescending(GetTotalSuspicion))
            {
                _ = text.AppendLine(CultureInfo.InvariantCulture, $"• {displayEntity(suspicion.Subject)} — score {GetTotalSuspicion(suspicion):0.0} ({suspicion.Evidence.Count} evidence)");
                _ = text.AppendLine(CultureInfo.InvariantCulture, $"  secrecy {suspicion.Vector.Secrecy:0.0} | role deviation {suspicion.Vector.RoleDeviation:0.0} | meta {suspicion.Vector.MetaBehavior:0.0} | impossible {suspicion.Vector.ImpossibleBehavior:0.0}");
            }
        }

        return text.ToString().TrimEnd();
    }

    private static float GetTotalSuspicion(SuspicionSnapshot snapshot) =>
        snapshot.Vector.Criminality +
        snapshot.Vector.Secrecy +
        snapshot.Vector.RoleDeviation +
        snapshot.Vector.MetaBehavior +
        snapshot.Vector.ImpossibleBehavior +
        snapshot.Vector.Deception;

    private static string LocalizeSummary(
        string summary,
        Func<EntityId, string> displayEntity)
    {
        string localized = summary;
        foreach (string actorId in new[] { "anna", "bob", "charlie", "dana", "evelyn", "george" })
        {
            localized = localized.Replace(
                actorId,
                displayEntity(new EntityId(actorId)),
                StringComparison.OrdinalIgnoreCase);
        }

        return localized;
    }
}
