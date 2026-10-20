using CityTycoon.Engine.Events;
using CityTycoon.Engine.State;
using CityTycoon.Engine.Steps;

namespace CityTycoon.Engine.Rules;

/// <summary>Moving pieces around the board (Section 7).</summary>
public static class Movement
{
    /// <summary>
    /// Moves a player forward one space at a time, so passing GO is noticed
    /// as it happens (Section 7.2, step 6). Landing on GO counts as passing it.
    /// </summary>
    public static void MoveForward(EngineContext ctx, PlayerState player, int spaces)
    {
        int from = player.Position;
        int goIndex = ctx.State.IndexOf("go");
        int position = from;
        int timesPassedGo = 0;
        for (int i = 0; i < spaces; i++)
        {
            position = (position + 1) % ctx.State.Board.Count;
            if (position == goIndex)
                timesPassedGo++;
        }

        player.Position = position;
        ctx.Emit(new PlayerMoved(player.Id, from, position));
        for (int i = 0; i < timesPassedGo; i++)
            CollectGo(ctx, player);
    }

    /// <summary>GO effects (Section 15.1). So far: the salary, a Momentum token, then any down-payment balances due.</summary>
    public static void CollectGo(EngineContext ctx, PlayerState player)
    {
        player.Cash += ctx.Rules.GoSalary;
        ctx.Emit(new PassedGo(player.Id, ctx.Rules.GoSalary));

        // Step 5: gain a Momentum token.
        MomentumRules.Gain(ctx, player, ctx.Rules.GoMomentumGain, "GO");

        // Step 6: pending balances are due, paid right after the salary (Section 8.5).
        // Pushed in reverse board order so the first one on the board is handled first.
        var pending = ctx.State.Properties.Values
            .Where(p => p.OwnerId == player.Id && p.IsPending)
            .OrderByDescending(p => ctx.State.IndexOf(p.PropertyId));
        foreach (PropertyState p in pending)
            ctx.Push(new DownPaymentDueStep { PlayerId = player.Id, PropertyId = p.PropertyId });
    }

    /// <summary>
    /// Sends a player straight to the Hospital without passing GO (Section 15.3).
    /// An Insured marker cancels the bill and the recovery turn and is used up.
    /// Otherwise they pay the bill and roll only 1 die on their next turn.
    /// Event cards that do this arrive in Phase 5.
    /// </summary>
    public static void SendToHospital(EngineContext ctx, PlayerState player, string reason)
    {
        player.Position = ctx.State.IndexOf("hospital");
        ctx.Emit(new SentToHospital(player.Id, reason));
        if (player.IsInsured)
        {
            player.IsInsured = false;
            ctx.Emit(new InsuranceUsed(player.Id));
            return;
        }

        player.IsRecovering = true;
        ctx.Push(new PaymentStep { FromId = player.Id, Amount = ctx.Rules.HospitalBill, Reason = PaymentReason.Hospital });
    }

    /// <summary>Sends a player straight to Jail without passing GO (Section 14.1).</summary>
    public static void SendToJail(EngineContext ctx, PlayerState player, string reason)
    {
        player.Position = ctx.State.IndexOf("jail");
        player.InJail = true;
        player.JailTurns = 0;
        player.DoublesInARow = 0;
        ctx.Emit(new SentToJail(player.Id, reason));
    }
}
