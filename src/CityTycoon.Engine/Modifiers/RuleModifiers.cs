using CityTycoon.Engine.Content;
using CityTycoon.Engine.State;
using CityTycoon.Engine.Tiles;

namespace CityTycoon.Engine.Modifiers;

/// <summary>
/// Gathers the active modifiers and adds up their answers. The rest of the
/// engine asks the questions here ("how many Momentum tokens can this player
/// hold?") and never looks at characters or perks itself.
/// </summary>
public static class RuleModifiers
{
    /// <summary>
    /// Every modifier in play right now: revealed Government tiles (a hidden
    /// tile has no effect at all, Section 12.1), each player's character,
    /// each color perk whose whole group a player holds, and each railroad
    /// or utility bonus a player has earned.
    /// </summary>
    public static IReadOnlyList<IRuleModifier> Active(GameState state, GameContent content)
    {
        // Working this out is the most-asked question in the engine, so the answer is kept until
        // something it depends on changes (who owns what, which tiles are revealed, who is out).
        long signature = Signature(state);
        if (state.ModifierCache is { } cache && cache.Signature == signature && ReferenceEquals(cache.Content, content))
            return cache.Modifiers;

        var active = new List<IRuleModifier>();
        foreach (BoardSpace tile in state.Board.Where(s => s.Kind == SpaceKind.GovernmentTile && s.IsRevealed))
            active.Add(new TileModifier(state, tile, content.GovTile(tile.TileId!)));

        foreach (PlayerState player in state.Players.Where(p => !p.IsBankrupt))
        {
            if (player.CharacterId is string characterId)
            {
                CharacterDefinition character = content.Character(characterId);
                foreach (ModifierSpec spec in character.Modifiers ?? [])
                    active.Add(ModifierTypes.Create(new ModifierContext(spec, player.Id, character.Name, content)));
            }
        }
        foreach (EarnedPerk perk in EarnedPerks(state, content))
        {
            foreach (ModifierSpec spec in perk.Specs)
                active.Add(ModifierTypes.Create(new ModifierContext(spec, perk.PlayerId, perk.Name, content, perk.GroupId)));
        }
        state.ModifierCache = new ModifierCache(signature, content, active);
        return active;
    }

    /// <summary>
    /// A number that changes whenever anything the active modifiers depend on changes. Checking it
    /// is much cheaper than gathering the modifiers again. (Buildings aren't included: the
    /// modifiers that care about them look at the live state when asked.)
    /// </summary>
    private static long Signature(GameState state)
    {
        long h = state.Players.Count;
        foreach (PlayerState p in state.Players)
            h = h * 1_000_003 + p.Id * 4 + (p.IsBankrupt ? 1 : 0) + (p.CharacterId?.GetHashCode() ?? 0) * 8L;
        foreach (BoardSpace s in state.Board)
        {
            if (s.Kind == SpaceKind.GovernmentTile)
                h = h * 1_000_003 + (s.IsRevealed ? 1 : 0) + (s.TileId?.GetHashCode() ?? 0) * 2L;
        }
        foreach (PropertyState owned in state.Properties.Values)
            h = h * 1_000_003 + ((owned.OwnerId ?? -1) + 2) * 4 + (owned.IsPending ? 1 : 0) + (owned.IsMortgaged ? 2 : 0);
        return h;
    }

    /// <summary>A color perk or railroad/utility bonus that a player has switched on.</summary>
    private sealed record EarnedPerk(int PlayerId, string Name, IReadOnlyList<ModifierSpec> Specs, string? GroupId);

    /// <summary>
    /// Every perk and bonus switched on right now. They depend on what each
    /// player owns outright (not pending, not mortgaged); one pass over the
    /// properties counts it all.
    /// </summary>
    private static List<EarnedPerk> EarnedPerks(GameState state, GameContent content)
    {
        var groupCounts = new Dictionary<(int Owner, string Group), int>();
        var typeCounts = new Dictionary<(int Owner, PropertyType Type), int>();
        foreach (PropertyState owned in state.Properties.Values)
        {
            if (owned.OwnerId is not int owner || owned.IsPending || owned.IsMortgaged)
                continue;
            PropertyDefinition def = content.Property(owned.PropertyId);
            if (def.Group != null)
                groupCounts[(owner, def.Group)] = groupCounts.GetValueOrDefault((owner, def.Group)) + 1;
            else
                typeCounts[(owner, def.Type)] = typeCounts.GetValueOrDefault((owner, def.Type)) + 1;
        }

        var earned = new List<EarnedPerk>();
        foreach (PlayerState player in state.Players.Where(p => !p.IsBankrupt))
        {
            foreach (GroupDefinition group in content.Groups.Values)
            {
                if (groupCounts.GetValueOrDefault((player.Id, group.Id)) >= content.GroupSize(group.Id))
                    earned.Add(new EarnedPerk(player.Id, group.Perk.Name, group.Perk.Modifiers ?? [], group.Id));
            }
            foreach (BonusDefinition bonus in content.Bonuses)
            {
                if (typeCounts.GetValueOrDefault((player.Id, bonus.PropertyType)) >= bonus.Count)
                    earned.Add(new EarnedPerk(player.Id, bonus.Name, bonus.Modifiers ?? [], null));
            }
        }
        return earned;
    }

    /// <summary>The names of the color perks and railroad/utility bonuses a player has switched on right now.</summary>
    public static List<string> PerkNames(GameState state, GameContent content, int playerId) =>
        EarnedPerks(state, content).Where(p => p.PlayerId == playerId).Select(p => p.Name).ToList();

    /// <summary>Asks every modifier one question and keeps the answers above zero, each with the modifier's name.</summary>
    private static List<(int Amount, string Source)> Amounts(IEnumerable<IRuleModifier> modifiers, Func<IRuleModifier, int> ask) =>
        modifiers.Select(m => (Amount: ask(m), m.Source)).Where(x => x.Amount > 0).ToList();

    // ----- Rent, building and cards -----

    /// <summary>Each modifier that changes this property's rent, with its percentage, for the rent breakdown.</summary>
    /// <param name="active">The active modifiers, if the caller already gathered them (saves work when asking about many properties).</param>
    public static List<(string Source, int Percent)> RentPercents(GameState state, GameContent content, string propertyId,
        IReadOnlyList<IRuleModifier>? active = null) =>
        (active ?? Active(state, content))
            .Select(m => (m.Source, Percent: m.RentPercent(state, propertyId)))
            .Where(x => x.Percent != 0)
            .ToList();

    /// <summary>True if this property ignores every negative rent modifier (Dark Blue's Prime Real Estate).</summary>
    public static bool IgnoresNegativeRent(GameState state, GameContent content, string propertyId,
        IReadOnlyList<IRuleModifier>? active = null) =>
        (active ?? Active(state, content)).Any(m => m.IgnoresNegativeRent(state, propertyId));

    /// <summary>
    /// The total rent percentage from every modifier, with the negative ones
    /// left out if the property ignores them, kept between the rules' limits (Section 13.1, step 2).
    /// </summary>
    public static int NetRentPercent(GameState state, GameContent content, string propertyId,
        IReadOnlyList<IRuleModifier>? active = null)
    {
        active ??= Active(state, content);
        bool ignoresBad = IgnoresNegativeRent(state, content, propertyId, active);
        int total = RentPercents(state, content, propertyId, active).Where(p => !ignoresBad || p.Percent > 0).Sum(p => p.Percent);
        return Math.Clamp(total, content.Rules.MinRentPercent, content.Rules.MaxRentPercent);
    }

    public static int BuildDiscountPercent(GameState state, GameContent content, string propertyId,
        IReadOnlyList<IRuleModifier>? active = null) =>
        (active ?? Active(state, content)).Sum(m => m.BuildDiscountPercent(state, propertyId));

    public static bool CanBeTargetedByCards(GameState state, GameContent content, string propertyId, CardPolarity polarity) =>
        Active(state, content).All(m => m.CanBeTargetedByCards(state, propertyId, polarity));

    public static bool IsFireSafe(GameState state, GameContent content, string propertyId) =>
        Active(state, content).Any(m => m.ProtectsFromFire(state, propertyId));

    /// <summary>False if this card may not be played against this player (the Lawyer can't be Audited).</summary>
    public static bool CanPlayerBeTargeted(GameState state, GameContent content, int playerId, CardDefinition card) =>
        Active(state, content).All(m => m.CanPlayerBeTargeted(state, playerId, card));

    // ----- GO -----

    /// <summary>The cash perks that pay this player when they pass GO, each with its name for the log (Community Roots).</summary>
    public static List<(int Amount, string Source)> GoCashes(GameState state, GameContent content, int playerId) =>
        Amounts(Active(state, content), m => m.GoCash(state, playerId));

    /// <summary>The extra Momentum tokens passing GO gives this player, each with its name (Commuter Belt).</summary>
    public static List<(int Amount, string Source)> GoMomentums(GameState state, GameContent content, int playerId) =>
        Amounts(Active(state, content), m => m.GoMomentum(state, playerId));

    /// <summary>Who an opponent passing GO must pay, and how much (Utility Bill).</summary>
    public static List<(int PayeeId, int Amount, string Source)> GoTolls(GameState state, GameContent content, int passerId) =>
        Active(state, content)
            .Where(m => m.OwnerId is not null)
            .Select(m => (PayeeId: m.OwnerId!.Value, Amount: m.GoToll(state, passerId), m.Source))
            .Where(x => x.Amount > 0)
            .ToList();

    public static int GoPeeks(GameState state, GameContent content, int playerId) =>
        Active(state, content).Sum(m => m.GoPeeks(state, playerId));

    public static int StartPeeks(GameState state, GameContent content, int playerId) =>
        Active(state, content).Sum(m => m.StartPeeks(state, playerId));

    // ----- Momentum -----

    /// <summary>The most Momentum tokens this player can hold (Section 7.3).</summary>
    public static int MomentumCap(GameState state, GameContent content, int playerId,
        IReadOnlyList<IRuleModifier>? active = null) =>
        content.Rules.MomentumCap + (active ?? Active(state, content)).Sum(m => m.MomentumCapBonus(state, playerId));

    /// <summary>How many tokens this player starts the game with.</summary>
    public static int StartingMomentum(GameState state, GameContent content, int playerId) =>
        Math.Max(content.Rules.StartingMomentum, Active(state, content).Select(m => m.StartingMomentum(state, playerId)).DefaultIfEmpty(0).Max());

    /// <summary>What a Precision Roll costs this player (never less than 1 token).</summary>
    public static int PrecisionRollCost(GameState state, GameContent content, int playerId) =>
        Math.Max(1, content.Rules.PrecisionRollCost - Active(state, content).Sum(m => m.PrecisionRollDiscount(state, playerId)));

    /// <summary>Tokens the owner gains when someone pays them rent on a property of this type (Toll Collector).</summary>
    public static List<(int Amount, string Source)> MomentumFromRents(GameState state, GameContent content, int ownerId, PropertyType type) =>
        Amounts(Active(state, content), m => m.MomentumFromRent(state, ownerId, type));

    // ----- Cards -----

    public static int HandLimit(GameState state, GameContent content, int playerId,
        IReadOnlyList<IRuleModifier>? active = null) =>
        content.Rules.HandLimit + (active ?? Active(state, content)).Sum(m => m.HandLimitBonus(state, playerId));

    public static bool DrawsTwoKeepsOne(GameState state, GameContent content, int playerId) =>
        Active(state, content).Any(m => m.DrawsTwoKeepsOne(state, playerId));

    // ----- Loans and the start of a turn -----

    /// <summary>What repaying one loan costs this player (Section 15.7).</summary>
    public static int LoanRepayCost(GameState state, GameContent content, int playerId,
        IReadOnlyList<IRuleModifier>? active = null)
    {
        var cheaper = (active ?? Active(state, content)).Select(m => m.LoanRepayCost(state, playerId)).Where(c => c > 0).ToList();
        return cheaper.Count == 0 ? content.Rules.LoanRepay : cheaper.Min();
    }

    /// <summary>How many loans this player may hold at once.</summary>
    public static int MaxLoans(GameState state, GameContent content, int playerId,
        IReadOnlyList<IRuleModifier>? active = null) =>
        Math.Max(content.Rules.MaxLoans, (active ?? Active(state, content)).Select(m => m.MaxLoans(state, playerId)).DefaultIfEmpty(0).Max());

    /// <summary>Cash paid to this player as their turn starts, each with its name (Dividends).</summary>
    public static List<(int Amount, string Source)> TurnStartIncomes(GameState state, GameContent content, int playerId) =>
        Amounts(Active(state, content), m => m.TurnStartIncome(state, playerId));

    /// <summary>Who a player starting their turn must pay, and how much (Neighborhood Watch).</summary>
    public static List<(int PayeeId, int Amount, string Source)> TurnStartCharges(GameState state, GameContent content, int playerId, bool jailedOrRecovering) =>
        Active(state, content)
            .Where(m => m.OwnerId is not null)
            .Select(m => (PayeeId: m.OwnerId!.Value, Amount: m.TurnStartCharge(state, playerId, jailedOrRecovering), m.Source))
            .Where(x => x.Amount > 0)
            .ToList();

    // ----- Building -----

    /// <summary>Who a player building a level must pay, and how much (Power Grid).</summary>
    public static List<(int PayeeId, int Amount, string Source)> BuildFees(GameState state, GameContent content, int builderId,
        IReadOnlyList<IRuleModifier>? active = null) =>
        (active ?? Active(state, content))
            .Where(m => m.OwnerId is not null)
            .Select(m => (PayeeId: m.OwnerId!.Value, Amount: m.BuildFee(state, builderId), m.Source))
            .Where(x => x.Amount > 0)
            .ToList();

    // ----- Moving, the Hospital and Jail -----

    /// <summary>The Express Line price for this player, or 0 if they haven't earned it.</summary>
    public static int ExpressLineCost(GameState state, GameContent content, int playerId) =>
        Active(state, content).Select(m => m.ExpressLineCost(state, playerId)).Where(c => c > 0).DefaultIfEmpty(0).Min();

    public static bool CanHostileTakeover(GameState state, GameContent content, int playerId) =>
        Active(state, content).Any(m => m.CanHostileTakeover(state, playerId));

    /// <summary>What it costs this player to leave Jail early (the Lawyer pays nothing).</summary>
    public static int Bail(GameState state, GameContent content, int playerId) =>
        Active(state, content).Any(m => m.FreeBail(state, playerId)) ? 0 : content.Rules.JailBail;

    public static bool ShrugsOffHospital(GameState state, GameContent content, int playerId) =>
        Active(state, content).Any(m => m.ShrugsOffHospital(state, playerId));

    public static int InsuredMarkerCost(GameState state, GameContent content, int playerId)
    {
        var cheaper = Active(state, content).Select(m => m.InsuredMarkerCost(state, playerId)).Where(c => c > 0).ToList();
        return cheaper.Count == 0 ? content.Rules.InsuredMarkerCost : cheaper.Min();
    }

    // ----- Auctions, the Casino and the Council -----

    /// <summary>How much higher this player's bid counts when bids are compared (the Mogul).</summary>
    public static int BidBonus(GameState state, GameContent content, int playerId) =>
        Active(state, content).Sum(m => m.BidBonus(state, playerId));

    /// <summary>The name of the modifier that wins this player's dice ties, or null if they have none (the Mogul).</summary>
    public static string? DiceTieBreaker(GameState state, GameContent content, int playerId) =>
        Active(state, content).FirstOrDefault(m => m.WinsDiceTies(state, playerId))?.Source;

    public static bool CasinoReroll(GameState state, GameContent content, int playerId) =>
        Active(state, content).Any(m => m.CasinoReroll(state, playerId));

    /// <summary>This player's City Council influence (Section 11.3).</summary>
    public static int VoteInfluence(GameState state, GameContent content, int playerId) =>
        Math.Max(content.Rules.VoteInfluence, Active(state, content).Select(m => m.VoteInfluence(state, playerId)).DefaultIfEmpty(0).Max());
}
