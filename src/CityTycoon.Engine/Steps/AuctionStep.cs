using CityTycoon.Engine.Actions;
using CityTycoon.Engine.Content;
using CityTycoon.Engine.Events;
using CityTycoon.Engine.Modifiers;
using CityTycoon.Engine.Rules;
using CityTycoon.Engine.State;

namespace CityTycoon.Engine.Steps;

/// <summary>Why an auction is happening. It decides the minimum bid (Section 8.3).</summary>
public enum AuctionKind
{
    /// <summary>Someone landed on an unowned property.</summary>
    Normal,

    /// <summary>A bankrupt player's property went back to the bank.</summary>
    Bankruptcy,

    /// <summary>A pending property whose balance wasn't paid is re-auctioned (Section 8.5).</summary>
    Forfeit,

    /// <summary>A Hostile Bid forces an auction on another player's property (Section 8.6).</summary>
    Forced,
}

/// <summary>
/// A sealed-bid auction (Section 8.2). Everyone secretly bids or passes at the
/// same time, then all bids are revealed together and the highest bid wins.
/// Each bid is either "pay in full" or a down payment (Section 8.4 and 8.5).
/// Bidders may take a loan while the auction is open, so they can bid more.
/// </summary>
public sealed class AuctionStep : Step
{
    public string PropertyId { get; set; } = "";
    public AuctionKind Kind { get; set; }

    /// <summary>Ties go to this player first, then the next players in turn order (Section 8.2, rule 5).</summary>
    public int TieBreakStartId { get; set; }

    /// <summary>A player who may not bid (for example the bankrupt player whose property this was).</summary>
    public int? ExcludedPlayerId { get; set; }

    /// <summary>For a forced auction: the player who owns the property now.</summary>
    public int? OwnerId { get; set; }

    public bool Started { get; set; }
    public int MinBid { get; set; }

    /// <summary>Who may bid, in turn order.</summary>
    public List<int> Bidders { get; set; } = [];

    /// <summary>Sealed answers so far: player ID to bid amount (null means pass).</summary>
    public Dictionary<int, int?> Bids { get; set; } = [];

    /// <summary>The players whose bid is a down payment instead of pay in full.</summary>
    public HashSet<int> DownPaymentBidders { get; set; } = [];

    public override StepResult Run(EngineContext ctx)
    {
        if (!Started)
            Start(ctx);

        var waitingOn = Bidders.Where(id => !Bids.ContainsKey(id)).ToList();
        if (waitingOn.Count > 0)
        {
            string name = ctx.Content.Property(PropertyId).Name;
            return StepResult.WaitFor(waitingOn.Select(id => new PendingDecision(
                id, DecisionKind.SubmitBid, $"Auction for {name}: bid (minimum ${MinBid}) or pass.",
                PropertyId, MinBid, MaxBidFor(ctx, id), ctx.Rules.BidIncrement,
                MaxDownPaymentBid: MaxDownPaymentBidFor(ctx, id),
                // Taking a loan is the only other thing you can do mid-auction (Section 8.4).
                Management: ManagementOptions.TakeLoan)));
        }

        Resolve(ctx);
        return StepResult.Done;
    }

    private void Start(EngineContext ctx)
    {
        Started = true;
        PropertyDefinition def = ctx.Content.Property(PropertyId);
        MinBid = MinimumBid(ctx.Rules, def.AssessedValue, Kind);

        // Every player who isn't bankrupt may bid, including players in Jail.
        Bidders = ctx.State.ActivePlayers.Where(p => p.Id != ExcludedPlayerId).Select(p => p.Id).ToList();

        // Players who can't reach the minimum bid, even with every loan they
        // could still take and a down payment, pass automatically. That way
        // nobody is asked a question with only one answer (docs/DECISIONS.md).
        foreach (int id in Bidders.Where(id => !CouldEverBid(ctx, id)))
            Bids[id] = null;

        ctx.Emit(new AuctionStarted(PropertyId, MinBid));
    }

    /// <summary>The minimum bid, rounded up to the bid step (Section 8.3).</summary>
    public static int MinimumBid(RulesConfig rules, int assessedValue, AuctionKind kind)
    {
        int percent = kind == AuctionKind.Forced ? rules.ForcedAuctionMinBidPercent : rules.AuctionMinBidPercent;
        int raw = assessedValue * percent;          // still multiplied by 100 at this point
        int step = rules.BidIncrement * 100;
        int roundedUp = (raw + step - 1) / step;    // whole-number "round up" trick
        return roundedUp * rules.BidIncrement;
    }

    /// <summary>The highest "pay in full" bid a player can afford: their cash, rounded down to the bid step.</summary>
    private static int MaxBidFor(EngineContext ctx, int playerId) => MaxFullBid(ctx.Rules, ctx.Player(playerId).Cash);

    private static int MaxFullBid(RulesConfig rules, int cash) => cash / rules.BidIncrement * rules.BidIncrement;

    /// <summary>
    /// The deposit for a down-payment bid: 20% of the bid, rounded up to $10 (Section 8.4).
    /// </summary>
    public static int Deposit(RulesConfig rules, int bid)
    {
        int raw = bid * rules.DownPaymentPercent; // still multiplied by 100
        int step = rules.BidIncrement * 100;
        return (raw + step - 1) / step * rules.BidIncrement;
    }

    /// <summary>True if the player has a free pending slot (Section 8.5: at most 2 pending properties).</summary>
    public static bool HasFreePendingSlot(EngineContext ctx, int playerId) =>
        ctx.State.Properties.Values.Count(p => p.OwnerId == playerId && p.IsPending) < ctx.Rules.MaxPendingProperties;

    /// <summary>
    /// The highest down-payment bid a player can make, or 0 if they can't make one.
    /// The deposit (not the whole bid) has to fit in their cash.
    /// </summary>
    private int MaxDownPaymentBidFor(EngineContext ctx, int playerId) =>
        AllowsDownPayment && HasFreePendingSlot(ctx, playerId) ? MaxDepositBid(ctx.Rules, ctx.Player(playerId).Cash) : 0;

    /// <summary>Forced auctions are paid in full: the money goes straight to the owner (docs/DECISIONS.md).</summary>
    private bool AllowsDownPayment => Kind != AuctionKind.Forced;

    /// <summary>
    /// Can a forced auction be held on this property (Section 8.6)? It must be
    /// owned outright, with no buildings, not mortgaged, and not part of a full
    /// color group its owner holds. (Police Stations arrive in Phase 6.)
    /// </summary>
    public static bool CanForceAuction(EngineContext ctx, string propertyId)
    {
        PropertyState owned = ctx.State.Properties[propertyId];
        if (owned.OwnerId is not int ownerId || owned.IsPending || owned.IsMortgaged || owned.Level > 0)
            return false;
        // A Police Station next door protects it (Section 12.3).
        if (!RuleModifiers.CanBeTargetedByCards(ctx.State, ctx.Content, propertyId))
            return false;
        PropertyDefinition def = ctx.Content.Property(propertyId);
        return def.Group == null || !ctx.Content.GroupMembers(def.Group).All(id => ctx.State.Properties[id].IsOwnedOutright(ownerId));
    }

    private static int MaxDepositBid(RulesConfig rules, int cash)
    {
        // Start from a guess that is a little too high, then step down until the deposit fits.
        int bid = MaxFullBid(rules, cash * 100 / Math.Max(1, rules.DownPaymentPercent) + rules.BidIncrement);
        while (bid > 0 && Deposit(rules, bid) > cash)
            bid -= rules.BidIncrement;
        return bid;
    }

    /// <summary>Could this player reach the minimum bid if they took every loan they still can?</summary>
    private bool CouldEverBid(EngineContext ctx, int playerId)
    {
        PlayerState player = ctx.Player(playerId);
        int loansLeft = Math.Max(0, MoneyRules.MaxLoans(ctx, playerId) - player.Loans);
        int bestCash = player.Cash + loansLeft * ctx.Rules.LoanAmount;
        int best = MaxFullBid(ctx.Rules, bestCash);
        if (AllowsDownPayment && HasFreePendingSlot(ctx, playerId))
            best = Math.Max(best, MaxDepositBid(ctx.Rules, bestCash));
        return best >= MinBid;
    }

    public override IReadOnlyList<GameAction> LegalActions(EngineContext ctx, PendingDecision decision)
    {
        // Any multiple of the bid step from MinBid up to the max is legal too (see IsLegal).
        var actions = new List<GameAction> { new PassBid() };
        if (decision.MaxBid >= decision.MinBid)
            actions.Add(new PlaceBid(decision.MinBid));
        else if (decision.MaxDownPaymentBid >= decision.MinBid)
            actions.Add(new PlaceBid(decision.MinBid, DownPayment: true));
        return actions;
    }

    public override bool IsLegal(EngineContext ctx, PendingDecision decision, GameAction action)
    {
        if (action is PassBid)
            return true;
        if (action is not PlaceBid bid || bid.Amount < MinBid || bid.Amount % ctx.Rules.BidIncrement != 0)
            return false;

        // Check against the player's cash right now, not when the decision was
        // made, because they may have taken a loan since.
        int cash = ctx.Player(decision.PlayerId).Cash;
        return bid.DownPayment
            ? AllowsDownPayment && HasFreePendingSlot(ctx, decision.PlayerId) && Deposit(ctx.Rules, bid.Amount) <= cash
            : bid.Amount <= cash;
    }

    public override void Receive(EngineContext ctx, PendingDecision decision, GameAction action)
    {
        Bids[decision.PlayerId] = action is PlaceBid bid ? bid.Amount : null;
        if (action is PlaceBid { DownPayment: true })
            DownPaymentBidders.Add(decision.PlayerId);
    }

    private void Resolve(EngineContext ctx)
    {
        // Reveal every bid at the same moment, in turn order.
        ctx.Emit(new BidsRevealed(PropertyId, Bidders.Select(id => new RevealedBid(id, Bids[id])).ToList()));

        var realBids = Bids.Where(b => b.Value != null).ToList();
        if (realBids.Count == 0)
        {
            // Nobody bid: the property stays with the bank (Section 8.2, rule 7),
            // or in a forced auction, with its owner (Section 8.6).
            if (Kind == AuctionKind.Forced)
                ctx.Emit(new OwnerKeptProperty(PropertyId, OwnerId!.Value));
            else
                ctx.Emit(new AuctionNoBids(PropertyId));
            return;
        }

        int highest = realBids.Max(b => b.Value!.Value);
        int winnerId = realBids
            .Where(b => b.Value == highest)
            .Select(b => b.Key)
            .OrderBy(id => SeatsAfterTieBreakStart(ctx.State, id))
            .First();

        // The winner pays their own bid to the bank: all of it now, or just the
        // deposit now and the rest at their next GO (Section 8.5).
        PropertyState owned = ctx.State.Properties[PropertyId];
        int assessed = ctx.Content.Property(PropertyId).AssessedValue;
        if (Kind == AuctionKind.Forced)
        {
            // The owner may bid too. If they win, they pay the bank and keep it.
            // Otherwise the winner pays the owner (Section 8.6).
            int? paidTo = winnerId == OwnerId ? null : OwnerId;
            ctx.Player(winnerId).Cash -= highest;
            if (paidTo is int ownerId)
                ctx.Player(ownerId).Cash += highest;
            owned.OwnerId = winnerId;
            ctx.Emit(new AuctionWon(PropertyId, winnerId, highest, assessed, PaidNow: highest, PaidToId: paidTo));
            return;
        }
        owned.OwnerId = winnerId;
        if (DownPaymentBidders.Contains(winnerId))
        {
            int deposit = Deposit(ctx.Rules, highest);
            ctx.Player(winnerId).Cash -= deposit;
            owned.IsPending = true;
            owned.PendingBalance = highest - deposit;
            ctx.Emit(new AuctionWon(PropertyId, winnerId, highest, assessed, DownPayment: true, PaidNow: deposit));
        }
        else
        {
            ctx.Player(winnerId).Cash -= highest;
            ctx.Emit(new AuctionWon(PropertyId, winnerId, highest, assessed, PaidNow: highest));
        }
    }

    /// <summary>How many seats after the tie-break start player this player sits (0 = that player).</summary>
    private int SeatsAfterTieBreakStart(GameState state, int playerId)
    {
        int start = state.Players.FindIndex(p => p.Id == TieBreakStartId);
        int seat = state.Players.FindIndex(p => p.Id == playerId);
        return (seat - start + state.Players.Count) % state.Players.Count;
    }
}
