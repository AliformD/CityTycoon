using CityTycoon.Engine.Events;
using CityTycoon.Engine.State;

namespace CityTycoon.Engine.Steps;

public enum PaymentReason { Rent, Tax, Bail }

/// <summary>
/// One player pays money to another player or to the bank. If they can't
/// afford it, they go bankrupt (Section 15.10).
///
/// Phase 1 has no mortgages, loans or buildings to sell, so "can't pay with
/// cash" means bankrupt. Phase 3 adds a raise-money step before that.
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

    public override StepResult Run(EngineContext ctx)
    {
        PlayerState payer = ctx.Player(FromId);
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

        DeclareBankrupt(ctx, payer, ToId);
        return StepResult.Done;
    }

    /// <summary>The player is out of the game. Their stuff goes to whoever they owed (Section 15.10).</summary>
    public static void DeclareBankrupt(EngineContext ctx, PlayerState player, int? creditorId)
    {
        var properties = ctx.State.Properties.Values.Where(p => p.OwnerId == player.Id).ToList();
        int cash = player.Cash;

        if (creditorId is int toId)
        {
            // Owed a player: they get the remaining cash and every property, still mortgaged where it was.
            ctx.Player(toId).Cash += cash;
            foreach (PropertyState p in properties)
                p.OwnerId = toId;
        }
        else
        {
            // Owed the bank: every property goes back to the bank and is auctioned.
            // Steps are pushed in reverse board order, so the first one on the board is auctioned first.
            var inBoardOrder = properties.OrderBy(p => ctx.State.IndexOf(p.PropertyId)).ToList();
            foreach (PropertyState p in inBoardOrder)
            {
                p.OwnerId = null;
                p.IsMortgaged = false;
                p.Level = 0;
            }
            for (int i = inBoardOrder.Count - 1; i >= 0; i--)
            {
                ctx.Push(new AuctionStep
                {
                    PropertyId = inBoardOrder[i].PropertyId,
                    Kind = AuctionKind.Bankruptcy,
                    TieBreakStartId = ctx.State.ActivePlayer.Id,
                    ExcludedPlayerId = player.Id,
                });
            }
        }

        player.Cash = 0;
        player.IsBankrupt = true;
        player.InJail = false;
        ctx.Emit(new PlayerBankrupt(player.Id, creditorId, cash, properties.Select(p => p.PropertyId).ToList()));
    }
}
