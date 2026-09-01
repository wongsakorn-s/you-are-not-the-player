using System.Text.Json;

namespace Game.Client.Godot.Configuration;

public sealed record CharacterCatalogDefinition(
    int SchemaVersion,
    CharacterDefinition[] Characters)
{
    public CharacterDefinition GetCharacter(string id) =>
        Characters.SingleOrDefault(character => character.Id == id) ??
        throw new KeyNotFoundException($"Character '{id}' does not exist in the catalog.");
}

public sealed record CharacterDefinition(
    string Id,
    string DisplayName,
    string Role,
    string Description,
    string Color,
    string PortraitKey);

public static class CharacterCatalogDefinitionParser
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static CharacterCatalogDefinition Parse(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        CharacterCatalogDefinition catalog = JsonSerializer.Deserialize<CharacterCatalogDefinition>(
            json,
            JsonOptions) ?? throw new FormatException("Character catalog cannot be null.");
        Validate(catalog);
        return catalog;
    }

    private static void Validate(CharacterCatalogDefinition catalog)
    {
        if (catalog.SchemaVersion != 1)
        {
            throw new FormatException(
                $"Unsupported character catalog schema version '{catalog.SchemaVersion}'.");
        }

        if (catalog.Characters is not { Length: > 0 })
        {
            throw new FormatException("Character catalog must contain at least one character.");
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (CharacterDefinition character in catalog.Characters)
        {
            ValidateText(character.Id, "Character ID");
            ValidateText(character.DisplayName, $"Display name for character '{character.Id}'");
            ValidateText(character.Role, $"Role for character '{character.Id}'");
            ValidateText(character.Description, $"Description for character '{character.Id}'");
            ValidateText(character.Color, $"Color for character '{character.Id}'");
            ValidateText(character.PortraitKey, $"Portrait key for character '{character.Id}'");
            if (!ids.Add(character.Id))
            {
                throw new FormatException($"Duplicate character '{character.Id}'.");
            }

            if (!IsValidHtmlColor(character.Color))
            {
                throw new FormatException(
                    $"Color for character '{character.Id}' is not a valid HTML color.");
            }
        }
    }

    private static bool IsValidHtmlColor(string value)
    {
        ReadOnlySpan<char> color = value.AsSpan();
        if (!color.IsEmpty && color[0] == '#')
        {
            color = color[1..];
        }

        return color.Length is 6 or 8 && color.ToArray().All(Uri.IsHexDigit);
    }

    private static void ValidateText(string value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new FormatException($"{fieldName} cannot be empty.");
        }
    }
}
