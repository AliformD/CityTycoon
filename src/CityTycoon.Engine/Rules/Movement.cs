using CityTycoon.Engine.Events;
using CityTycoon.Engine.Modifiers;
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

    /// <summary>
    /// Moves a player backwards (Section 7.4). Going back across GO never
    /// collects GO, and the space they end on isn't resolved (Rush Hour).
    /// </summary>
    public static void MoveBackward(EngineContext ctx, PlayerState player, int spaces)
    {
        int from = player.Position;
        int n = ctx.State.Board.Count;
        player.Position = ((from - spaces) % n + n) % n;
        ctx.Emit(new PlayerMoved(player.Id, from, player.Position));
    }

    /// <summary>
    /// GO effects, in the order of Section 15.1: the salary, Utility Bill, Community Roots,
    /// Momentum tokens, down-payment balances due, the City Planner's peek, and last of all
    /// the Red perk's Hostile Takeover becoming available again. and the catch-up bonus for the player furthest behind.
    /// </summary>
    public static void CollectGo(EngineContext ctx, PlayerState player)
    {
        // Step 1: the salary.
        player.Cash += ctx.Rules.GoSalary;
        ctx.Emit(new PassedGo(player.Id, ctx.Rules.GoSalary));

        // Step 2: Utility Bill. An opponent who owns both utilities takes $20 out of the salary.
        foreach (var (payeeId, amount, source) in RuleModifiers.GoTolls(ctx.State, ctx.Content, player.Id))
        {
            player.Cash -= amount;
            ctx.Player(payeeId).Cash += amount;
            ctx.Emit(new PerkPaid(player.Id, payeeId, amount, source));
        }

        // Step 3: Community Roots and any other cash perk.
        foreach (var (amount, source) in RuleModifiers.GoCashes(ctx.State, ctx.Content, player.Id))
        {
            player.Cash += amount;
            ctx.Emit(new PerkIncome(player.Id, amount, source));
        }

        // Step 4: catch-up bonus. Whoever has the lowest net worth (ties all count) gets $100 (Section 16.2).
        if (IsLowestNetWorth(ctx, player))
        {
            player.Cash += ctx.Rules.CatchUpBonus;
            ctx.Emit(new CatchUpPaid(player.Id, ctx.Rules.CatchUpBonus));
        }

        // Step 5: Momentum tokens. The base rule gives none now (decision #140), but Commuter Belt still gives 1.
        MomentumRules.Gain(ctx, player, ctx.Rules.GoMomentumGain, "GO");
        foreach (var (amount, source) in RuleModifiers.GoMomentums(ctx.State, ctx.Content, player.Id))
            MomentumRules.Gain(ctx, player, amount, source);

        // Step 7: the City Planner peeks. It is pushed before the down payments, so it runs after them (step 6 comes first).
        for (int i = 0; i < RuleModifiers.GoPeeks(ctx.State, ctx.Content, player.Id); i++)
            ctx.Push(new PeekTileStep { PlayerId = player.Id });

        // Step 8: the Red perk's Hostile Takeover is available again.
        player.TakeoverUsed = false;

        // Step 6: pending balances are due, paid right after the salary (Section 8.5).
        // Pushed in reverse board order so the first one on the board is handled first.
        var pending = ctx.State.Properties.Values
            .Where(p => p.OwnerId == player.Id && p.IsPending)
            .OrderByDescending(p => ctx.State.IndexOf(p.PropertyId));
        foreach (PropertyState p in pending)
            ctx.Push(new DownPaymentDueStep { PlayerId = player.Id, PropertyId = p.PropertyId });
    }

    /// <summary>True if no other player still in the game has a lower net worth than this one.</summary>
    private static bool IsLowestNetWorth(EngineContext ctx, PlayerState player)
    {
        var active = RuleModifiers.Active(ctx.State, ctx.Content);
        int mine = NetWorth.Calculate(ctx.State, ctx.Content, player.Id, active);
        return ctx.State.ActivePlayers.All(p => NetWorth.Calculate(ctx.State, ctx.Content, p.Id, active) >= mine);
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
        // The Medic pays nothing and rolls normally. Their Insured marker (if any) is kept.
        if (RuleModifiers.ShrugsOffHospital(ctx.State, ctx.Content, player.Id))
        {
            ctx.Emit(new HospitalShrugged(player.Id));
            return;
        }
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
