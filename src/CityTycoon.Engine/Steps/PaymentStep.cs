using CityTycoon.Engine.Actions;
using CityTycoon.Engine.Cards;
using CityTycoon.Engine.Content;
using CityTycoon.Engine.Events;
using CityTycoon.Engine.Rules;
using CityTycoon.Engine.State;

namespace CityTycoon.Engine.Steps;

public enum PaymentReason { Rent, Tax, Bail, Hospital, Card, Tile }

/// <summary>
/// One player pays money to another player or to the bank.
///
/// If they don't have the cash, they must raise it first by selling buildings,
/// mortgaging, or taking loans (the "raise money" flow). Only if even
/// everything they could raise isn't enough do they go bankrupt (Section 15.10).
/// </summary>
public sealed class PaymentStep : Step
{
    public int FromId { get; set; }

    /// <summary>The player being paid, or null for the bank.</summary>
    public int? ToId { get; set; }

    public int Amount { get; set; }
    public PaymentReason Reason { get; set; }

    /// <summary>The property (for rent) or tax space this payment is for.</summary>
    public string? SpaceId { get; set; }

    /// <summary>For card payments: which card made this happen.</summary>
    public string? CardId { get; set; }

    /// <summary>For Government tile fees: which tile (Police Station, Landfill...).</summary>
    public string? TileId { get; set; }

    /// <summary>For rent: the Rent Strike question, once it has been opened.</summary>
    public ReactionWindow? RentStrike { get; set; }

    /// <summary>True once the "you need to raise money" message has been logged.</summary>
    public bool AnnouncedShortfall { get; set; }

    public bool PlayedRentStrike { get; set; }

    public override StepResult Run(EngineContext ctx)
    {
        PlayerState payer = ctx.Player(FromId);
        if (payer.IsBankrupt)
            return StepResult.Done;

        // Owing rent opens a Reaction window for Rent Strike (Section 15.4).
        if (Reason == PaymentReason.Rent)
        {
            RentStrike ??= ReactionWindow.Open(ctx, CardTrigger.RentOwed, [FromId]);
            if (RentStrike.Decision(ctx, $"You owe ${Amount} rent for {ctx.Content.Property(SpaceId!).Name}. Play a card?") is { } ask)
                return StepResult.WaitFor(ask with { AmountOwed = Amount, PropertyId = SpaceId });
            if (RentStrike.Played is { } play && !PlayedRentStrike)
            {
                // The card's effect runs on top of this step (the owner may cancel it with Insurance), then this step pays.
                PlayedRentStrike = true;
                ctx.Push(new CardPlayStep { PlayerId = FromId, Play = play });
                return StepResult.Continue;
            }
        }

        if (payer.Cash >= Amount)
        {
            payer.Cash -= Amount;
            if (ToId is int toId)
                ctx.Player(toId).Cash += Amount;

            ctx.Emit(Reason switch
            {
                PaymentReason.Rent => new RentPaid(FromId, ToId!.Value, SpaceId!, Amount),
                PaymentReason.Tax => new TaxPaid(FromId, SpaceId!, Amount),
                PaymentReason.Hospital => new HospitalBillPaid(FromId, Amount),
                PaymentReason.Card => new CardPaymentMade(FromId, ToId, Amount, CardId!),
                PaymentReason.Tile => new TileFeePaid(FromId, ToId, Amount, TileId!),
                _ => new BailPaid(FromId, Amount),
            });
            return StepResult.Done;
        }

        if (MoneyRules.MaxRaisableCash(ctx, FromId) < Amount)
        {
            Bankruptcy.Declare(ctx, payer, ToId);
            return StepResult.Done;
        }

        // They can raise enough: ask them to sell, mortgage or borrow, then try again.
        if (!AnnouncedShortfall)
        {
            AnnouncedShortfall = true;
            ctx.Emit(new RaisingMoney(FromId, Amount));
        }
        return StepResult.WaitFor(new PendingDecision(FromId, DecisionKind.RaiseMoney,
            $"You owe ${Amount} but have ${payer.Cash}. Sell buildings, mortgage, or take a loan to raise the rest.",
            SpaceId, Management: ManagementOptions.RaiseMoney, AmountOwed: Amount));
    }

    public override IReadOnlyList<GameAction> LegalActions(EngineContext ctx, PendingDecision decision) =>
        decision.Kind == DecisionKind.PlayReaction ? RentStrike!.LegalActions(ctx, decision) : [];

    public override void Receive(EngineContext ctx, PendingDecision decision, GameAction action) =>
        RentStrike!.Receive(ctx, decision, action);
}
