using Game.Client.Godot.Configuration;
using Game.Client.Godot.Presentation;
using Game.Sim.Entities;
using Game.Sim.Player;

namespace Game.Sim.Tests.Presentation;

public sealed class DialogueNarrativeFormatterTests
{
    [Fact]
    public void Format_UsesAuthoredScheduleLineInsteadOfInternalIdText()
    {
        DialogueNarrativeFormatter formatter = new(LoadCatalog());
        var partner = new EntityId("anna");
        var request = new DialogueRequest(
            DialogueActionKind.InquireSchedule,
            new EntityId("george"),
            partner);

        string result = formatter.Format(
            partner,
            request,
            new DialogueOutcome(true, "anna: raw fallback"),
            id => id.Value,
            location => location.Value);

        Assert.Contains("night rounds", result, StringComparison.Ordinal);
        Assert.DoesNotContain("anna:", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Format_UsesNoMemoryBranchForUnconfirmedSubject()
    {
        DialogueNarrativeFormatter formatter = new(LoadCatalog());
        var partner = new EntityId("bob");
        var subject = new EntityId("charlie");
        var request = new DialogueRequest(
            DialogueActionKind.AskAboutSubject,
            new EntityId("george"),
            partner,
            subject: subject);

        string result = formatter.Format(
            partner,
            request,
            new DialogueOutcome(true, "raw fallback"),
            id => id.Value,
            location => location.Value);

        Assert.Contains("No clean sighting", result, StringComparison.Ordinal);
        Assert.Contains("charlie", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Format_UsesThaiVariantWhenRequested()
    {
        DialogueNarrativeFormatter formatter = new(LoadCatalog());
        var partner = new EntityId("anna");
        var request = new DialogueRequest(
            DialogueActionKind.InquireSchedule,
            new EntityId("george"),
            partner);

        string result = formatter.Format(
            partner,
            request,
            new DialogueOutcome(true, "raw fallback"),
            id => id.Value,
            location => location.Value,
            useThai: true);

        Assert.Contains("ฉันกำลังตรวจรอบกลางคืน", result, StringComparison.Ordinal);
    }

    private static DialogueCatalogDefinition LoadCatalog() =>
        DialogueCatalogDefinitionParser.Parse(
            File.ReadAllText(Path.Combine(
                AppContext.BaseDirectory,
                "Data",
                "Dialogue",
                "dialogue-lines.json")));
}
