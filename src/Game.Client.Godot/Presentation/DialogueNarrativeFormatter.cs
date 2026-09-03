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
            return outcome.FailureReason ?? "The conversation could not continue.";
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
        return result;
    }
}
