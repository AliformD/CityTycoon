using CityTycoon.Engine.Content;

namespace CityTycoon.Engine.Modifiers;

/// <summary>
/// The list of every modifier type that content files can name (Section 21.3).
/// A character, perk or bonus in JSON says <c>{ "type": "GoCash", "value": 50 }</c>
/// and this list turns "GoCash" into the <see cref="GoCashModifier"/> class.
///
/// To add a brand-new kind of rule change: write one class (see the other
/// *Modifiers.cs files), then add one line below. A character or perk that
/// reuses an existing type needs no C# at all.
///
/// The list is written out by hand (not found by "reflection") so it also
/// works in the browser, where unused code is trimmed away.
/// </summary>
public static class ModifierTypes
{
    /// <param name="NeedsValue">True if the entry must have a "value" above zero.</param>
    /// <param name="NeedsCard">True if the entry must name an existing card in "card".</param>
    private sealed record Kind(Func<ModifierContext, IRuleModifier> Create, bool NeedsValue = false, bool NeedsCard = false);

    private static readonly Dictionary<string, Kind> All = new()
    {
        // Color perks (Section 13.2)
        ["GoCash"] = new(c => new GoCashModifier(c), NeedsValue: true),
        ["GoMomentum"] = new(c => new GoMomentumModifier(c), NeedsValue: true),
        ["MomentumCap"] = new(c => new MomentumCapModifier(c), NeedsValue: true),
        ["DrawTwoKeepOne"] = new(c => new DrawTwoKeepOneModifier(c)),
        ["HandLimit"] = new(c => new HandLimitModifier(c), NeedsValue: true),
        ["JailToll"] = new(c => new JailTollModifier(c), NeedsValue: true),
        ["HostileTakeover"] = new(c => new HostileTakeoverModifier(c)),
        ["LoanRepayCost"] = new(c => new LoanRepayCostModifier(c), NeedsValue: true),
        ["MaxLoans"] = new(c => new MaxLoansModifier(c), NeedsValue: true),
        ["DividendPerTower"] = new(c => new DividendPerTowerModifier(c), NeedsValue: true),
        ["IgnoreNegativeRent"] = new(c => new IgnoreNegativeRentModifier(c)),
        ["SkyscraperCostMultiplier"] = new(c => new SkyscraperCostMultiplierModifier(c), NeedsValue: true),

        // Railroad and utility bonuses (Sections 13.3 and 13.4)
        ["ExpressLine"] = new(c => new ExpressLineModifier(c), NeedsValue: true),
        ["TollCollector"] = new(c => new TollCollectorModifier(c), NeedsValue: true),
        ["PrecisionRollDiscount"] = new(c => new PrecisionRollDiscountModifier(c), NeedsValue: true),
        ["UtilityBill"] = new(c => new UtilityBillModifier(c), NeedsValue: true),
        ["PowerGrid"] = new(c => new PowerGridModifier(c), NeedsValue: true),

        // Characters (Section 5)
        ["BidBonus"] = new(c => new BidBonusModifier(c), NeedsValue: true),
        ["DiceTieBreaker"] = new(c => new DiceTieBreakerModifier(c)),
        ["CasinoReroll"] = new(c => new CasinoRerollModifier(c)),
        ["MoveReroll"] = new(c => new MoveRerollModifier(c)),
        ["StadiumMultiplier"] = new(c => new StadiumMultiplierModifier(c), NeedsValue: true),
        ["NudgeRange"] = new(c => new NudgeRangeModifier(c), NeedsValue: true),
        ["StartingMomentum"] = new(c => new StartingMomentumModifier(c), NeedsValue: true),
        ["FreeBail"] = new(c => new FreeBailModifier(c)),
        ["JailGoShare"] = new(c => new JailGoShareModifier(c), NeedsValue: true),
        ["ImmuneToCard"] = new(c => new ImmuneToCardModifier(c), NeedsCard: true),
        ["BuildDiscount"] = new(c => new BuildDiscountModifier(c), NeedsValue: true),
        ["VoteInfluence"] = new(c => new VoteInfluenceModifier(c), NeedsValue: true),
        ["LobbyBonus"] = new(c => new LobbyBonusModifier(c), NeedsValue: true),
        ["ReverseVote"] = new(c => new ReverseVoteModifier(c)),
        ["PeekAtStart"] = new(c => new PeekAtStartModifier(c), NeedsValue: true),
        ["PeekOnGo"] = new(c => new PeekOnGoModifier(c), NeedsValue: true),
        ["HospitalImmunity"] = new(c => new HospitalImmunityModifier(c)),
        // "value" is the price, so 0 (free) is allowed.
        ["InsuredMarkerCost"] = new(c => new InsuredMarkerCostModifier(c)),
    };

    public static IReadOnlyCollection<string> Types => All.Keys;

    /// <summary>Builds the modifier a content entry describes.</summary>
    public static IRuleModifier Create(ModifierContext context) => All[context.Spec.Type].Create(context);

    /// <summary>Checks one entry in a content file. Throws a ContentException with a plain message if it is wrong.</summary>
    /// <param name="where">Words that say where the entry is, like "character 'mogul'".</param>
    public static void Validate(ModifierSpec spec, string where, GameContent content)
    {
        if (!All.TryGetValue(spec.Type, out Kind? kind))
            throw new ContentException($"{where} uses the unknown modifier type '{spec.Type}'.");
        if (kind.NeedsValue && spec.Value <= 0)
            throw new ContentException($"{where}: the '{spec.Type}' modifier needs a \"value\" above zero.");
        if (kind.NeedsCard && (spec.Card == null || !content.Cards.ContainsKey(spec.Card)))
            throw new ContentException($"{where}: the '{spec.Type}' modifier needs a \"card\" that exists.");
    }
}
