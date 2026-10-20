using System.Text.Json;

namespace CityTycoon.Engine.Content;

// These "records" are plain data shapes that match the JSON files in /content.
// A record is a class whose main job is holding data. C# writes the
// constructor, equality checks and ToString() for you.

/// <summary>What kind of space a board square is.</summary>
public enum SpaceKind { Go, Jail, CityPark, GoToJail, Property, Event, Opportunity, Tax, Landmark, GovernmentTile }

public enum PropertyType { Color, Railroad, Utility }

public enum TaxType { Income, Luxury }

public enum LandmarkType { Hospital, Casino, Mall, Stadium }

/// <summary>board.json: the 4 corners and each district's fixed spaces, in order (Section 4).</summary>
public sealed record BoardDefinition(
    int ContentVersion,
    IReadOnlyList<SpaceDefinition> Corners,
    IReadOnlyList<DistrictDefinition> Districts);

public sealed record DistrictDefinition(string Id, string Name, IReadOnlyList<SpaceDefinition> Spaces);

/// <summary>
/// One fixed space. For property spaces the <see cref="Id"/> is the property's ID
/// and the name comes from properties.json, so it only has to be written once.
/// </summary>
public sealed record SpaceDefinition(
    string Id,
    SpaceKind Kind,
    string? Name = null,
    TaxType? Tax = null,
    LandmarkType? Landmark = null);

/// <summary>properties.json (Section 8.7).</summary>
public sealed record PropertiesFile(int ContentVersion, IReadOnlyList<PropertyDefinition> Properties);

public sealed record PropertyDefinition(
    string Id,
    string Name,
    PropertyType Type,
    int AssessedValue,
    string? Group = null,
    int HouseCost = 0,
    IReadOnlyList<int>? Rents = null);

/// <summary>groups.json: color groups and their perks (Section 13.2).</summary>
public sealed record GroupsFile(int ContentVersion, IReadOnlyList<GroupDefinition> Groups);

/// <param name="Color">A hex color for drawing, like "#8B5A2B".</param>
/// <param name="Code">A short text code (like "BR") so the group can be told apart without color.</param>
public sealed record GroupDefinition(string Id, string Name, string Color, string Code, PerkDefinition Perk);

/// <param name="Modifiers">The rule changes the perk makes (see ModifierTypes.cs). A perk needs at least one.</param>
public sealed record PerkDefinition(string Id, string Name, string Text, IReadOnlyList<ModifierSpec>? Modifiers = null);

/// <summary>
/// One rule change, as written in a content file: the "type" names a modifier
/// class (listed in ModifierTypes.cs), and "value" or "card" are its settings.
/// For example <c>{ "type": "GoCash", "value": 50 }</c> adds $50 when passing GO.
/// </summary>
public sealed record ModifierSpec(string Type, int Value = 0, string? Card = null);

/// <summary>characters.json: the 8 characters (Section 5).</summary>
public sealed record CharactersFile(int ContentVersion, IReadOnlyList<CharacterDefinition> Characters);

public sealed record CharacterDefinition(string Id, string Name, string Text, IReadOnlyList<ModifierSpec>? Modifiers = null);

/// <summary>bonuses.json: the railroad and utility bonuses (Sections 13.3 and 13.4).</summary>
public sealed record BonusesFile(int ContentVersion, IReadOnlyList<BonusDefinition> Bonuses);

/// <param name="PropertyType">Which kind of property counts: railroad or utility.</param>
/// <param name="Count">How many of that kind you need to own (outright and not mortgaged) to switch the bonus on.</param>
public sealed record BonusDefinition(string Id, string Name, string Text, PropertyType PropertyType, int Count,
    IReadOnlyList<ModifierSpec>? Modifiers = null);

/// <summary>govtiles.json: the 7 Government tiles (Section 12).</summary>
public sealed record GovTilesFile(int ContentVersion, int TilesPerGame, IReadOnlyList<GovTileDefinition> Tiles);

/// <summary>
/// One Government tile. <see cref="Neighbor"/> is what it does to the properties
/// either side of it once revealed; <see cref="Landing"/> names a landing effect
/// type (a small C# class in Tiles/TileLandings.cs) plus its settings.
/// </summary>
public sealed record GovTileDefinition(string Id, string Name, string NeighborText, string LandingText,
    TileNeighborEffect? Neighbor = null, JsonElement Landing = default)
{
    /// <summary>The landing effect's type name, like "PayBank".</summary>
    public string LandingType => Landing.ValueKind == JsonValueKind.Object && Landing.TryGetProperty("type", out JsonElement t) ? t.GetString() ?? "" : "";

    /// <summary>A whole-number landing setting, like "amount".</summary>
    public int Int(string name, int? fallback = null) =>
        Landing.ValueKind == JsonValueKind.Object && Landing.TryGetProperty(name, out JsonElement v) && v.TryGetInt32(out int i) ? i
        : fallback ?? throw new ContentException($"Government tile '{Id}' needs a whole number \"{name}\" in its landing effect.");

    public string Setting(string name) =>
        Landing.ValueKind == JsonValueKind.Object && Landing.TryGetProperty(name, out JsonElement v) && v.ValueKind == JsonValueKind.String ? v.GetString()!
        : throw new ContentException($"Government tile '{Id}' needs a text setting \"{name}\" in its landing effect.");

    public bool Flag(string name) =>
        Landing.ValueKind == JsonValueKind.Object && Landing.TryGetProperty(name, out JsonElement v) && v.ValueKind == JsonValueKind.True;
}

/// <summary>What a revealed tile does to its two neighbors (Section 12.3). Anything left out does nothing.</summary>
public sealed record TileNeighborEffect(
    int RentPercent = 0,
    int BuildDiscountPercent = 0,
    bool ProtectFromCards = false,
    bool ProtectFromFire = false,
    int TransitHopCost = 0);
