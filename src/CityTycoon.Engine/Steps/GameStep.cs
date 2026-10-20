using CityTycoon.Engine.Events;
using CityTycoon.Engine.Rules;
using CityTycoon.Engine.State;

namespace CityTycoon.Engine.Steps;

/// <summary>
/// The bottom of the stack for the whole game. Each time it runs, it starts
/// the next player's turn, runs the end-of-round checks when a round finishes,
/// and ends the game when the time comes (Sections 7.1 and 16).
/// </summary>
public sealed class GameStep : Step
{
    /// <summary>False until the very first turn has been started.</summary>
    public bool Started { get; set; }

    public override StepResult Run(EngineContext ctx)
    {
        GameState state = ctx.State;
        if (state.IsOver)
            return StepResult.Done;

        if (!Started)
        {
            Started = true;
            return StartTurn(ctx);
        }

        // If we get here, a turn has just finished.
        if (state.ActivePlayers.Count() <= 1)
        {
            EndGame(ctx, "Only one player is left.");
            return StepResult.Done;
        }

        int current = state.ActivePlayerIndex;
        int next = NextActiveIndex(state, current);

        // Going back to an earlier seat means everyone has had a turn: the round is over.
        if (next <= current)
        {
            ctx.Emit(new RoundEnded(state.Round));
            // End-of-round checks (Section 7.2, step 10). Ordinances and the
            // City Council arrive in Phase 8; the Timed-mode limit is checked now.
            if (state.Settings.Mode == GameMode.Timed && state.Round >= state.Settings.MaxRounds)
            {
                EndGame(ctx, $"The {state.Settings.MaxRounds}-round limit was reached.");
                return StepResult.Done;
            }
            state.Round++;
        }

        state.ActivePlayerIndex = next;
        return StartTurn(ctx);
    }

    private static StepResult StartTurn(EngineContext ctx)
    {
        ctx.Push(new TurnStep { PlayerId = ctx.State.ActivePlayer.Id });
        return StepResult.Continue;
    }

    /// <summary>The next seat (wrapping around) whose player isn't bankrupt.</summary>
    private static int NextActiveIndex(GameState state, int current)
    {
        for (int offset = 1; offset <= state.Players.Count; offset++)
        {
            int index = (current + offset) % state.Players.Count;
            if (!state.Players[index].IsBankrupt)
                return index;
        }
        return current;
    }

    public static void EndGame(EngineContext ctx, string reason)
    {
        var standings = NetWorth.Standings(ctx.State, ctx.Content);
        var winners = standings.Where(s => s.Rank == 1 && !s.IsBankrupt).Select(s => s.PlayerId).ToList();
        ctx.State.Result = new GameResult { WinnerIds = winners, Reason = reason, Standings = standings };
        ctx.Emit(new GameEnded(winners, reason));
    }
}
