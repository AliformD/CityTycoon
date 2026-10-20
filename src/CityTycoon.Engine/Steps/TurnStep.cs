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
    PrecisionRoll,
    PrecisionChosen,
    Nudge,
    Move,
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

    /// <summary>Recovering from the Hospital: this turn rolls only 1 die (Section 15.3).</summary>
    public bool OneDieTurn { get; set; }

    /// <summary>The 3 dice of a Precision Roll, while the player picks 2.</summary>
    public List<int> PrecisionDice { get; set; } = [];

    /// <summary>Which 2 of the Precision Roll dice the player kept.</summary>
    public int KeptFirst { get; set; }
    public int KeptSecond { get; set; }

    /// <summary>The two dice that will move the player (the second is 0 on a one-die turn).</summary>
    public int Die1 { get; set; }
    public int Die2 { get; set; }

    /// <summary>The Nudge chosen (spaces added or taken away).</summary>
    public int NudgeSpaces { get; set; }

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
                player.BuildsThisTurn = 0;
                player.TradesProposedThisTurn = 0;
                EndStadiumBoosts(ctx);
                OneDieTurn = player.IsRecovering;
                player.IsRecovering = false;
                if (player.InJail)
                    player.JailTurns++;
                Stage = player.InJail ? TurnStage.JailChoice : TurnStage.ChooseMove;
                return StepResult.Continue;

            case TurnStage.JailChoice:
                return StepResult.WaitFor(new PendingDecision(PlayerId, DecisionKind.JailChoice,
                    $"You're in Jail (turn {player.JailTurns} of {ctx.Rules.MaxJailTurns}). Pay bail or try for doubles?",
                    Management: ManagementOptions.InJail));

            case TurnStage.ChooseMove:
                return StepResult.WaitFor(new PendingDecision(PlayerId, DecisionKind.ChooseMoveMethod,
                    OneDieTurn ? "You're recovering from the Hospital, so you roll only 1 die this turn." : "How do you want to move?",
                    Management: ManagementOptions.OwnTurn));

            case TurnStage.Roll:
                return OneDieTurn
                    ? AfterDice(ctx, player, ctx.Dice.RollDie(), 0)
                    : AfterDice(ctx, player, ctx.Dice.RollDie(), ctx.Dice.RollDie());

            case TurnStage.PrecisionRoll:
                return PrecisionRollStage(ctx, player);

            case TurnStage.PrecisionChosen:
            {
                int first = PrecisionDice[KeptFirst], second = PrecisionDice[KeptSecond];
                PrecisionDice = [];
                return AfterDice(ctx, player, first, second);
            }

            case TurnStage.Nudge:
                return StepResult.WaitFor(new PendingDecision(PlayerId, DecisionKind.ChooseNudge,
                    $"You rolled {Die1 + Die2}. Spend {ctx.Rules.NudgeCost} Momentum token to move up to {ctx.Rules.NudgeMaxSpaces} spaces more or fewer?",
                    Dice: Die2 == 0 ? [Die1] : [Die1, Die2]));

            case TurnStage.Move:
                return MoveAndResolve(ctx, player, Die1 + Die2 + NudgeSpaces, diceTotal: Die1 + Die2);

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
                return StepResult.WaitFor(new PendingDecision(PlayerId, DecisionKind.PostMoveActions, "Anything else before you end your turn?",
                    Management: ManagementOptions.OwnTurn));

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
            DecisionKind.ChooseMoveMethod => !OneDieTurn && MomentumRules.CanSpend(player, MomentumRules.PrecisionRollCost(ctx, player))
                ? [new RollDice(), new PrecisionRoll()]
                : [new RollDice()],
            DecisionKind.ChoosePrecisionDice => [new ChooseDice(0, 1), new ChooseDice(0, 2), new ChooseDice(1, 2)],
            DecisionKind.ChooseNudge => MomentumRules.NudgeOptions(ctx, player, Die1 + Die2).Select(n => (GameAction)new Nudge(n)).ToList(),
            DecisionKind.PostMoveActions => [new EndTurn()],
            // Bail is paid with cash on hand. Short? Take a loan or mortgage something first.
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

            case PrecisionRoll:
                Stage = TurnStage.PrecisionRoll;
                break;

            case ChooseDice keep:
                KeptFirst = keep.First;
                KeptSecond = keep.Second;
                Stage = TurnStage.PrecisionChosen;
                break;

            case Nudge nudge:
                NudgeSpaces = nudge.Spaces;
                if (nudge.Spaces != 0)
                {
                    player.Momentum -= ctx.Rules.NudgeCost;
                    ctx.Emit(new Nudged(PlayerId, nudge.Spaces, ctx.Rules.NudgeCost));
                }
                Stage = TurnStage.Move;
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

    /// <summary>Spends the tokens, rolls 3 dice, and asks which 2 to keep (Section 7.3).</summary>
    private StepResult PrecisionRollStage(EngineContext ctx, PlayerState player)
    {
        if (PrecisionDice.Count == 0)
        {
            int cost = MomentumRules.PrecisionRollCost(ctx, player);
            player.Momentum -= cost;
            PrecisionDice = Enumerable.Range(0, ctx.Rules.PrecisionRollDice).Select(_ => ctx.Dice.RollDie()).ToList();
            ctx.Emit(new PrecisionRolled(PlayerId, cost, PrecisionDice.ToList()));
        }
        return StepResult.WaitFor(new PendingDecision(PlayerId, DecisionKind.ChoosePrecisionDice,
            $"You rolled {string.Join(", ", PrecisionDice)}. Which 2 dice do you keep?", Dice: PrecisionDice.ToList()));
    }

    /// <summary>
    /// The dice that will move the player are known. Check doubles, then offer
    /// a Nudge if the player has a token to spend, otherwise move straight away.
    /// </summary>
    private StepResult AfterDice(EngineContext ctx, PlayerState player, int die1, int die2)
    {
        var roll = new DiceRolled(PlayerId, die1, die2);
        ctx.Emit(roll);
        Die1 = die1;
        Die2 = die2;
        NudgeSpaces = 0;

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

        // Only offer a Nudge when there's a real choice (a token to spend).
        bool canNudge = MomentumRules.NudgeOptions(ctx, player, roll.Total).Any(n => n != 0);
        Stage = canNudge ? TurnStage.Nudge : TurnStage.Move;
        return StepResult.Continue;
    }

    /// <summary>A Stadium boost lasts until the start of the chooser's next turn (Section 15.3).</summary>
    private void EndStadiumBoosts(EngineContext ctx)
    {
        foreach (StadiumBoost boost in ctx.State.StadiumBoosts.Where(b => b.ChosenById == PlayerId).ToList())
        {
            ctx.State.StadiumBoosts.Remove(boost);
            ctx.Emit(new StadiumEnded(PlayerId, boost.GroupId));
        }
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

    /// <param name="diceTotal">What the dice showed, before any Nudge (utility rent uses it).</param>
    private StepResult MoveAndResolve(EngineContext ctx, PlayerState player, int spaces, int? diceTotal = null)
    {
        LastDiceTotal = diceTotal ?? spaces;
        Stage = TurnStage.PostMove;
        // Push "resolve the space" first, then move. Anything passing GO pushes
        // (like a down payment falling due) lands on top, so it's dealt with
        // before the landing space, in the order it happened.
        ctx.Push(new ResolveSpaceStep { PlayerId = PlayerId, DiceTotal = LastDiceTotal });
        Movement.MoveForward(ctx, player, spaces);
        return StepResult.Continue;
    }
}
