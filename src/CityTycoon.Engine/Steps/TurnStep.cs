using CityTycoon.Engine.Actions;
using CityTycoon.Engine.Events;
using CityTycoon.Engine.Rules;
using CityTycoon.Engine.State;

namespace CityTycoon.Engine.Steps;

/// <summary>Where a turn has got to. The turn sequence is in Section 7.2.</summary>
public enum TurnStage
{
    Start,
    JailChoice,
    ChooseMove,
    Roll,
    JailRoll,
    ForcedBailMove,
    PostMove,
    AfterPostMove,
}

/// <summary>
/// One player's turn, from start to finish. It is a small "state machine":
/// <see cref="Stage"/> remembers where the turn is, and each call to
/// <see cref="Run"/> does the work for that stage and moves to the next.
/// </summary>
public sealed class TurnStep : Step
{
    public int PlayerId { get; set; }
    public TurnStage Stage { get; set; } = TurnStage.Start;

    /// <summary>True when the last roll was doubles and earns another go.</summary>
    public bool ExtraTurnEarned { get; set; }

    /// <summary>
    /// True once the player has rolled out of Jail this turn. Jail rolls never
    /// earn an extra turn (Angel's ruling, docs/DECISIONS.md #6).
    /// </summary>
    public bool LeftJailThisTurn { get; set; }

    /// <summary>The dice total that moved the player, kept for utility rent.</summary>
    public int LastDiceTotal { get; set; }

    public override StepResult Run(EngineContext ctx)
    {
        PlayerState player = ctx.Player(PlayerId);
        if (player.IsBankrupt || ctx.State.IsOver)
            return StepResult.Done;

        switch (Stage)
        {
            case TurnStage.Start:
                ctx.Emit(new TurnStarted(PlayerId, ctx.State.Round));
                player.DoublesInARow = 0;
                if (player.InJail)
                    player.JailTurns++;
                Stage = player.InJail ? TurnStage.JailChoice : TurnStage.ChooseMove;
                return StepResult.Continue;

            case TurnStage.JailChoice:
                return StepResult.WaitFor(new PendingDecision(PlayerId, DecisionKind.JailChoice,
                    $"You're in Jail (turn {player.JailTurns} of {ctx.Rules.MaxJailTurns}). Pay bail or try for doubles?"));

            case TurnStage.ChooseMove:
                return StepResult.WaitFor(new PendingDecision(PlayerId, DecisionKind.ChooseMoveMethod, "How do you want to move?"));

            case TurnStage.Roll:
                return RollAndMove(ctx, player);

            case TurnStage.JailRoll:
                return RollInJail(ctx, player);

            case TurnStage.ForcedBailMove:
                // The bail payment (pushed in RollInJail) has been made. Now move by that roll.
                player.InJail = false;
                LeftJailThisTurn = true;
                ctx.Emit(new LeftJail(PlayerId, "paid bail after the last Jail turn"));
                return MoveAndResolve(ctx, player, LastDiceTotal);

            case TurnStage.PostMove:
                // Going to Jail ends your turn straight away (Section 14.1).
                if (player.InJail)
                    return StepResult.Done;
                return StepResult.WaitFor(new PendingDecision(PlayerId, DecisionKind.PostMoveActions, "Anything else before you end your turn?"));

            case TurnStage.AfterPostMove:
                // Doubles: take another turn from the pre-roll phase (Section 7.2, step 9).
                if (ExtraTurnEarned && !player.InJail)
                {
                    ExtraTurnEarned = false;
                    Stage = TurnStage.ChooseMove;
                    return StepResult.Continue;
                }
                return StepResult.Done;

            default:
                throw new InvalidOperationException($"Unknown turn stage {Stage}.");
        }
    }

    public override IReadOnlyList<GameAction> LegalActions(EngineContext ctx, PendingDecision decision)
    {
        PlayerState player = ctx.Player(PlayerId);
        return decision.Kind switch
        {
            DecisionKind.ChooseMoveMethod => [new RollDice()],
            DecisionKind.PostMoveActions => [new EndTurn()],
            // Bail can only be paid with cash on hand (loans and mortgages arrive in Phase 3).
            DecisionKind.JailChoice => player.Cash >= ctx.Rules.JailBail
                ? [new PayBail(), new RollForDoubles()]
                : [new RollForDoubles()],
            _ => [],
        };
    }

    public override void Receive(EngineContext ctx, PendingDecision decision, GameAction action)
    {
        PlayerState player = ctx.Player(PlayerId);
        switch (action)
        {
            case RollDice:
                Stage = TurnStage.Roll;
                break;

            case PayBail:
                player.Cash -= ctx.Rules.JailBail;
                player.InJail = false;
                LeftJailThisTurn = true;
                ctx.Emit(new BailPaid(PlayerId, ctx.Rules.JailBail));
                ctx.Emit(new LeftJail(PlayerId, "paid bail"));
                // Out of Jail: now choose how to move, like a normal turn.
                Stage = TurnStage.ChooseMove;
                break;

            case RollForDoubles:
                Stage = TurnStage.JailRoll;
                break;

            case EndTurn:
                Stage = TurnStage.AfterPostMove;
                break;
        }
    }

    private StepResult RollAndMove(EngineContext ctx, PlayerState player)
    {
        var roll = new DiceRolled(PlayerId, ctx.Dice.RollDie(), ctx.Dice.RollDie());
        ctx.Emit(roll);

        if (roll.IsDoubles && !LeftJailThisTurn)
        {
            player.DoublesInARow++;
            if (player.DoublesInARow >= ctx.Rules.MaxDoublesBeforeJail)
            {
                // Three doubles in a row: go to Jail instead of moving (Section 7.2, step 9).
                Movement.SendToJail(ctx, player, $"rolled doubles {ctx.Rules.MaxDoublesBeforeJail} times in a row");
                return StepResult.Done;
            }
        }

        ExtraTurnEarned = roll.IsDoubles && !LeftJailThisTurn;
        return MoveAndResolve(ctx, player, roll.Total);
    }

    /// <summary>Jail option 3: try for doubles (Section 14.3).</summary>
    private StepResult RollInJail(EngineContext ctx, PlayerState player)
    {
        var roll = new DiceRolled(PlayerId, ctx.Dice.RollDie(), ctx.Dice.RollDie());
        ctx.Emit(roll);
        LastDiceTotal = roll.Total;

        if (roll.IsDoubles)
        {
            // Out! Move by that roll, but no extra turn.
            player.InJail = false;
            LeftJailThisTurn = true;
            ctx.Emit(new LeftJail(PlayerId, "rolled doubles"));
            return MoveAndResolve(ctx, player, roll.Total);
        }

        if (player.JailTurns >= ctx.Rules.MaxJailTurns)
        {
            // Failed on the last allowed turn: must pay bail and move by this roll.
            Stage = TurnStage.ForcedBailMove;
            ctx.Push(new PaymentStep { FromId = PlayerId, ToId = null, Amount = ctx.Rules.JailBail, Reason = PaymentReason.Bail });
            return StepResult.Continue;
        }

        ctx.Emit(new StayedInJail(PlayerId, player.JailTurns));
        return StepResult.Done;
    }

    private StepResult MoveAndResolve(EngineContext ctx, PlayerState player, int spaces)
    {
        LastDiceTotal = spaces;
        Movement.MoveForward(ctx, player, spaces);
        Stage = TurnStage.PostMove;
        ctx.Push(new ResolveSpaceStep { PlayerId = PlayerId, DiceTotal = spaces });
        return StepResult.Continue;
    }
}
