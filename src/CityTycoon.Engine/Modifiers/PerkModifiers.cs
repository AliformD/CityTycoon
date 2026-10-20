using CityTycoon.Engine.Content;
using CityTycoon.Engine.State;

namespace CityTycoon.Engine.Modifiers;

// One small class per color-group perk effect (Section 13.2). They exist only
// while their owner holds the whole group, with none of it mortgaged or
// pending. The groups and their perks are described in content/groups.json.

/// <summary>Brown, Community Roots: extra cash passing GO.</summary>
public sealed class GoCashModifier(ModifierContext c) : OwnedModifier(c), IRuleModifier
{
    public int GoCash(GameState s, int playerId) => Mine(playerId) ? Value : 0;
}

/// <summary>Light Blue, Commuter Belt: an extra Momentum token passing GO.</summary>
public sealed class GoMomentumModifier(ModifierContext c) : OwnedModifier(c), IRuleModifier
{
    public int GoMomentum(GameState s, int playerId) => Mine(playerId) ? Value : 0;
}

/// <summary>Pink, Arts District: draw 2 Opportunity cards and keep 1.</summary>
public sealed class DrawTwoKeepOneModifier(ModifierContext c) : OwnedModifier(c), IRuleModifier
{
    public bool DrawsTwoKeepsOne(GameState s, int playerId) => Mine(playerId);
}

/// <summary>Pink, Arts District: room for another card in hand.</summary>
public sealed class HandLimitModifier(ModifierContext c) : OwnedModifier(c), IRuleModifier
{
    public int HandLimitBonus(GameState s, int playerId) => Mine(playerId) ? Value : 0;
}

/// <summary>Orange, Neighborhood Watch: opponents in Jail or recovering pay the owner.</summary>
public sealed class JailTollModifier(ModifierContext c) : OwnedModifier(c), IRuleModifier
{
    public int TurnStartCharge(GameState s, int playerId, bool jailedOrRecovering) =>
        jailedOrRecovering && !Mine(playerId) ? Value : 0;
}

/// <summary>Red, Hostile Takeover: once per lap, force an auction.</summary>
public sealed class HostileTakeoverModifier(ModifierContext c) : OwnedModifier(c), IRuleModifier
{
    public bool CanHostileTakeover(GameState s, int playerId) => Mine(playerId);
}

/// <summary>Yellow, Bank Partner: cheaper loan repayment.</summary>
public sealed class LoanRepayCostModifier(ModifierContext c) : OwnedModifier(c), IRuleModifier
{
    public int LoanRepayCost(GameState s, int playerId) => Mine(playerId) ? Value : 0;
}

/// <summary>Yellow, Bank Partner: more loans at once.</summary>
public sealed class MaxLoansModifier(ModifierContext c) : OwnedModifier(c), IRuleModifier
{
    public int MaxLoans(GameState s, int playerId) => Mine(playerId) ? Value : 0;
}

/// <summary>Green, Dividends: cash at the start of each turn for every hotel or skyscraper owned.</summary>
public sealed class DividendPerTowerModifier(ModifierContext c) : OwnedModifier(c), IRuleModifier
{
    public int TurnStartIncome(GameState s, int playerId)
    {
        if (!Mine(playerId))
            return 0;
        int hotelLevel = Content.Rules.HotelLevel;
        return Value * s.Properties.Values.Count(p => p.OwnerId == Owner && p.Level >= hotelLevel);
    }
}

/// <summary>Dark Blue, Prime Real Estate: this group's rent ignores every negative modifier.</summary>
public sealed class IgnoreNegativeRentModifier(ModifierContext c) : OwnedModifier(c), IRuleModifier
{
    public bool IgnoresNegativeRent(GameState s, string propertyId) =>
        s.Properties[propertyId].OwnerId == Owner && Content.Property(propertyId).Group == Context.GroupId;
}

/// <summary>
/// Dark Blue, Prime Real Estate: skyscraper levels cost a different multiple of
/// the house cost. It has no hook: <see cref="Rules.BuildingRules.LevelCost"/>
/// reads the number straight from the content, so the price you pay and the
/// value you get back when selling always agree.
/// </summary>
public sealed class SkyscraperCostMultiplierModifier(ModifierContext c) : OwnedModifier(c);
