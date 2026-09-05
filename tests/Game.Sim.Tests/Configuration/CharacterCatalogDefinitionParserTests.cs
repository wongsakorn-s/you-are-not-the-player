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

        // Every character is somebody in both languages: a name, a job, and a
        // place they are normally found. Six strangers met in one night is a lot
        // to hold, and the job titles used to live in a switch statement in the
        // client where the content file could not see them.
        foreach (CharacterDefinition character in catalog.Characters)
        {
            Assert.False(string.IsNullOrWhiteSpace(character.DisplayNameThai));
            Assert.False(string.IsNullOrWhiteSpace(character.RoleThai));
            Assert.False(string.IsNullOrWhiteSpace(character.Station));
            Assert.False(string.IsNullOrWhiteSpace(character.StationThai));
            Assert.NotEqual(character.NameIn(thai: true), character.NameIn(thai: false));
            Assert.Equal(character.Role, character.RoleIn(thai: false));
        }
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
