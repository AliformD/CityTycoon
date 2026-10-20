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

public sealed record PerkDefinition(string Id, string Name, string Text);

/// <summary>govtiles.json: the 7 Government tiles (Section 12).</summary>
public sealed record GovTilesFile(int ContentVersion, int TilesPerGame, IReadOnlyList<GovTileDefinition> Tiles);

public sealed record GovTileDefinition(string Id, string Name, string NeighborText, string LandingText);
