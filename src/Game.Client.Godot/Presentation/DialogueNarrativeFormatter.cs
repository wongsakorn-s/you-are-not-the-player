using Game.Client.Godot.Configuration;
using Game.Sim.Entities;
using Game.Sim.Locations;
using Game.Sim.Player;

namespace Game.Client.Godot.Presentation;

public sealed class DialogueNarrativeFormatter
{
    private readonly DialogueCatalogDefinition _catalog;

    public DialogueNarrativeFormatter(DialogueCatalogDefinition catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        _catalog = catalog;
    }

    public string Format(
        EntityId partner,
        DialogueRequest request,
        DialogueOutcome outcome,
        Func<EntityId, string> displayEntity,
        Func<LocationId, string> displayLocation,
        string? objectName = null,
        bool useThai = false)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(outcome);
        ArgumentNullException.ThrowIfNull(displayEntity);
        ArgumentNullException.ThrowIfNull(displayLocation);

        if (!outcome.Succeeded)
        {
            // The simulation's reasons are diagnostics - "Cannot speak with bob;
            // not in the same location" names an entity id and a code path. What
            // the player needs is what happened in the room.
            return useThai
                ? "คุณคุยกับเขาตอนนี้ไม่ได้ ต้องอยู่ห้องเดียวกันก่อน"
                : "There is no one here to say that to.";
        }

        DialogueCharacterLines lines = _catalog.GetLines(partner.Value);
        if (useThai && lines.Thai is not null)
        {
            lines = lines.Thai;
        }
        string template = request.Kind switch
        {
            DialogueActionKind.InquireSchedule => lines.Schedule,
            DialogueActionKind.AskAboutSubject => outcome.TransferredMemory is null
                ? lines.AskAboutSubjectNoMemory
                : lines.AskAboutSubject,
            DialogueActionKind.InquireAboutObject => lines.ObjectLine,
            DialogueActionKind.ConfrontEvidence => lines.Confront,
            DialogueActionKind.ShareRumor => lines.AskAboutSubject,
            _ => throw new ArgumentOutOfRangeException(nameof(request), request.Kind, "Unknown dialogue action."),
        };

        EntityId subject = request.Subject ?? outcome.TransferredMemory?.Subject ?? partner;
        LocationId location = outcome.TransferredMemory?.Location ?? new LocationId("hotel");
        string result = template
            .Replace("{subject}", displayEntity(subject), StringComparison.Ordinal)
            .Replace("{location}", displayLocation(location), StringComparison.Ordinal)
            .Replace(
                "{time}",
                JournalPresentationFormatter.FormatClock(outcome.TransferredMemory?.EventTime.Tick ?? 0),
                StringComparison.Ordinal)
            .Replace(
                "{tick}",
                JournalPresentationFormatter.FormatClock(outcome.TransferredMemory?.EventTime.Tick ?? 0),
                StringComparison.Ordinal)
            .Replace("{object}", objectName ?? request.TargetObjectId ?? "object", StringComparison.Ordinal);

        // A statement the case file will hold against someone has to be a
        // statement the player actually heard. The catalog line sets the tone;
        // the claim itself has to be said out loud or the two records disagree.
        if (outcome.Claim is { } claim)
        {
            string where = displayLocation(claim.ClaimedLocation);
            string when = JournalPresentationFormatter.FormatClock(claim.ClaimedTime.Tick);
            result += useThai
                ? $"\n\n\u0e15\u0e2d\u0e19 {when} \u0e09\u0e31\u0e19\u0e2d\u0e22\u0e39\u0e48\u0e17\u0e35\u0e48{where}"
                : $"\n\nAround {when} I was at {where}.";
        }

        return result;
    }
}
