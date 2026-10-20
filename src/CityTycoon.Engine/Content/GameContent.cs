namespace CityTycoon.Engine.Content;

/// <summary>
/// All of the game's content (rules, board, properties, groups, tiles) in one
/// place, checked for mistakes when it loads. Content never changes during a
/// game, so one copy is shared by every game.
/// </summary>
public sealed class GameContent
{
    private static readonly Lazy<GameContent> DefaultContent = new(LoadFromFiles);

    /// <summary>The content from the /content folder. Loaded once, then reused.</summary>
    public static GameContent Default => DefaultContent.Value;

    public RulesConfig Rules { get; }
    public BoardDefinition Board { get; }
    public IReadOnlyDictionary<string, PropertyDefinition> Properties { get; }
    public IReadOnlyDictionary<string, GroupDefinition> Groups { get; }
    public IReadOnlyList<GovTileDefinition> GovTiles { get; }
    public int GovTilesPerGame { get; }

    public GameContent(RulesConfig rules, BoardDefinition board, PropertiesFile properties, GroupsFile groups, GovTilesFile govTiles)
    {
        Rules = rules;
        Board = board;
        Properties = ToDictionary(properties.Properties, p => p.Id, "property");
        Groups = ToDictionary(groups.Groups, g => g.Id, "group");
        GovTiles = govTiles.Tiles;
        GovTilesPerGame = govTiles.TilesPerGame;
        Validate();
    }

    public static GameContent LoadFromFiles() => new(
        ContentLoader.LoadRules(),
        ContentLoader.Load<BoardDefinition>("board.json"),
        ContentLoader.Load<PropertiesFile>("properties.json"),
        ContentLoader.Load<GroupsFile>("groups.json"),
        ContentLoader.Load<GovTilesFile>("govtiles.json"));

    public PropertyDefinition Property(string id) =>
        Properties.TryGetValue(id, out var p) ? p : throw new KeyNotFoundException($"Unknown property '{id}'.");

    public GovTileDefinition GovTile(string id) =>
        GovTiles.FirstOrDefault(t => t.Id == id) ?? throw new KeyNotFoundException($"Unknown Government tile '{id}'.");

    /// <summary>The IDs of every property in a color group.</summary>
    public IEnumerable<string> GroupMembers(string groupId) =>
        Properties.Values.Where(p => p.Group == groupId).Select(p => p.Id);

    /// <summary>Total number of board spaces once one Government tile is added per district.</summary>
    public int BoardSize => Board.Corners.Count + Board.Districts.Sum(d => d.Spaces.Count + 1);

    private static Dictionary<string, T> ToDictionary<T>(IEnumerable<T> items, Func<T, string> id, string what)
    {
        var result = new Dictionary<string, T>();
        foreach (T item in items)
        {
            if (!result.TryAdd(id(item), item))
                throw new ContentException($"Two {what} entries share the ID '{id(item)}'.");
        }
        return result;
    }

    /// <summary>
    /// Checks the content files agree with each other, so a typo in a JSON
    /// file is caught when the game starts instead of halfway through a game.
    /// </summary>
    private void Validate()
    {
        // Corners: exactly GO, Jail, City Park, Go To Jail, in that order (Section 4.1).
        SpaceKind[] cornerKinds = [SpaceKind.Go, SpaceKind.Jail, SpaceKind.CityPark, SpaceKind.GoToJail];
        if (!Board.Corners.Select(c => c.Kind).SequenceEqual(cornerKinds))
            throw new ContentException("board.json corners must be go, jail, cityPark, goToJail in that order.");
        if (Board.Districts.Count != Board.Corners.Count)
            throw new ContentException("board.json needs one district per side of the board (one per corner).");

        // Every space ID is unique.
        var allSpaces = Board.Corners.Concat(Board.Districts.SelectMany(d => d.Spaces)).ToList();
        var duplicate = allSpaces.GroupBy(s => s.Id).FirstOrDefault(g => g.Count() > 1);
        if (duplicate != null)
            throw new ContentException($"board.json uses the space ID '{duplicate.Key}' more than once.");

        foreach (SpaceDefinition space in allSpaces)
        {
            if (space.Kind == SpaceKind.Property && !Properties.ContainsKey(space.Id))
                throw new ContentException($"board.json space '{space.Id}' is a property, but properties.json has no such property.");
            if (space.Kind != SpaceKind.Property && string.IsNullOrWhiteSpace(space.Name))
                throw new ContentException($"board.json space '{space.Id}' needs a name.");
            if (space.Kind == SpaceKind.Tax && space.Tax == null)
                throw new ContentException($"board.json tax space '{space.Id}' needs a \"tax\" type.");
            if (space.Kind == SpaceKind.Landmark && space.Landmark == null)
                throw new ContentException($"board.json landmark '{space.Id}' needs a \"landmark\" type.");
            if (space.Kind == SpaceKind.GovernmentTile)
                throw new ContentException("Government tiles are placed by the board generator, not listed in board.json.");
        }

        // Every property is on the board exactly once.
        foreach (string id in Properties.Keys)
        {
            int count = allSpaces.Count(s => s.Kind == SpaceKind.Property && s.Id == id);
            if (count != 1)
                throw new ContentException($"Property '{id}' appears on the board {count} times (it should be exactly once).");
        }

        // Color properties have a valid group, a house cost and 7 rents (base, 4 houses, hotel, skyscraper).
        foreach (PropertyDefinition p in Properties.Values.Where(p => p.Type == PropertyType.Color))
        {
            if (p.Group == null || !Groups.ContainsKey(p.Group))
                throw new ContentException($"Property '{p.Id}' has unknown group '{p.Group}'.");
            if (p.Rents is not { Count: 7 })
                throw new ContentException($"Property '{p.Id}' needs exactly 7 rents.");
            if (p.HouseCost <= 0)
                throw new ContentException($"Property '{p.Id}' needs a house cost.");
        }

        // Each color group sits entirely in one district (Section 4.2), because zoning is per district.
        foreach (string groupId in Groups.Keys)
        {
            var districts = Board.Districts.Where(d => d.Spaces.Any(s => Properties.TryGetValue(s.Id, out var p) && p.Group == groupId)).ToList();
            if (districts.Count != 1)
                throw new ContentException($"Group '{groupId}' must sit inside exactly one district.");
        }

        // Railroad and utility rent tables need one entry per property of that type.
        if (Rules.RailroadRents.Count < Properties.Values.Count(p => p.Type == PropertyType.Railroad))
            throw new ContentException("rules.json railroadRents needs one entry per railroad.");
        if (Rules.UtilityRentMultipliers.Count < Properties.Values.Count(p => p.Type == PropertyType.Utility))
            throw new ContentException("rules.json utilityRentMultipliers needs one entry per utility.");

        // Government tiles: unique IDs, and enough of them for one per district.
        ToDictionary(GovTiles, t => t.Id, "Government tile");
        if (GovTilesPerGame != Board.Districts.Count || GovTiles.Count < GovTilesPerGame)
            throw new ContentException("govtiles.json needs one tile per district, picked from at least that many tiles.");
    }
}
