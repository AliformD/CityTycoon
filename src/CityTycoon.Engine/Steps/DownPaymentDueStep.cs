using CityTycoon.Engine.Actions;
using CityTycoon.Engine.Events;
using CityTycoon.Engine.Rules;
using CityTycoon.Engine.State;

namespace CityTycoon.Engine.Steps;

/// <summary>
/// A pending property's balance is due because its owner passed GO (Section 8.5).
/// Pay it from cash; if that's not enough, the owner may raise money or give
/// the property up. A given-up ("forfeited") property is re-auctioned at once,
/// the deposit is lost, and the old owner can't bid.
/// </summary>
public sealed class DownPaymentDueStep : Step
{
    public int PlayerId { get; set; }
    public string PropertyId { get; set; } = "";

    /// <summary>True once the owner chooses to give the property up.</summary>
    public bool ChoseToForfeit { get; set; }

    public override StepResult Run(EngineContext ctx)
    {
        PlayerState player = ctx.Player(PlayerId);
        PropertyState owned = ctx.State.Properties[PropertyId];
        if (player.IsBankrupt || owned.OwnerId != PlayerId || !owned.IsPending)
            return StepResult.Done;

        int balance = owned.PendingBalance;
        if (!ChoseToForfeit && player.Cash >= balance)
        {
            player.Cash -= balance;
            owned.IsPending = false;
            owned.PendingBalance = 0;
            ctx.Emit(new DownPaymentPaid(PlayerId, PropertyId, balance));
            return StepResult.Done;
        }

        if (!ChoseToForfeit && MoneyRules.MaxRaisableCash(ctx, PlayerId) >= balance)
        {
            string name = ctx.Content.Property(PropertyId).Name;
            return StepResult.WaitFor(new PendingDecision(PlayerId, DecisionKind.RaiseMoney,
                $"The ${balance} balance on {name} is due. Raise the money, or give the property up (you lose your deposit).",
                PropertyId, Management: ManagementOptions.RaiseMoney, AmountOwed: balance));
        }

        Forfeit(ctx);
        return StepResult.Done;
    }

    private void Forfeit(EngineContext ctx)
    {
        PropertyState owned = ctx.State.Properties[PropertyId];
        owned.OwnerId = null;
        owned.IsPending = false;
        owned.PendingBalance = 0;
        ctx.Emit(new PropertyForfeited(PlayerId, PropertyId));
        ctx.Push(new AuctionStep
        {
            PropertyId = PropertyId,
            Kind = AuctionKind.Forfeit,
            TieBreakStartId = ctx.State.ActivePlayer.Id,
            ExcludedPlayerId = PlayerId,
        });
    }

    public override IReadOnlyList<GameAction> LegalActions(EngineContext ctx, PendingDecision decision) => [new ForfeitProperty()];

    public override void Receive(EngineContext ctx, PendingDecision decision, GameAction action) => ChoseToForfeit = true;
}
