using CityTycoon.Engine.Content;
using CityTycoon.Engine.Events;
using CityTycoon.Engine.Modifiers;
using CityTycoon.Engine.State;
using CityTycoon.Engine.Steps;

namespace CityTycoon.Engine.Rules;

/// <summary>Mortgages (Section 15.8) and loans (Section 15.7).</summary>
public static class MoneyRules
{
    /// <summary>Unmortgaging costs the mortgage value plus 10%, rounded up to a whole dollar.</summary>
    public static int UnmortgageCost(GameContent content, PropertyDefinition def)
    {
        int value = NetWorth.MortgageValue(content, def);
        return (value * (100 + content.Rules.UnmortgageFeePercent) + 99) / 100;
    }

    /// <summary>The fee for receiving a mortgaged property in a trade: 10% of its mortgage value (Section 15.9).</summary>
    public static int MortgageTransferFee(GameContent content, PropertyDefinition def) =>
        (NetWorth.MortgageValue(content, def) * content.Rules.UnmortgageFeePercent + 99) / 100;

    public static string? WhyCantMortgage(EngineContext ctx, int playerId, string propertyId)
    {
        PropertyState owned = ctx.State.Properties[propertyId];
        if (owned.OwnerId != playerId)
            return "You don't own this property.";
        if (owned.IsPending)
            return "You can't mortgage a pending property.";
        if (owned.IsMortgaged)
            return "It's already mortgaged.";
        if (GroupHasBuildings(ctx, propertyId))
            return "Sell the buildings in this group first.";
        return null;
    }

    public static void MortgageProperty(EngineContext ctx, int playerId, string propertyId)
    {
        int value = NetWorth.MortgageValue(ctx.Content, ctx.Content.Property(propertyId));
        ctx.State.Properties[propertyId].IsMortgaged = true;
        ctx.Player(playerId).Cash += value;
        ctx.Emit(new PropertyMortgaged(playerId, propertyId, value));
    }

    public static string? WhyCantUnmortgage(EngineContext ctx, int playerId, string propertyId)
    {
        PropertyState owned = ctx.State.Properties[propertyId];
        if (owned.OwnerId != playerId || !owned.IsMortgaged)
            return "You don't have this property mortgaged.";
        if (ctx.Player(playerId).Cash < UnmortgageCost(ctx.Content, ctx.Content.Property(propertyId)))
            return "You don't have enough cash.";
        return null;
    }

    public static void UnmortgageProperty(EngineContext ctx, int playerId, string propertyId)
    {
        int cost = UnmortgageCost(ctx.Content, ctx.Content.Property(propertyId));
        ctx.State.Properties[propertyId].IsMortgaged = false;
        ctx.Player(playerId).Cash -= cost;
        ctx.Emit(new PropertyUnmortgaged(playerId, propertyId, cost));
    }

    /// <summary>A property in a color group can't be mortgaged while any of its group has buildings.</summary>
    public static bool GroupHasBuildings(EngineContext ctx, string propertyId)
    {
        string? group = ctx.Content.Property(propertyId).Group;
        return group != null && ctx.Content.GroupMembers(group).Any(id => ctx.State.Properties[id].Level > 0);
    }

    /// <summary>How many loans this player may hold (3 with the Yellow perk, otherwise 2).</summary>
    public static int MaxLoans(EngineContext ctx, int playerId) => RuleModifiers.MaxLoans(ctx.State, ctx.Content, playerId);

    /// <summary>What repaying one loan costs this player ($210 with the Yellow perk, otherwise $250).</summary>
    public static int LoanRepayCost(EngineContext ctx, int playerId) => RuleModifiers.LoanRepayCost(ctx.State, ctx.Content, playerId);

    public static bool CanTakeLoan(EngineContext ctx, int playerId) =>
        ctx.Player(playerId).Loans < MaxLoans(ctx, playerId);

    public static void TakeLoan(EngineContext ctx, int playerId)
    {
        PlayerState player = ctx.Player(playerId);
        player.Loans++;
        player.Cash += ctx.Rules.LoanAmount;
        ctx.Emit(new LoanTaken(playerId, ctx.Rules.LoanAmount));
    }

    public static bool CanRepayLoan(EngineContext ctx, int playerId) =>
        ctx.Player(playerId).Loans > 0 && ctx.Player(playerId).Cash >= LoanRepayCost(ctx, playerId);

    public static void RepayLoan(EngineContext ctx, int playerId)
    {
        PlayerState player = ctx.Player(playerId);
        int cost = LoanRepayCost(ctx, playerId);
        player.Loans--;
        player.Cash -= cost;
        ctx.Emit(new LoanRepaid(playerId, cost));
    }

    /// <summary>
    /// The most cash a player could have right now by selling every building,
    /// mortgaging everything they can, and taking every loan they're allowed
    /// (Section 15.10). If a debt is bigger than this, they're bankrupt.
    /// </summary>
    public static int MaxRaisableCash(EngineContext ctx, int playerId)
    {
        PlayerState player = ctx.Player(playerId);
        int total = player.Cash;
        foreach (PropertyState owned in ctx.State.Properties.Values.Where(p => p.OwnerId == playerId))
        {
            PropertyDefinition def = ctx.Content.Property(owned.PropertyId);
            total += BuildingRules.SellAllValue(ctx.Content, def, owned.Level);
            if (!owned.IsMortgaged && !owned.IsPending)
                total += NetWorth.MortgageValue(ctx.Content, def);
        }
        total += (MaxLoans(ctx, playerId) - player.Loans) * ctx.Rules.LoanAmount;
        return total;
    }
}
