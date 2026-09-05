using System.Text.Json;
using System.Text.Json.Serialization;

namespace Game.Client.Godot.Configuration;

public sealed record DialogueCatalogDefinition(
    int SchemaVersion,
    Dictionary<string, DialogueCharacterLines> Characters)
{
    public DialogueCharacterLines GetLines(string characterId) =>
        Characters.TryGetValue(characterId, out DialogueCharacterLines? lines)
            ? lines
            : throw new KeyNotFoundException($"Dialogue for character '{characterId}' does not exist.");
}

public sealed record DialogueCharacterLines(
    string Schedule,
    string AskAboutSubject,
    string AskAboutSubjectNoMemory,
    [property: JsonPropertyName("object")] string ObjectLine,
    string Confront,
    [property: JsonPropertyName("thai")] DialogueCharacterLines? Thai = null);

public static class DialogueCatalogDefinitionParser
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static DialogueCatalogDefinition Parse(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        DialogueCatalogDefinition catalog = JsonSerializer.Deserialize<DialogueCatalogDefinition>(
            json,
            JsonOptions) ?? throw new FormatException("Dialogue catalog cannot be null.");
        Validate(catalog);
        return catalog;
    }

    private static void Validate(DialogueCatalogDefinition catalog)
    {
        if (catalog.SchemaVersion != 1)
        {
            throw new FormatException(
                $"Unsupported dialogue catalog schema version '{catalog.SchemaVersion}'.");
        }

        if (catalog.Characters is not { Count: > 0 })
        {
            throw new FormatException("Dialogue catalog must contain at least one character.");
        }

        foreach ((string id, DialogueCharacterLines? lines) in catalog.Characters)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new FormatException("Dialogue character ID cannot be empty.");
            }

            if (lines is null)
            {
                throw new FormatException($"Dialogue lines for '{id}' cannot be null.");
            }

            ValidateLines(lines, id);
            if (lines.Thai is not null)
            {
                ValidateLines(lines.Thai, $"{id}.thai");
            }
        }
    }

    private static void ValidateLines(DialogueCharacterLines lines, string id)
    {
        ValidateText(lines.Schedule, id, nameof(lines.Schedule));
        ValidateText(lines.AskAboutSubject, id, nameof(lines.AskAboutSubject));
        ValidateText(lines.AskAboutSubjectNoMemory, id, nameof(lines.AskAboutSubjectNoMemory));
        ValidateText(lines.ObjectLine, id, nameof(lines.ObjectLine));
        ValidateText(lines.Confront, id, nameof(lines.Confront));
    }

    private static void ValidateText(string value, string characterId, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new FormatException($"Dialogue '{fieldName}' for '{characterId}' cannot be empty.");
        }
    }
}
