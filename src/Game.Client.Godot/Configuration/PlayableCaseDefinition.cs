using System.Text.Json;
using Game.Sim.PlayerAi;

namespace Game.Client.Godot.Configuration;

public sealed record PlayableCaseDefinition(
    int SchemaVersion,
    string CaseId,
    string Title,
    string HumanHost,
    string? HiddenPlayer,
    string? IncidentCulprit,
    string? PlayerArchetype,
    string OpeningLocation,
    string Objective,
    ulong Seed)
{
    /// <summary>
    /// Content overrides for the hidden truth. Anything left null is chosen by the
    /// seed instead, which is what makes two runs pose different questions. The
    /// first playable case pins only the culprit, because its ending prose names
    /// who opened the basement door.
    /// </summary>
    public PlayerAiArchetype? ParsedPlayerArchetype => PlayerArchetype is null
        ? null
        : Enum.Parse<PlayerAiArchetype>(PlayerArchetype, ignoreCase: true);

    public void ValidateReferences(
        CharacterCatalogDefinition characters,
        HotelWorldDefinition hotel)
    {
        ArgumentNullException.ThrowIfNull(characters);
        ArgumentNullException.ThrowIfNull(hotel);

        _ = characters.GetCharacter(HumanHost);
        if (HiddenPlayer is not null)
        {
            _ = characters.GetCharacter(HiddenPlayer);
        }

        if (IncidentCulprit is not null)
        {
            _ = characters.GetCharacter(IncidentCulprit);
        }

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
        ValidateOptionalText(definition.HiddenPlayer, "Hidden player");
        ValidateOptionalText(definition.IncidentCulprit, "Incident culprit");
        ValidateOptionalText(definition.PlayerArchetype, "Player archetype");
        ValidateText(definition.OpeningLocation, "Opening location");
        ValidateText(definition.Objective, "Case objective");

        if (definition.PlayerArchetype is { } archetype &&
            !Enum.TryParse(archetype, ignoreCase: true, out PlayerAiArchetype _))
        {
            throw new FormatException($"Unknown player archetype '{archetype}'.");
        }
    }

    // Absent means "let the seed decide"; present but blank is a content mistake.
    private static void ValidateOptionalText(string? value, string fieldName)
    {
        if (value is not null)
        {
            ValidateText(value, fieldName);
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
