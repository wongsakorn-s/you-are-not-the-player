using System.Text.Json;
using Game.Sim.PlayerAi;

namespace Game.Client.Godot.Configuration;

public sealed record PlayableCaseDefinition(
    int SchemaVersion,
    string CaseId,
    string Title,
    string HumanHost,
    string HiddenPlayer,
    string IncidentCulprit,
    string PlayerArchetype,
    string OpeningLocation,
    string Objective,
    ulong Seed)
{
    public PlayerAiArchetype ParsedPlayerArchetype =>
        Enum.Parse<PlayerAiArchetype>(PlayerArchetype, ignoreCase: true);

    public void ValidateReferences(
        CharacterCatalogDefinition characters,
        HotelWorldDefinition hotel)
    {
        ArgumentNullException.ThrowIfNull(characters);
        ArgumentNullException.ThrowIfNull(hotel);

        _ = characters.GetCharacter(HumanHost);
        _ = characters.GetCharacter(HiddenPlayer);
        _ = characters.GetCharacter(IncidentCulprit);

        if (!hotel.Locations.Any(location => location.Id == OpeningLocation))
        {
            throw new FormatException(
                $"Opening location '{OpeningLocation}' does not exist in the hotel definition.");
        }
    }
}

public static class PlayableCaseDefinitionParser
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static PlayableCaseDefinition Parse(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        PlayableCaseDefinition definition = JsonSerializer.Deserialize<PlayableCaseDefinition>(
            json,
            JsonOptions) ?? throw new FormatException("Playable case definition cannot be null.");
        Validate(definition);
        return definition;
    }

    private static void Validate(PlayableCaseDefinition definition)
    {
        if (definition.SchemaVersion != 1)
        {
            throw new FormatException(
                $"Unsupported playable case schema version '{definition.SchemaVersion}'.");
        }

        ValidateText(definition.CaseId, "Case ID");
        ValidateText(definition.Title, "Case title");
        ValidateText(definition.HumanHost, "Human host");
        ValidateText(definition.HiddenPlayer, "Hidden player");
        ValidateText(definition.IncidentCulprit, "Incident culprit");
        ValidateText(definition.PlayerArchetype, "Player archetype");
        ValidateText(definition.OpeningLocation, "Opening location");
        ValidateText(definition.Objective, "Case objective");

        if (!Enum.TryParse(definition.PlayerArchetype, ignoreCase: true, out PlayerAiArchetype _))
        {
            throw new FormatException(
                $"Unknown player archetype '{definition.PlayerArchetype}'.");
        }
    }

    private static void ValidateText(string value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new FormatException($"{fieldName} cannot be empty.");
        }
    }
}
