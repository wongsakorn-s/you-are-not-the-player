using System.Text.Json;
using Game.Sim.Locations;
using Godot;

namespace Game.Client.Godot.Configuration;

public sealed record HotelWorldDefinition(
    int SchemaVersion,
    NavigationSurfaceDefinition Navigation,
    HotelLocationDefinition[] Locations,
    HotelPortalDefinition[] Portals)
{
    public LocationGraph CreateLocationGraph()
    {
        var graph = new LocationGraph();
        foreach (HotelLocationDefinition location in Locations)
        {
            graph.AddLocation(new LocationId(location.Id));
        }

        foreach (HotelPortalDefinition portal in Portals)
        {
            graph.ConnectBidirectional(
                new LocationId(portal.From),
                new LocationId(portal.To),
                portal.Id,
                portal.RequiresAccess);
        }

        return graph;
    }
}

public sealed record NavigationSurfaceDefinition(
    float MinimumX,
    float MaximumX,
    float MinimumZ,
    float MaximumZ,
    float Height);

public sealed record HotelLocationDefinition(
    string Id,
    string DisplayName,
    WorldPoint Marker,
    WorldPoint FloorPosition,
    WorldSize FloorSize,
    string Color,
    bool Restricted,
    string? DisplayNameThai = null,
    string? ProseName = null,
    string? ProseNameThai = null)
{
    /// <summary>The map label, in the language being read.</summary>
    public string LabelIn(bool thai) =>
        thai && !string.IsNullOrWhiteSpace(DisplayNameThai) ? DisplayNameThai : DisplayName;

    /// <summary>
    /// The room's name as it belongs in a sentence.
    /// </summary>
    /// <remarks>
    /// Map labels are shouted - HOTEL LOBBY, MAIN HALLWAY - which is right on a
    /// floor plan and wrong in "Clara is going to MAIN HALLWAY".
    /// </remarks>
    public string ProseIn(bool thai) => thai
        ? !string.IsNullOrWhiteSpace(ProseNameThai) ? ProseNameThai : LabelIn(thai: true)
        : !string.IsNullOrWhiteSpace(ProseName) ? ProseName : DisplayName;
}

public sealed record HotelPortalDefinition(
    string Id,
    string From,
    string To,
    bool RequiresAccess,
    HotelDoorDefinition? Door);

public sealed record HotelDoorDefinition(WorldPoint Position, WorldSize Size, string Color);

public sealed record WorldPoint(float X, float Y, float Z)
{
    public Vector3 ToVector3() => new(X, Y, Z);
}

public sealed record WorldSize(float X, float Y, float Z)
{
    public Vector3 ToVector3() => new(X, Y, Z);
}

public static class HotelWorldDefinitionParser
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static HotelWorldDefinition Parse(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        HotelWorldDefinition definition = JsonSerializer.Deserialize<HotelWorldDefinition>(
            json,
            JsonOptions) ?? throw new FormatException("Hotel world definition cannot be null.");
        Validate(definition);
        return definition;
    }

    private static void Validate(HotelWorldDefinition definition)
    {
        if (definition.SchemaVersion != 1)
        {
            throw new FormatException(
                $"Unsupported hotel world schema version '{definition.SchemaVersion}'.");
        }

        if (definition.Locations is not { Length: > 0 })
        {
            throw new FormatException("Hotel world must contain at least one location.");
        }

        if (definition.Portals is null)
        {
            throw new FormatException("Hotel world portals cannot be null.");
        }

        var locationIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (HotelLocationDefinition location in definition.Locations)
        {
            ValidateText(location.Id, "Location ID");
            ValidateText(location.DisplayName, $"Display name for location '{location.Id}'");
            ValidateText(location.Color, $"Color for location '{location.Id}'");
            if (!locationIds.Add(location.Id))
            {
                throw new FormatException($"Duplicate hotel location '{location.Id}'.");
            }

            ValidateSize(location.FloorSize, $"Floor size for location '{location.Id}'");
        }

        var portalIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (HotelPortalDefinition portal in definition.Portals)
        {
            ValidateText(portal.Id, "Portal ID");
            if (!portalIds.Add(portal.Id))
            {
                throw new FormatException($"Duplicate hotel portal '{portal.Id}'.");
            }

            if (!locationIds.Contains(portal.From) || !locationIds.Contains(portal.To))
            {
                throw new FormatException(
                    $"Portal '{portal.Id}' references an unknown location.");
            }

            if (portal.Door is not null)
            {
                ValidateSize(portal.Door.Size, $"Door size for portal '{portal.Id}'");
                ValidateText(portal.Door.Color, $"Door color for portal '{portal.Id}'");
            }
        }

        NavigationSurfaceDefinition navigation = definition.Navigation ??
            throw new FormatException("Hotel navigation surface cannot be null.");
        if (navigation.MinimumX >= navigation.MaximumX ||
            navigation.MinimumZ >= navigation.MaximumZ)
        {
            throw new FormatException("Hotel navigation bounds are invalid.");
        }
    }

    private static void ValidateSize(WorldSize size, string fieldName)
    {
        if (size is null || size.X <= 0.0f || size.Y <= 0.0f || size.Z <= 0.0f)
        {
            throw new FormatException($"{fieldName} must be positive on every axis.");
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
