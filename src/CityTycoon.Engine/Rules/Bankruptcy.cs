using CityTycoon.Engine.Modifiers;
using CityTycoon.Engine.Events;
using CityTycoon.Engine.State;
using CityTycoon.Engine.Steps;

namespace CityTycoon.Engine.Rules;

/// <summary>Going bankrupt (Section 15.10).</summary>
public static class Bankruptcy
{
    /// <summary>The player is out of the game. Their stuff goes to whoever they owed.</summary>
    /// <param name="creditorId">The player who was owed, or null for the bank.</param>
    public static void Declare(EngineContext ctx, PlayerState player, int? creditorId)
    {
        var owned = ctx.State.Properties.Values.Where(p => p.OwnerId == player.Id).ToList();

        // 1. Every building is sold to the bank first (this also returns their permits).
        int buildingsSoldFor = 0;
        foreach (PropertyState p in owned.Where(p => p.Level > 0))
            buildingsSoldFor += BuildingRules.RemoveAllBuildings(ctx, p.PropertyId);
        player.Cash += buildingsSoldFor;

        // 2. The bank's loans are paid back first, from whatever cash there is.
        int beforeLoans = player.Cash;
        player.Cash = Math.Max(0, player.Cash - RuleModifiers.LoanDebt(ctx.State, ctx.Content, player.Id));
        player.LoanBook.Clear();

        // 3. Pending properties were never fully paid for, so they go back to the bank.
        var pending = owned.Where(p => p.IsPending).ToList();
        var outright = owned.Where(p => !p.IsPending).ToList();
        var toAuction = new List<PropertyState>(pending);

        int cash = player.Cash;
        if (creditorId is int toId)
        {
            // Owed a player: they get the cash and properties (still mortgaged where they were).
            ctx.Player(toId).Cash += cash;
            foreach (PropertyState p in outright)
                p.OwnerId = toId;
            // Momentum tokens go to them too (up to their token limit).
            MomentumRules.Gain(ctx, ctx.Player(toId), player.Momentum, $"{player.Name}'s bankruptcy");
        }
        else
        {
            // Owed the bank: every property is auctioned.
            toAuction.AddRange(outright);
        }

        foreach (PropertyState p in toAuction)
        {
            p.OwnerId = null;
            p.IsMortgaged = false;
            p.IsPending = false;
            p.PendingBalance = 0;
        }

        player.Cash = 0;
        player.Momentum = 0;
        player.IsInsured = false;
        player.IsRecovering = false;
        ctx.State.StadiumBoosts.RemoveAll(b => b.ChosenById == player.Id);
        ctx.State.PropertyBoosts.RemoveAll(b => b.ChosenById == player.Id);
        // Their Opportunity cards go to the discard pile.
        foreach (string card in player.Hand)
            Cards.CardRules.Discard(ctx, card);
        player.Hand.Clear();
        player.RoadWorkCost = 0;
        player.IsBankrupt = true;
        player.InJail = false;
        ctx.Emit(new PlayerBankrupt(player.Id, creditorId, cash, outright.Select(p => p.PropertyId).ToList(),
            buildingsSoldFor, beforeLoans - cash));

        // Pushed in reverse board order, so the first property on the board is auctioned first.
        foreach (PropertyState p in toAuction.OrderByDescending(p => ctx.State.IndexOf(p.PropertyId)))
        {
            ctx.Push(new AuctionStep
            {
                PropertyId = p.PropertyId,
                Kind = AuctionKind.Bankruptcy,
                TieBreakStartId = ctx.State.ActivePlayer.Id,
                ExcludedPlayerId = player.Id,
            });
        }
    }
}
