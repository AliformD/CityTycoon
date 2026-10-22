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

    /// <summary>What repaying a loan of this size costs this player (less interest with the Yellow perk).</summary>
    public static int LoanRepayCost(EngineContext ctx, int playerId, int amount) =>
        RuleModifiers.LoanRepayCost(ctx.State, ctx.Content, playerId, amount);

    /// <summary>True if this player may take another loan (of any size the bank offers).</summary>
    public static bool CanTakeLoan(EngineContext ctx, int playerId) =>
        ctx.Player(playerId).Loans < MaxLoans(ctx, playerId);

    public static bool CanTakeLoan(EngineContext ctx, int playerId, int amount) =>
        CanTakeLoan(ctx, playerId) && ctx.Rules.LoanOptions.Any(o => o.Amount == amount);

    public static void TakeLoan(EngineContext ctx, int playerId, int amount)
    {
        PlayerState player = ctx.Player(playerId);
        player.LoanBook.Add(amount);
        player.Cash += amount;
        ctx.Emit(new LoanTaken(playerId, amount));
    }

    /// <summary>True if this player holds a loan of this size and has the cash to pay it back.</summary>
    public static bool CanRepayLoan(EngineContext ctx, int playerId, int amount) =>
        ctx.Player(playerId).LoanBook.Contains(amount) && ctx.Player(playerId).Cash >= LoanRepayCost(ctx, playerId, amount);

    public static void RepayLoan(EngineContext ctx, int playerId, int amount)
    {
        PlayerState player = ctx.Player(playerId);
        int cost = LoanRepayCost(ctx, playerId, amount);
        player.LoanBook.Remove(amount);
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
        total += Math.Max(0, MaxLoans(ctx, playerId) - player.Loans) * ctx.Rules.BiggestLoan;
        return total;
    }
}
