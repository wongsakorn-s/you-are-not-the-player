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
        // Six people, six voices: no two of them answer the same question with
        // the same sentence in either language.
        foreach (string line in new[] { "Schedule", "Confront", "ObjectLine" })
        {
            string[] spoken =
            [
                .. catalog.Characters.Values.Select(lines => line switch
                {
                    "Schedule" => lines.Schedule,
                    "Confront" => lines.Confront,
                    _ => lines.ObjectLine,
                }),
            ];
            Assert.Equal(spoken.Length, spoken.Distinct(StringComparer.Ordinal).Count());
        }
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
