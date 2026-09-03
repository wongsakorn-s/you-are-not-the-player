using Game.Client.Godot.Configuration;

namespace Game.Sim.Tests.Configuration;

public sealed class DialogueCatalogDefinitionParserTests
{
    [Fact]
    public void ProductionCatalog_ContainsDistinctLinesForEveryCharacter()
    {
        string path = Path.Combine(
            AppContext.BaseDirectory,
            "Data",
            "Dialogue",
            "dialogue-lines.json");

        DialogueCatalogDefinition catalog = DialogueCatalogDefinitionParser.Parse(
            File.ReadAllText(path));

        Assert.Equal(6, catalog.Characters.Count);
        Assert.Contains("night rounds", catalog.GetLines("anna").Schedule, StringComparison.Ordinal);
        Assert.Contains("security", catalog.GetLines("bob").Schedule, StringComparison.OrdinalIgnoreCase);
        Assert.NotEqual(catalog.GetLines("anna").Confront, catalog.GetLines("bob").Confront);
        Assert.NotNull(catalog.GetLines("anna").Thai);
        Assert.NotNull(catalog.GetLines("george").Thai);

        foreach ((string _, DialogueCharacterLines lines) in catalog.Characters)
        {
            DialogueCharacterLines thai = Assert.IsType<DialogueCharacterLines>(lines.Thai);
            string combinedThai = string.Join(
                '\n',
                thai.Schedule,
                thai.AskAboutSubject,
                thai.AskAboutSubjectNoMemory,
                thai.ObjectLine,
                thai.Confront);
            Assert.DoesNotContain("George", combinedThai, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Player", combinedThai, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void Parse_RejectsMissingDialogueLine()
    {
        const string InvalidJson = """
            {
              "schemaVersion": 1,
              "characters": {
                "anna": {
                  "schedule": "line",
                  "askAboutSubject": "line",
                  "askAboutSubjectNoMemory": "line",
                  "object": "line",
                  "confront": ""
                }
              }
            }
            """;

        FormatException error = Assert.Throws<FormatException>(
            () => DialogueCatalogDefinitionParser.Parse(InvalidJson));

        Assert.Contains("confront", error.Message, StringComparison.OrdinalIgnoreCase);
    }
}
