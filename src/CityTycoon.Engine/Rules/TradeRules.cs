using CityTycoon.Engine.Actions;
using CityTycoon.Engine.Content;
using CityTycoon.Engine.Events;
using CityTycoon.Engine.State;
using CityTycoon.Engine.Steps;

namespace CityTycoon.Engine.Rules;

/// <summary>Trading between players (Section 15.9).</summary>
public static class TradeRules
{
    /// <summary>
    /// Checks a trade could happen right now. Returns null if it can, or a
    /// plain-English reason if not. It's checked when the trade is offered and
    /// again when it's accepted, in case something changed in between.
    /// </summary>
    public static string? WhyInvalid(EngineContext ctx, TradeOffer offer)
    {
        GameState state = ctx.State;
        if (offer.FromId == offer.ToId)
            return "You can't trade with yourself.";
        if (state.Players.All(p => p.Id != offer.ToId) || state.Player(offer.ToId).IsBankrupt)
            return "That player isn't in the game.";
        if (offer.GiveCash < 0 || offer.GetCash < 0)
            return "Cash amounts can't be negative.";
        if (offer.GiveCash == 0 && offer.GetCash == 0 && offer.GiveProperties.Count == 0 && offer.GetProperties.Count == 0)
            return "The trade is empty.";
        if (offer.GiveProperties.Concat(offer.GetProperties).Distinct().Count() != offer.GiveProperties.Count + offer.GetProperties.Count)
            return "A property is listed twice.";

        PlayerState from = state.Player(offer.FromId);
        PlayerState to = state.Player(offer.ToId);
        if (from.Cash < offer.GiveCash)
            return $"{from.Name} doesn't have that much cash.";
        if (to.Cash < offer.GetCash)
            return $"{to.Name} doesn't have that much cash.";

        foreach (string id in offer.GiveProperties)
        {
            if (WhyCantTradeProperty(ctx, offer.FromId, id) is string reason)
                return reason;
        }
        foreach (string id in offer.GetProperties)
        {
            if (WhyCantTradeProperty(ctx, offer.ToId, id) is string reason)
                return reason;
        }

        // Each side must be able to pay the bank's fee for any mortgaged property they receive.
        if (from.Cash - offer.GiveCash + offer.GetCash < MortgageFees(ctx, offer.GetProperties))
            return $"{from.Name} couldn't pay the fees on the mortgaged properties.";
        if (to.Cash - offer.GetCash + offer.GiveCash < MortgageFees(ctx, offer.GiveProperties))
            return $"{to.Name} couldn't pay the fees on the mortgaged properties.";

        return null;
    }

    /// <summary>
    /// Checks a counter-offer to <paramref name="offer"/>. Returns null if it can be
    /// sent, or a plain-English reason if not. A counter goes back the other way
    /// (from whoever is answering, to whoever made the offer) and must change something.
    /// </summary>
    public static string? WhyBadCounter(EngineContext ctx, TradeOffer offer, TradeOffer counter, int countersSoFar)
    {
        if (countersSoFar >= ctx.Rules.MaxTradeCounters)
            return "This trade has had all its counter-offers. Accept or decline it.";
        if (counter.FromId != offer.ToId || counter.ToId != offer.FromId)
            return "A counter-offer goes back to the player who made the offer.";
        if (IsSameTrade(offer, counter))
            return "That's the same trade. Change something, or just accept it.";
        return WhyInvalid(ctx, counter);
    }

    /// <summary>True if the counter swaps exactly what the offer did (just written from the other side).</summary>
    private static bool IsSameTrade(TradeOffer offer, TradeOffer counter) =>
        offer.GiveCash == counter.GetCash && offer.GetCash == counter.GiveCash
        && offer.GiveProperties.ToHashSet().SetEquals(counter.GetProperties)
        && offer.GetProperties.ToHashSet().SetEquals(counter.GiveProperties);

    /// <summary>Only unbuilt, non-pending properties can be traded, and only if the whole group has no buildings.</summary>
    public static string? WhyCantTradeProperty(EngineContext ctx, int ownerId, string propertyId)
    {
        if (!ctx.State.Properties.TryGetValue(propertyId, out PropertyState? owned) || owned.OwnerId != ownerId)
            return "Someone is offering a property they don't own.";
        if (owned.IsPending)
            return $"{ctx.Content.Property(propertyId).Name} is pending, so it can't be traded yet.";
        if (MoneyRules.GroupHasBuildings(ctx, propertyId))
            return $"{ctx.Content.Property(propertyId).Name}'s group has buildings, so it can't be traded.";
        return null;
    }

    private static int MortgageFees(EngineContext ctx, IEnumerable<string> propertyIds) =>
        propertyIds.Where(id => ctx.State.Properties.TryGetValue(id, out var p) && p.IsMortgaged)
                   .Sum(id => MoneyRules.MortgageTransferFee(ctx.Content, ctx.Content.Property(id)));

    /// <summary>Swaps everything in the offer. Mortgaged properties stay mortgaged; the receiver pays the bank 10%.</summary>
    public static void Execute(EngineContext ctx, TradeOffer offer)
    {
        PlayerState from = ctx.Player(offer.FromId);
        PlayerState to = ctx.Player(offer.ToId);

        from.Cash += offer.GetCash - offer.GiveCash;
        to.Cash += offer.GiveCash - offer.GetCash;

        int feesFrom = MortgageFees(ctx, offer.GetProperties);
        int feesTo = MortgageFees(ctx, offer.GiveProperties);
        foreach (string id in offer.GiveProperties)
            ctx.State.Properties[id].OwnerId = offer.ToId;
        foreach (string id in offer.GetProperties)
            ctx.State.Properties[id].OwnerId = offer.FromId;
        from.Cash -= feesFrom;
        to.Cash -= feesTo;

        ctx.Emit(new TradeCompleted(offer, feesFrom + feesTo));
    }
}
