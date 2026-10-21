using CityTycoon.Engine.Actions;
using CityTycoon.Engine.Events;
using CityTycoon.Engine.Rules;

namespace CityTycoon.Engine.Steps;

/// <summary>
/// Asks the other player to accept, decline or counter a trade offer (Section 15.9).
/// A counter-offer swaps who is answering: this step keeps the newest offer and
/// asks again, until someone accepts or declines, or the counters run out.
/// </summary>
public sealed class TradeResponseStep : Step
{
    /// <summary>The offer waiting for an answer. After a counter, this is the counter.</summary>
    public TradeOffer Offer { get; set; } = null!;

    /// <summary>Null until the other player answers.</summary>
    public bool? Accepted { get; set; }

    /// <summary>A counter-offer that was just sent, waiting to be put in place by <see cref="Run"/>.</summary>
    public TradeOffer? Counter { get; set; }

    /// <summary>How many counter-offers this trade has had so far (limited by maxTradeCounters).</summary>
    public int Counters { get; set; }

    public override StepResult Run(EngineContext ctx)
    {
        if (Counter is TradeOffer counter)
        {
            ctx.Emit(new TradeCountered(Offer, counter));
            Offer = counter;
            Counter = null;
            Counters++;
        }

        if (Accepted == null)
        {
            string from = ctx.Player(Offer.FromId).Name;
            int left = Math.Max(0, ctx.Rules.MaxTradeCounters - Counters);
            string prompt = Counters == 0 ? $"{from} offers you a trade. Accept it?" : $"{from} sent a counter-offer. Accept it?";
            return StepResult.WaitFor(new PendingDecision(Offer.ToId, DecisionKind.RespondToTrade,
                prompt, Trade: Offer, CountersLeft: left));
        }

        if (Accepted == false)
        {
            ctx.Emit(new TradeDeclined(Offer, "declined"));
        }
        else if (TradeRules.WhyInvalid(ctx, Offer) is string reason)
        {
            ctx.Emit(new TradeDeclined(Offer, reason));
        }
        else
        {
            TradeRules.Execute(ctx, Offer);
        }
        return StepResult.Done;
    }

    public override IReadOnlyList<GameAction> LegalActions(EngineContext ctx, PendingDecision decision) =>
        [new AcceptTrade(), new DeclineTrade()];

    /// <summary>Counter-offers can't all be listed (any mix of cash and property), so they are checked here.</summary>
    public override bool IsLegal(EngineContext ctx, PendingDecision decision, GameAction action) =>
        action is CounterTrade counter
            ? TradeRules.WhyBadCounter(ctx, Offer, counter.Offer, Counters) == null
            : base.IsLegal(ctx, decision, action);

    public override void Receive(EngineContext ctx, PendingDecision decision, GameAction action)
    {
        if (action is CounterTrade counter)
            Counter = counter.Offer;
        else
            Accepted = action is AcceptTrade;
    }
}
