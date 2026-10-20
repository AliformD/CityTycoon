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

    /// <summary>GO effects (Section 15.1). Phase 1 only has the salary.</summary>
    public static void CollectGo(EngineContext ctx, PlayerState player)
    {
        player.Cash += ctx.Rules.GoSalary;
        ctx.Emit(new PassedGo(player.Id, ctx.Rules.GoSalary));
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
