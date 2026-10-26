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

    /// <summary>First-lap house rule: only the player who landed is asked, and the price is the listed one. Saying no starts a normal auction.</summary>
    FixedPrice,
}

/// <summary>
/// An auction for one property. Usually it is a sealed-bid auction (Section 8.2), but when someone
/// lands on a color property whose group already has an owner, it is an open auction instead
/// (docs/DECISIONS.md #91): players take turns to raise or drop out.
///
/// Sealed-bid: Everyone secretly bids or passes at the
/// same time, then all bids are revealed together and the highest bid wins.
/// Each bid is either "pay in full" or a down payment (Section 8.4 and 8.5). Open auctions are pay in full only (decision #163).
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

    /// <summary>The player who started a Hostile Takeover: they must bid at least the minimum and can't pass (decision #166).</summary>
    public int? MustBidId { get; set; }

    public bool Started { get; set; }
    public int MinBid { get; set; }

    /// <summary>True for an open (ascending) auction, where players take turns to raise or drop out.</summary>
    public bool Open { get; set; }

    /// <summary>Open auction: players who have dropped out (or couldn't afford to go on).</summary>
    public HashSet<int> Out { get; set; } = [];

    /// <summary>Open auction: the highest bid so far, who made it, and how they will pay.</summary>
    public int HighBid { get; set; }
    public int? HighBidderId { get; set; }
    public bool HighBidIsDownPayment { get; set; }

    /// <summary>Open auction: the position in <see cref="Bidders"/> of the player whose turn it is.</summary>
    public int Turn { get; set; }

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
        if (Open)
            return RunOpen(ctx);

        var waitingOn = Bidders.Where(id => !Bids.ContainsKey(id)).ToList();
        if (waitingOn.Count > 0)
        {
            string name = ctx.Content.Property(PropertyId).Name;
            if (Kind == AuctionKind.FixedPrice)
                return StepResult.WaitFor(new PendingDecision(
                    TieBreakStartId, DecisionKind.BuyAtListedPrice,
                    $"First lap: buy {name} for its listed price of ${MinBid}? If you say no, everyone bids for it.",
                    PropertyId, MinBid, MaxBidFor(ctx, TieBreakStartId), ctx.Rules.BidIncrement,
                    Management: ManagementOptions.TakeLoan));
            return StepResult.WaitFor(waitingOn.Select(id => new PendingDecision(
                id, DecisionKind.SubmitBid, $"Silent auction for {name}: bid in secret (minimum ${MinBid}) or pass.",
                PropertyId, MinBid, MaxBidFor(ctx, id), ctx.Rules.BidIncrement,
                MaxDownPaymentBid: MaxDownPaymentBidFor(ctx, id),
                // Taking a loan is the only other thing you can do mid-auction (Section 8.4).
                Management: ManagementOptions.TakeLoan,
                MustBid: id == MustBidId)));
        }

        Resolve(ctx);
        return StepResult.Done;
    }

    private void Start(EngineContext ctx)
    {
        Started = true;
        PropertyDefinition def = ctx.Content.Property(PropertyId);
        MinBid = MinimumBid(ctx.Rules, def.AssessedValue, Kind);

        if (Kind == AuctionKind.FixedPrice)
        {
            MinBid = def.AssessedValue;
            Bidders = [TieBreakStartId];
            if (!CouldEverBid(ctx, TieBreakStartId))
                Bids[TieBreakStartId] = null;
            ctx.Emit(new AuctionStarted(PropertyId, MinBid, FixedPrice: true));
            return;
        }

        // Every player who isn't bankrupt may bid, including players in Jail.
        Bidders = ctx.State.ActivePlayers.Where(p => p.Id != ExcludedPlayerId).Select(p => p.Id).ToList();

        Open = ShouldBeOpen(ctx, Kind, PropertyId);
        if (Open)
        {
            // The player who landed bids first, then the others in turn order.
            int first = Bidders.IndexOf(TieBreakStartId);
            Bidders = Bidders.Skip(Math.Max(first, 0)).Concat(Bidders.Take(Math.Max(first, 0))).ToList();
            ctx.Emit(new AuctionStarted(PropertyId, MinBid, Open: true));
            return;
        }

        // Players who can't reach the minimum bid, even with every loan they
        // could still take and a down payment, pass automatically. That way
        // nobody is asked a question with only one answer (docs/DECISIONS.md).
        foreach (int id in Bidders.Where(id => !CouldEverBid(ctx, id)))
            Bids[id] = null;

        ctx.Emit(new AuctionStarted(PropertyId, MinBid));
    }

    /// <summary>
    /// Open or sealed? The first property of a set to be sold is sealed-bid; once any other
    /// property of that set has an owner, the rest are open auctions. A set is a color group,
    /// all the railroads, or both utilities. Only a landing auction can be open; the other kinds
    /// of auction stay sealed (docs/DECISIONS.md #91).
    /// </summary>
    public static bool ShouldBeOpen(EngineContext ctx, AuctionKind kind, string propertyId)
    {
        if (kind != AuctionKind.Normal)
            return false;
        var others = SetMates(ctx, propertyId);
        bool anyOwned = others.Any(p => ctx.State.Properties[p.Id].OwnerId != null);
        bool lastPiece = others.All(p => ctx.State.Properties[p.Id].OwnerId != null);
        return ctx.Rules.SilentAuctionFor switch
        {
            "lastOfSet" => !lastPiece,
            "firstAndLast" => anyOwned && !lastPiece,
            _ => anyOwned,
        };
    }

    /// <summary>The other properties in this property's set (its color group, the other railroads, or the other utility).</summary>
    private static List<PropertyDefinition> SetMates(EngineContext ctx, string propertyId)
    {
        PropertyDefinition def = ctx.Content.Property(propertyId);
        return ctx.Content.Properties.Values
            .Where(p => p.Id != propertyId && (def.Group != null ? p.Group == def.Group : p.Group == null && p.Type == def.Type))
            .ToList();
    }

    /// <summary>True when every other property of this one's set already has an owner.</summary>
    public static bool IsLastPiece(EngineContext ctx, string propertyId) =>
        SetMates(ctx, propertyId).All(p => ctx.State.Properties[p.Id].OwnerId != null);

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
        AllowsDownPayment(ctx) && HasFreePendingSlot(ctx, playerId) ? MaxDepositBid(ctx.Rules, ctx.Player(playerId).Cash) : 0;

    /// <summary>
    /// Forced auctions are paid in full: the money goes straight to the owner (docs/DECISIONS.md).
    /// Open auctions are paid in full too, unless the house rule allows down payments there (decision #163),
    /// so only cash and loans count when someone is racing to finish a color group.
    /// </summary>
    private bool AllowsDownPayment(EngineContext ctx) => Kind != AuctionKind.Forced && Kind != AuctionKind.FixedPrice
        && (!Open || ctx.Rules.OpenAuctionDownPayments)
        && (Open || Kind != AuctionKind.Normal || ctx.Rules.LastPieceDownPayments || !IsLastPiece(ctx, PropertyId));

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
        // Hostile Bid and Hostile Takeover are both negative: a Police Station blocks them.
        if (!RuleModifiers.CanBeTargetedByCards(ctx.State, ctx.Content, propertyId, CardPolarity.Negative))
            return false;
        PropertyDefinition def = ctx.Content.Property(propertyId);
        return def.Group == null || !ctx.Content.GroupMembers(def.Group).All(id => ctx.State.Properties[id].IsOwnedOutright(ownerId));
    }

    /// <summary>
    /// Can this player use Hostile Takeover on this property? They need the cash for the minimum bid right now,
    /// because their own bid can't be a pass (decision #166). They can take a loan first.
    /// </summary>
    public static bool CanAffordTakeover(EngineContext ctx, int playerId, string propertyId) =>
        MaxFullBid(ctx.Rules, ctx.Player(playerId).Cash)
            >= MinimumBid(ctx.Rules, ctx.Content.Property(propertyId).AssessedValue, AuctionKind.Forced);

    private static int MaxDepositBid(RulesConfig rules, int cash)
    {
        // Start from a guess that is a little too high, then step down until the deposit fits.
        int bid = MaxFullBid(rules, cash * 100 / Math.Max(1, rules.DownPaymentPercent) + rules.BidIncrement);
        while (bid > 0 && Deposit(rules, bid) > cash)
            bid -= rules.BidIncrement;
        return bid;
    }

    /// <summary>Could this player reach the minimum bid if they took every loan they still can?</summary>
    private bool CouldEverBid(EngineContext ctx, int playerId) => CouldBid(ctx, playerId, MinBid);

    private bool CouldBid(EngineContext ctx, int playerId, int minimum)
    {
        PlayerState player = ctx.Player(playerId);
        int loansLeft = Math.Max(0, MoneyRules.MaxLoans(ctx, playerId) - player.Loans);
        int bestCash = player.Cash + loansLeft * ctx.Rules.BiggestLoan;
        int best = MaxFullBid(ctx.Rules, bestCash);
        if (AllowsDownPayment(ctx) && HasFreePendingSlot(ctx, playerId))
            best = Math.Max(best, MaxDepositBid(ctx.Rules, bestCash));
        return best >= minimum;
    }

    public override IReadOnlyList<GameAction> LegalActions(EngineContext ctx, PendingDecision decision)
    {
        if (decision.Kind == DecisionKind.BuyAtListedPrice)
            return ctx.Player(decision.PlayerId).Cash >= MinBid ? [new BuyAtListedPrice(), new NoThanks()] : [new NoThanks()];
        // Any multiple of the bid step from MinBid up to the max is legal too (see IsLegal).
        var actions = new List<GameAction>();
        if (!decision.MustBid)
            actions.Add(new PassBid());
        if (decision.MaxBid >= decision.MinBid)
            actions.Add(new PlaceBid(decision.MinBid));
        else if (decision.MaxDownPaymentBid >= decision.MinBid)
            actions.Add(new PlaceBid(decision.MinBid, DownPayment: true));
        return actions;
    }

    public override bool IsLegal(EngineContext ctx, PendingDecision decision, GameAction action)
    {
        if (decision.Kind == DecisionKind.BuyAtListedPrice)
            // Checked against cash right now: the player may have taken a loan since the question was asked.
            return action is NoThanks || (action is BuyAtListedPrice && ctx.Player(decision.PlayerId).Cash >= MinBid);
        if (action is PassBid)
            return !decision.MustBid;
        if (action is not PlaceBid bid || bid.Amount < decision.MinBid || bid.Amount % ctx.Rules.BidIncrement != 0)
            return false;

        // Check against the player's cash right now, not when the decision was
        // made, because they may have taken a loan since.
        int cash = ctx.Player(decision.PlayerId).Cash;
        return bid.DownPayment
            ? AllowsDownPayment(ctx) && HasFreePendingSlot(ctx, decision.PlayerId) && Deposit(ctx.Rules, bid.Amount) <= cash
            : bid.Amount <= cash;
    }

    public override void Receive(EngineContext ctx, PendingDecision decision, GameAction action)
    {
        if (Open)
        {
            ReceiveOpen(ctx, decision, action);
            return;
        }
        if (decision.Kind == DecisionKind.BuyAtListedPrice)
        {
            Bids[decision.PlayerId] = action is BuyAtListedPrice ? MinBid : null;
            return;
        }
        Bids[decision.PlayerId] = action is PlaceBid bid ? bid.Amount : null;
        if (action is PlaceBid { DownPayment: true })
            DownPaymentBidders.Add(decision.PlayerId);
    }

    private void Resolve(EngineContext ctx)
    {
        // Reveal every bid at the same moment, in turn order.
        ctx.Emit(new BidsRevealed(PropertyId, Bidders.Select(id => new RevealedBid(id, Bids[id])).ToList()));

        var realBids = Bids.Where(b => b.Value != null).ToList();
        if (Kind == AuctionKind.FixedPrice)
        {
            // Yes: they pay the listed price. No: everyone gets to bid for it, by the usual auction rules.
            if (realBids.Count > 0)
                Award(ctx, TieBreakStartId, MinBid);
            else
                ctx.Push(new AuctionStep { PropertyId = PropertyId, Kind = AuctionKind.Normal, TieBreakStartId = TieBreakStartId });
            return;
        }
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

        // Bids are compared with each bidder's bonus added (the Mogul's counts $10 higher), but
        // everyone pays the bid they actually made. If the compared bids tie, the bidder with
        // the bonus wins, then the order from Section 8.2.
        int topBid = realBids.Max(b => b.Value!.Value);
        var bonus = realBids.ToDictionary(b => b.Key, b => RuleModifiers.BidBonus(ctx.State, ctx.Content, b.Key));
        int topCompared = realBids.Max(b => b.Value!.Value + bonus[b.Key]);
        int winnerId = realBids
            .Where(b => b.Value!.Value + bonus[b.Key] == topCompared)
            .Select(b => b.Key)
            .OrderByDescending(id => bonus[id] > 0)
            .ThenBy(id => SeatsAfterTieBreakStart(ctx.State, id))
            .First();
        int highest = Bids[winnerId]!.Value;   // what the winner pays
        if (highest < topBid)
            ctx.Emit(new BidEdgeWon(winnerId, PropertyId, highest, topBid));

        Award(ctx, winnerId, highest);
    }

    /// <summary>
    /// The winner pays their own bid to the bank: all of it now, or just the
    /// deposit now and the rest at their next GO (Section 8.5).
    /// </summary>
    private void Award(EngineContext ctx, int winnerId, int highest)
    {
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
            ctx.Emit(new AuctionWon(PropertyId, winnerId, highest, assessed, PaidNow: highest, ListedPrice: Kind == AuctionKind.FixedPrice));
        }
    }

    // ----- Open auction -----

    /// <summary>The lowest bid that is allowed next: the opening bid, or a step above the highest bid.</summary>
    private int CurrentMin(EngineContext ctx) => HighBidderId == null ? MinBid : HighBid + ctx.Rules.BidIncrement;

    private StepResult RunOpen(EngineContext ctx)
    {
        while (true)
        {
            var contenders = Bidders.Where(id => !Out.Contains(id)).ToList();
            if (HighBidderId is int leader)
            {
                if (contenders.All(id => id == leader))
                {
                    if (HighBidIsDownPayment)
                        DownPaymentBidders.Add(leader);
                    Award(ctx, leader, HighBid);
                    return StepResult.Done;
                }
            }
            else if (contenders.Count == 0)
            {
                ctx.Emit(new AuctionNoBids(PropertyId));
                return StepResult.Done;
            }

            // The next player still in who isn't already winning.
            while (Out.Contains(Bidders[Turn % Bidders.Count]) || Bidders[Turn % Bidders.Count] == HighBidderId)
                Turn++;
            int id = Bidders[Turn % Bidders.Count];

            // Someone who couldn't reach the next bid, even with every loan, drops out quietly.
            if (!CouldBid(ctx, id, CurrentMin(ctx)))
            {
                Out.Add(id);
                ctx.Emit(new AuctionPassed(PropertyId, id));
                continue;
            }

            string name = ctx.Content.Property(PropertyId).Name;
            string state = HighBidderId is int h ? $"{ctx.Player(h).Name} leads with ${HighBid}." : "Nobody has bid yet.";
            return StepResult.WaitFor(new PendingDecision(
                id, DecisionKind.SubmitBid, $"Open auction for {name}. {state} Raise to at least ${CurrentMin(ctx)}, or drop out.",
                PropertyId, CurrentMin(ctx), MaxBidFor(ctx, id), ctx.Rules.BidIncrement,
                MaxDownPaymentBid: MaxDownPaymentBidFor(ctx, id),
                Management: ManagementOptions.TakeLoan,
                OpenAuction: true, HighBidderId: HighBidderId, HighBid: HighBid,
                Bidders: Bidders.ToList(), DroppedOut: Bidders.Where(Out.Contains).ToList()));
        }
    }

    private void ReceiveOpen(EngineContext ctx, PendingDecision decision, GameAction action)
    {
        int id = decision.PlayerId;
        if (action is PlaceBid bid)
        {
            HighBid = bid.Amount;
            HighBidderId = id;
            HighBidIsDownPayment = bid.DownPayment;
            ctx.Emit(new BidRaised(PropertyId, id, bid.Amount, bid.DownPayment));
        }
        else
        {
            Out.Add(id);
            ctx.Emit(new AuctionPassed(PropertyId, id));
        }
        Turn++;
    }

    /// <summary>How many seats after the tie-break start player this player sits (0 = that player).</summary>
    private int SeatsAfterTieBreakStart(GameState state, int playerId)
    {
        int start = state.Players.FindIndex(p => p.Id == TieBreakStartId);
        int seat = state.Players.FindIndex(p => p.Id == playerId);
        return (seat - start + state.Players.Count) % state.Players.Count;
    }
}
