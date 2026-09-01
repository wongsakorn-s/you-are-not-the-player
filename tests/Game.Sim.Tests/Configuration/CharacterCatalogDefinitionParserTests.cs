using Game.Client.Godot.Configuration;

namespace Game.Sim.Tests.Configuration;

public sealed class CharacterCatalogDefinitionParserTests
{
    [Fact]
    public void ProductionCatalog_ContainsLockedSixCharacterCast()
    {
        CharacterCatalogDefinition catalog = CharacterCatalogDefinitionParser.Parse(
            File.ReadAllText(ContentPath("Characters", "characters.json")));

        Assert.Equal(6, catalog.Characters.Length);
        Assert.Equal("George", catalog.GetCharacter("george").DisplayName);
        Assert.Equal("Clara", catalog.GetCharacter("charlie").DisplayName);
        Assert.Equal("Manager", catalog.GetCharacter("evelyn").Role);
    }

    [Fact]
    public void Parse_RejectsDuplicateCharacterIds()
    {
        const string InvalidJson = """
            {
              "schemaVersion": 1,
              "characters": [
                {
                  "id": "same",
                  "displayName": "One",
                  "role": "Guest",
                  "description": "First",
                  "color": "ffffff",
                  "portraitKey": "one"
                },
                {
                  "id": "same",
                  "displayName": "Two",
                  "role": "Guest",
                  "description": "Second",
                  "color": "000000",
                  "portraitKey": "two"
                }
              ]
            }
            """;

        FormatException error = Assert.Throws<FormatException>(
            () => CharacterCatalogDefinitionParser.Parse(InvalidJson));

        Assert.Contains("Duplicate character", error.Message, StringComparison.Ordinal);
    }

    private static string ContentPath(string directory, string fileName) =>
        Path.Combine(AppContext.BaseDirectory, "Data", directory, fileName);
}
