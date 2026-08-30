using System.Text.Json;
using Game.Sim.Events;
using Game.Sim.Memory;

namespace Game.Sim.Suspicion;

public static class JsonSuspicionRuleParser
{
    private static readonly string[] RuleProperties = ["id", "match", "effects"];
    private static readonly string[] MatchProperties = ["event", "requiredTags", "memoryKind", "pattern"];

    public static InMemorySuspicionRuleRepository Parse(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidDataException("Suspicion rule JSON root must be an array.");
            }

            var rules = new List<SuspicionRule>();
            foreach (JsonElement element in document.RootElement.EnumerateArray())
            {
                rules.Add(ParseRule(element));
            }

            return new InMemorySuspicionRuleRepository(rules);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("Suspicion rule JSON is malformed.", exception);
        }
        catch (ArgumentException exception)
        {
            throw new InvalidDataException("Suspicion rule JSON contains invalid data.", exception);
        }
    }

    private static SuspicionRule ParseRule(JsonElement element)
    {
        EnsureObject(element, "rule");
        ValidateProperties(element, RuleProperties, "rule");

        string id = GetRequiredString(element, "id", "rule");
        JsonElement match = GetRequiredProperty(element, "match", JsonValueKind.Object, "rule");
        ValidateProperties(match, MatchProperties, $"rule '{id}' match");

        EventType eventType = ParseEnum<EventType>(
            GetRequiredString(match, "event", $"rule '{id}' match"),
            $"rule '{id}' event");
        MemoryKind? memoryKind = match.TryGetProperty("memoryKind", out JsonElement memoryKindElement)
            ? ParseEnum<MemoryKind>(
                GetString(memoryKindElement, $"rule '{id}' memoryKind"),
                $"rule '{id}' memoryKind")
            : null;
        BehaviorPatternKind? behaviorPattern = match.TryGetProperty(
            "pattern",
            out JsonElement patternElement)
            ? ParseEnum<BehaviorPatternKind>(
                GetString(patternElement, $"rule '{id}' pattern"),
                $"rule '{id}' pattern")
            : null;
        EventTag[] requiredTags = ParseRequiredTags(match, id);
        List<SuspicionEffect> effects = ParseEffects(element, id);

        return new SuspicionRule(
            id,
            eventType,
            requiredTags,
            memoryKind,
            effects,
            behaviorPattern);
    }

    private static EventTag[] ParseRequiredTags(JsonElement match, string ruleId)
    {
        if (!match.TryGetProperty("requiredTags", out JsonElement tagsElement))
        {
            return [];
        }

        if (tagsElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException($"Rule '{ruleId}' requiredTags must be an array.");
        }

        return tagsElement
            .EnumerateArray()
            .Select((tag, index) => ParseEnum<EventTag>(
                GetString(tag, $"rule '{ruleId}' requiredTags[{index}]"),
                $"rule '{ruleId}' requiredTags[{index}]"))
            .ToArray();
    }

    private static List<SuspicionEffect> ParseEffects(JsonElement rule, string ruleId)
    {
        JsonElement effectsElement = GetRequiredProperty(
            rule,
            "effects",
            JsonValueKind.Object,
            $"rule '{ruleId}'");
        var effects = new List<SuspicionEffect>();
        var dimensions = new HashSet<SuspicionDimension>();

        foreach (JsonProperty property in effectsElement.EnumerateObject())
        {
            SuspicionDimension dimension = ParseEnum<SuspicionDimension>(
                property.Name,
                $"rule '{ruleId}' effect '{property.Name}'");
            if (!dimensions.Add(dimension))
            {
                throw new InvalidDataException(
                    $"Rule '{ruleId}' defines effect '{dimension}' more than once.");
            }

            if (property.Value.ValueKind != JsonValueKind.Number ||
                !property.Value.TryGetSingle(out float strength))
            {
                throw new InvalidDataException(
                    $"Rule '{ruleId}' effect '{property.Name}' must be a number.");
            }

            effects.Add(new SuspicionEffect(dimension, strength));
        }

        return effects;
    }

    private static TEnum ParseEnum<TEnum>(string value, string context)
        where TEnum : struct, Enum
    {
        if (!Enum.TryParse(value, ignoreCase: true, out TEnum parsed) || !Enum.IsDefined(parsed))
        {
            throw new InvalidDataException($"{context} has unknown value '{value}'.");
        }

        return parsed;
    }

    private static JsonElement GetRequiredProperty(
        JsonElement element,
        string propertyName,
        JsonValueKind expectedKind,
        string context)
    {
        if (!element.TryGetProperty(propertyName, out JsonElement property) ||
            property.ValueKind != expectedKind)
        {
            throw new InvalidDataException(
                $"{context} property '{propertyName}' must be {expectedKind}.");
        }

        return property;
    }

    private static string GetRequiredString(
        JsonElement element,
        string propertyName,
        string context) =>
        GetString(
            GetRequiredProperty(element, propertyName, JsonValueKind.String, context),
            $"{context} property '{propertyName}'");

    private static string GetString(JsonElement element, string context)
    {
        if (element.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(element.GetString()))
        {
            throw new InvalidDataException($"{context} must be a non-empty string.");
        }

        return element.GetString()!;
    }

    private static void EnsureObject(JsonElement element, string context)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException($"Suspicion {context} must be an object.");
        }
    }

    private static void ValidateProperties(
        JsonElement element,
        IReadOnlyCollection<string> allowedProperties,
        string context)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (!seen.Add(property.Name))
            {
                throw new InvalidDataException(
                    $"Suspicion {context} contains duplicate property '{property.Name}'.");
            }

            if (!allowedProperties.Contains(property.Name, StringComparer.Ordinal))
            {
                throw new InvalidDataException(
                    $"Suspicion {context} contains unknown property '{property.Name}'.");
            }
        }
    }
}
