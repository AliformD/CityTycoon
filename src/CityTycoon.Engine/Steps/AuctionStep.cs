using CityTycoon.Engine.Actions;
using CityTycoon.Engine.Content;
using CityTycoon.Engine.Events;
using CityTycoon.Engine.State;

namespace CityTycoon.Engine.Steps;

/// <summary>Why an auction is happening. It decides the minimum bid (Section 8.3).</summary>
public enum AuctionKind
{
    /// <summary>Someone landed on an unowned property.</summary>
    Normal,

    /// <summary>A bankrupt player's property went back to the bank.</summary>
    Bankruptcy,
}

/// <summary>
/// A sealed-bid auction (Section 8.2). Everyone secretly bids or passes at the
/// same time, then all bids are revealed together and the highest bid wins.
/// Phase 1 supports "pay in full" bids. Down payments arrive in Phase 3.
/// </summary>
public sealed class AuctionStep : Step
{
    public string PropertyId { get; set; } = "";
    public AuctionKind Kind { get; set; }

    /// <summary>Ties go to this player first, then the next players in turn order (Section 8.2, rule 5).</summary>
    public int TieBreakStartId { get; set; }

    /// <summary>A player who may not bid (for example the bankrupt player whose property this was).</summary>
    public int? ExcludedPlayerId { get; set; }

    public bool Started { get; set; }
    public int MinBid { get; set; }

    /// <summary>Who may bid, in turn order.</summary>
    public List<int> Bidders { get; set; } = [];

    /// <summary>Sealed answers so far: player ID to bid amount (null means pass).</summary>
    public Dictionary<int, int?> Bids { get; set; } = [];

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
                PropertyId, MinBid, MaxBidFor(ctx, id), ctx.Rules.BidIncrement)));
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

        // Players who can't afford the minimum bid pass automatically, so nobody
        // is asked a question with only one answer (docs/DECISIONS.md).
        foreach (int id in Bidders.Where(id => ctx.Player(id).Cash < MinBid))
            Bids[id] = null;

        ctx.Emit(new AuctionStarted(PropertyId, MinBid));
    }

    /// <summary>The minimum bid, rounded up to the bid step (Section 8.3).</summary>
    public static int MinimumBid(RulesConfig rules, int assessedValue, AuctionKind kind)
    {
        int percent = rules.AuctionMinBidPercent; // Normal and bankruptcy auctions both use this.
        int raw = assessedValue * percent;          // still multiplied by 100 at this point
        int step = rules.BidIncrement * 100;
        int roundedUp = (raw + step - 1) / step;    // whole-number "round up" trick
        return roundedUp * rules.BidIncrement;
    }

    /// <summary>The highest bid a player can afford: their cash, rounded down to the bid step.</summary>
    private static int MaxBidFor(EngineContext ctx, int playerId)
    {
        int inc = ctx.Rules.BidIncrement;
        return ctx.Player(playerId).Cash / inc * inc;
    }

    public override IReadOnlyList<GameAction> LegalActions(EngineContext ctx, PendingDecision decision) =>
        // Any multiple of the bid step from MinBid to MaxBid is legal too (see IsLegal).
        decision.MaxBid >= decision.MinBid
            ? [new PassBid(), new PlaceBid(decision.MinBid)]
            : [new PassBid()];

    public override bool IsLegal(EngineContext ctx, PendingDecision decision, GameAction action) => action switch
    {
        PassBid => true,
        PlaceBid bid => bid.Amount >= MinBid
            && bid.Amount <= ctx.Player(decision.PlayerId).Cash
            && bid.Amount % ctx.Rules.BidIncrement == 0,
        _ => false,
    };

    public override void Receive(EngineContext ctx, PendingDecision decision, GameAction action) =>
        Bids[decision.PlayerId] = action is PlaceBid bid ? bid.Amount : null;

    private void Resolve(EngineContext ctx)
    {
        // Reveal every bid at the same moment, in turn order.
        ctx.Emit(new BidsRevealed(PropertyId, Bidders.Select(id => new RevealedBid(id, Bids[id])).ToList()));

        var realBids = Bids.Where(b => b.Value != null).ToList();
        if (realBids.Count == 0)
        {
            // Nobody bid: the property stays with the bank (Section 8.2, rule 7).
            ctx.Emit(new AuctionNoBids(PropertyId));
            return;
        }

        int highest = realBids.Max(b => b.Value!.Value);
        int winnerId = realBids
            .Where(b => b.Value == highest)
            .Select(b => b.Key)
            .OrderBy(id => SeatsAfterTieBreakStart(ctx.State, id))
            .First();

        // The winner pays their own bid to the bank.
        ctx.Player(winnerId).Cash -= highest;
        ctx.State.Properties[PropertyId].OwnerId = winnerId;
        ctx.Emit(new AuctionWon(PropertyId, winnerId, highest, ctx.Content.Property(PropertyId).AssessedValue));
    }

    /// <summary>How many seats after the tie-break start player this player sits (0 = that player).</summary>
    private int SeatsAfterTieBreakStart(GameState state, int playerId)
    {
        int start = state.Players.FindIndex(p => p.Id == TieBreakStartId);
        int seat = state.Players.FindIndex(p => p.Id == playerId);
        return (seat - start + state.Players.Count) % state.Players.Count;
    }
}
