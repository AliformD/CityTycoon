using CityTycoon.Engine.Cards;
using CityTycoon.Engine.Modifiers;
using CityTycoon.Engine.Tiles;

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

    /// <summary>The characters players can pick (Section 5), in file order.</summary>
    public IReadOnlyList<CharacterDefinition> Characters { get; }

    /// <summary>The railroad and utility bonuses (Sections 13.3 and 13.4).</summary>
    public IReadOnlyList<BonusDefinition> Bonuses { get; }

    /// <summary>Every Opportunity and Event card, keyed by card ID (Section 21.2).</summary>
    public IReadOnlyDictionary<string, CardDefinition> Cards { get; }

    private readonly IReadOnlyList<CardsFile> cardFiles;
    private readonly Dictionary<string, GovTileDefinition> govTilesById;
    private readonly Dictionary<string, CharacterDefinition> charactersById;
    private readonly Dictionary<string, int> groupSizes;

    public GameContent(RulesConfig rules, BoardDefinition board, PropertiesFile properties, GroupsFile groups, GovTilesFile govTiles,
        IReadOnlyList<CardsFile>? cards = null, CharactersFile? characters = null, BonusesFile? bonuses = null)
    {
        cardFiles = cards ?? [];
        Cards = ToDictionary(cardFiles.SelectMany(f => f.Cards), c => c.Id, "card");
        Rules = rules;
        Board = board;
        Properties = ToDictionary(properties.Properties, p => p.Id, "property");
        Groups = ToDictionary(groups.Groups, g => g.Id, "group");
        GovTiles = govTiles.Tiles;
        GovTilesPerGame = govTiles.TilesPerGame;
        govTilesById = ToDictionary(GovTiles, t => t.Id, "Government tile");
        Characters = characters?.Characters ?? [];
        charactersById = ToDictionary(Characters, c => c.Id, "character");
        Bonuses = bonuses?.Bonuses ?? [];
        ToDictionary(Bonuses, b => b.Id, "bonus");
        groupSizes = Properties.Values.Where(p => p.Group != null).GroupBy(p => p.Group!).ToDictionary(g => g.Key, g => g.Count());
        Validate();
    }

    public static GameContent LoadFromFiles() => new(
        ContentLoader.LoadRules(),
        ContentLoader.Load<BoardDefinition>("board.json"),
        ContentLoader.Load<PropertiesFile>("properties.json"),
        ContentLoader.Load<GroupsFile>("groups.json"),
        ContentLoader.Load<GovTilesFile>("govtiles.json"),
        ContentLoader.LoadCardFiles(),
        ContentLoader.Load<CharactersFile>("characters.json"),
        ContentLoader.Load<BonusesFile>("bonuses.json"));

    /// <summary>
    /// A copy of this content with extra card files added, the way an expansion
    /// would be (Section 21.2). The new cards are checked like the built-in ones.
    /// </summary>
    public GameContent WithExtraCards(params CardsFile[] extra) => new(
        Rules, Board,
        new PropertiesFile(1, Properties.Values.ToList()),
        new GroupsFile(1, Groups.Values.ToList()),
        new GovTilesFile(1, GovTilesPerGame, GovTiles),
        [.. cardFiles, .. extra],
        new CharactersFile(1, Characters),
        new BonusesFile(1, Bonuses));

    public CardDefinition Card(string id) =>
        Cards.TryGetValue(id, out var c) ? c : throw new KeyNotFoundException($"Unknown card '{id}'.");

    public PropertyDefinition Property(string id) =>
        Properties.TryGetValue(id, out var p) ? p : throw new KeyNotFoundException($"Unknown property '{id}'.");

    public CharacterDefinition Character(string id) =>
        charactersById.TryGetValue(id, out var c) ? c : throw new KeyNotFoundException($"Unknown character '{id}'.");

    /// <summary>How many properties are in a color group.</summary>
    public int GroupSize(string groupId) => groupSizes[groupId];

    /// <summary>
    /// The skyscraper price as a multiple of the house cost for this property. Normally the rule from
    /// rules.json, but a color perk can change it (Dark Blue's Prime Real Estate). Because only the
    /// owner of the whole group can ever build there, the perk is always on when it matters,
    /// and buying and selling always use the same price.
    /// </summary>
    public int SkyscraperCostMultiplier(PropertyDefinition def) =>
        def.Group != null
            ? Groups[def.Group].Perk.Modifiers?.FirstOrDefault(m => m.Type == "SkyscraperCostMultiplier")?.Value ?? Rules.SkyscraperCostMultiplier
            : Rules.SkyscraperCostMultiplier;

    public GovTileDefinition GovTile(string id) =>
        govTilesById.TryGetValue(id, out var t) ? t : throw new KeyNotFoundException($"Unknown Government tile '{id}'.");

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

        // Government tiles: unique IDs (checked when the lookup was built), and enough of them for one per district.
        if (GovTilesPerGame != Board.Districts.Count || GovTiles.Count < GovTilesPerGame)
            throw new ContentException("govtiles.json needs one tile per district, picked from at least that many tiles.");
        foreach (GovTileDefinition tile in GovTiles)
        {
            if (TileLandings.Find(tile.LandingType) is not { } landing)
                throw new ContentException($"Government tile '{tile.Id}' uses the unknown landing type '{tile.LandingType}'.");
            landing.Validate(tile);
        }

        // Cards: a known effect type with the settings it needs, and a timing that fits the deck.
        foreach (CardDefinition card in Cards.Values)
        {
            if (card.Copies < 1)
                throw new ContentException($"Card '{card.Id}' needs at least 1 copy.");
            if (CardEffects.Find(card.EffectType) is not { } effect)
                throw new ContentException($"Card '{card.Id}' uses the unknown effect type '{card.EffectType}'.");
            if (card.Deck == CardDeck.Event && card.Timing != CardTiming.Immediate)
                throw new ContentException($"Event card '{card.Id}' must happen immediately (no \"timing\").");
            if (card.Deck == CardDeck.Opportunity && card.Timing == CardTiming.Immediate)
                throw new ContentException($"Opportunity card '{card.Id}' needs a \"timing\" (ownTurn, reaction or council).");
            if ((card.Timing == CardTiming.Reaction) != (card.Trigger != CardTrigger.None))
                throw new ContentException($"Card '{card.Id}': reaction cards need a \"trigger\", and only reaction cards have one.");
            effect.Validate(card);
            if (card.Deck == CardDeck.Opportunity && card.Polarity == null)
                throw new ContentException($"Opportunity card '{card.Id}' needs a \"polarity\": \"helpful\" or \"negative\".");
        }
        if (Rules.MallPrices.Count == 0)
            throw new ContentException("rules.json mallPrices needs at least one slot.");

        // Color perks, railroad/utility bonuses and characters: every modifier is a known type with the settings it needs.
        foreach (GroupDefinition group in Groups.Values)
        {
            if (group.Perk.Modifiers is not { Count: > 0 })
                throw new ContentException($"The perk of group '{group.Id}' needs at least one modifier.");
            foreach (ModifierSpec spec in group.Perk.Modifiers)
                ModifierTypes.Validate(spec, $"The perk of group '{group.Id}'", this);
        }
        foreach (BonusDefinition bonus in Bonuses)
        {
            if (bonus.PropertyType == PropertyType.Color)
                throw new ContentException($"Bonus '{bonus.Id}' must be for railroads or utilities.");
            int available = Properties.Values.Count(p => p.Type == bonus.PropertyType);
            if (bonus.Count < 1 || bonus.Count > available)
                throw new ContentException($"Bonus '{bonus.Id}' needs a count between 1 and {available}.");
            if (bonus.Modifiers is not { Count: > 0 })
                throw new ContentException($"Bonus '{bonus.Id}' needs at least one modifier.");
            foreach (ModifierSpec spec in bonus.Modifiers)
                ModifierTypes.Validate(spec, $"Bonus '{bonus.Id}'", this);
        }
        foreach (CharacterDefinition character in Characters)
        {
            if (character.Modifiers is not { Count: > 0 })
                throw new ContentException($"Character '{character.Id}' needs at least one modifier.");
            foreach (ModifierSpec spec in character.Modifiers)
                ModifierTypes.Validate(spec, $"Character '{character.Id}'", this);
        }
        if (Characters.Count > 0 && Characters.Count < Rules.MaxPlayers)
            throw new ContentException($"characters.json needs at least {Rules.MaxPlayers} characters, so every player can have a different one.");
    }
}
