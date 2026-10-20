using CityTycoon.Engine.Actions;
using CityTycoon.Engine.Events;
using CityTycoon.Engine.Rules;
using CityTycoon.Engine.State;

namespace CityTycoon.Engine.Steps;

public enum PaymentReason { Rent, Tax, Bail }

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

    /// <summary>True once the "you need to raise money" message has been logged.</summary>
    public bool AnnouncedShortfall { get; set; }

    public override StepResult Run(EngineContext ctx)
    {
        PlayerState payer = ctx.Player(FromId);
        if (payer.IsBankrupt)
            return StepResult.Done;

        if (payer.Cash >= Amount)
        {
            payer.Cash -= Amount;
            if (ToId is int toId)
                ctx.Player(toId).Cash += Amount;

            ctx.Emit(Reason switch
            {
                PaymentReason.Rent => new RentPaid(FromId, ToId!.Value, SpaceId!, Amount),
                PaymentReason.Tax => new TaxPaid(FromId, SpaceId!, Amount),
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
}
