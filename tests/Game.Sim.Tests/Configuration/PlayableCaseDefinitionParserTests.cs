using Game.Client.Godot.Configuration;
using Game.Sim.Cases;
using Game.Sim.Entities;

namespace Game.Sim.Tests.Configuration;

public sealed class PlayableCaseDefinitionParserTests
{
    [Fact]
    public void ProductionCase_PinsOnlyWhatItsEndingProseDependsOn()
    {
        CharacterCatalogDefinition characters = CharacterCatalogDefinitionParser.Parse(
            File.ReadAllText(ContentPath("Characters", "characters.json")));
        HotelWorldDefinition hotel = HotelWorldDefinitionParser.Parse(
            File.ReadAllText(ContentPath("Hotel", "hotel-world.json")));
        PlayableCaseDefinition playableCase = PlayableCaseDefinitionParser.Parse(
            File.ReadAllText(ContentPath("Cases", "first-playable-case.json")));

        playableCase.ValidateReferences(characters, hotel);

        Assert.Equal("george", playableCase.HumanHost);
        Assert.Equal(481_516UL, playableCase.Seed);

        // The culprit stays pinned because the aftermath text names who opened the
        // basement door. Who is being steered, and how, is left to the seed so two
        // runs of the same case ask different questions.
        Assert.Equal("george", playableCase.IncidentCulprit);
        Assert.Null(playableCase.HiddenPlayer);
        Assert.Null(playableCase.ParsedPlayerArchetype);
    }

    [Fact]
    public void ProductionCase_GeneratesAnAnswerableTruthFromItsSeed()
    {
        CharacterCatalogDefinition characters = CharacterCatalogDefinitionParser.Parse(
            File.ReadAllText(ContentPath("Characters", "characters.json")));
        PlayableCaseDefinition playableCase = PlayableCaseDefinitionParser.Parse(
            File.ReadAllText(ContentPath("Cases", "first-playable-case.json")));
        var host = new EntityId(playableCase.HumanHost);

        SessionTruth truth = CaseGenerator.Generate(
            playableCase.Seed,
            new CaseGenerationOptions(
                host,
                characters.Characters.Select(character => new EntityId(character.Id)),
                shiftTicks: 360,
                pinnedIncidentCulprit: new EntityId(playableCase.IncidentCulprit!)));

        Assert.Equal(new EntityId("george"), truth.IncidentCulprit);

        // The accusation screen cannot name the host yet, so a case where the host
        // is the hidden player would be unwinnable.
        Assert.NotEqual(host, truth.HiddenPlayer);
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
