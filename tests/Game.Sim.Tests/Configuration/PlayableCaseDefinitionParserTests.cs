using Game.Client.Godot.Configuration;
using Game.Sim.PlayerAi;

namespace Game.Sim.Tests.Configuration;

public sealed class PlayableCaseDefinitionParserTests
{
    [Fact]
    public void ProductionCase_ReferencesKnownContentAndLocksFirstPlayableTruth()
    {
        CharacterCatalogDefinition characters = CharacterCatalogDefinitionParser.Parse(
            File.ReadAllText(ContentPath("Characters", "characters.json")));
        HotelWorldDefinition hotel = HotelWorldDefinitionParser.Parse(
            File.ReadAllText(ContentPath("Hotel", "hotel-world.json")));
        PlayableCaseDefinition playableCase = PlayableCaseDefinitionParser.Parse(
            File.ReadAllText(ContentPath("Cases", "first-playable-case.json")));

        playableCase.ValidateReferences(characters, hotel);

        Assert.Equal("george", playableCase.HumanHost);
        Assert.Equal("charlie", playableCase.HiddenPlayer);
        Assert.Equal("george", playableCase.IncidentCulprit);
        Assert.Equal(PlayerAiArchetype.Explorer, playableCase.ParsedPlayerArchetype);
        Assert.Equal(481_516UL, playableCase.Seed);
    }

    [Fact]
    public void Parse_RejectsUnknownPlayerArchetype()
    {
        const string InvalidJson = """
            {
              "schemaVersion": 1,
              "caseId": "test",
              "title": "Test",
              "humanHost": "george",
              "hiddenPlayer": "charlie",
              "incidentCulprit": "george",
              "playerArchetype": "Unknown",
              "openingLocation": "lobby",
              "objective": "Test objective",
              "seed": 1
            }
            """;

        FormatException error = Assert.Throws<FormatException>(
            () => PlayableCaseDefinitionParser.Parse(InvalidJson));

        Assert.Contains("Unknown player archetype", error.Message, StringComparison.Ordinal);
    }

    private static string ContentPath(string directory, string fileName) =>
        Path.Combine(AppContext.BaseDirectory, "Data", directory, fileName);
}
