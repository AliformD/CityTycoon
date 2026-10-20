using CityTycoon.Engine.Actions;
using CityTycoon.Engine.Events;
using CityTycoon.Engine.Rules;

namespace CityTycoon.Engine.Steps;

/// <summary>Asks the other player to accept or decline a trade offer (Section 15.9).</summary>
public sealed class TradeResponseStep : Step
{
    public TradeOffer Offer { get; set; } = null!;

    /// <summary>Null until the other player answers.</summary>
    public bool? Accepted { get; set; }

    public override StepResult Run(EngineContext ctx)
    {
        if (Accepted == null)
        {
            string from = ctx.Player(Offer.FromId).Name;
            return StepResult.WaitFor(new PendingDecision(Offer.ToId, DecisionKind.RespondToTrade,
                $"{from} offers you a trade. Accept it?", Trade: Offer));
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

    public override void Receive(EngineContext ctx, PendingDecision decision, GameAction action) =>
        Accepted = action is AcceptTrade;
}
