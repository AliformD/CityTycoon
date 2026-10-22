using System.Text.Json.Serialization;
using CityTycoon.Engine.Actions;
using CityTycoon.Engine.Cards;
using CityTycoon.Engine.Content;
using CityTycoon.Engine.Events;
using CityTycoon.Engine.Modifiers;
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
    MoveReroll,
    DiceKept,
    PrecisionRoll,
    PrecisionChosen,
    ExpressRoll,
    Nudge,
    Detour,
    Move,
    JailRoll,
    ForcedBailMove,
    PostMove,
    AfterPostMove,
    EndOfTurn,
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
    public bool RecoveringThisTurn { get; set; }

    /// <summary>Road Work: this turn rolls only 1 die, unless this many tokens are spent. 0 means no Road Work.</summary>
    public int RoadWorkCost { get; set; }

    /// <summary>True when this turn rolls only 1 die (Hospital recovery or Road Work, Section 7.3).</summary>
    [JsonIgnore]
    public bool OneDieTurn => RecoveringThisTurn || RoadWorkCost > 0;

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

    /// <summary>Spaces another player's Detour card added or took away.</summary>
    public int DetourSpaces { get; set; }

    /// <summary>The Detour question for other players, once it has been opened.</summary>
    public ReactionWindow? DetourWindow { get; set; }

    /// <summary>How far the player will move: the dice plus any Nudge.</summary>
    [JsonIgnore]
    public int PlannedSpaces => Die1 + Die2 + NudgeSpaces;

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
                player.ExtraBuildsThisTurn = 0;
                player.BuildDiscountThisTurn = 0;
                EndBoosts(ctx);
                RecoveringThisTurn = player.IsRecovering;
                player.IsRecovering = false;
                RoadWorkCost = player.RoadWorkCost;
                player.RoadWorkCost = 0;
                if (player.InJail)
                    player.JailTurns++;
                Stage = player.InJail ? TurnStage.JailChoice : TurnStage.ChooseMove;
                StartOfTurnPerks(ctx, player);
                return StepResult.Continue;

            case TurnStage.JailChoice:
                return StepResult.WaitFor(new PendingDecision(PlayerId, DecisionKind.JailChoice,
                    $"You're in Jail (turn {player.JailTurns} of {ctx.Rules.MaxJailTurns}). Pay bail or try for doubles?",
                    Management: ManagementOptions.InJail));

            case TurnStage.ChooseMove:
                return StepResult.WaitFor(new PendingDecision(PlayerId, DecisionKind.ChooseMoveMethod,
                    RecoveringThisTurn ? "You're recovering from the Hospital, so you roll only 1 die this turn."
                    : RoadWorkCost > 0 ? $"Road Work! You roll only 1 die this turn, unless you spend {RoadWorkCost} Momentum token."
                    : player.DoublesInARow > 0 ? "You rolled doubles, so you go again! How do you want to move?"
                    : "How do you want to move?",
                    Management: ManagementOptions.OwnTurn));

            case TurnStage.Roll:
                return OneDieTurn
                    ? AfterDice(ctx, player, ctx.Dice.RollDie(), 0, fromRoll: true)
                    : AfterDice(ctx, player, ctx.Dice.RollDie(), ctx.Dice.RollDie(), fromRoll: true);

            case TurnStage.MoveReroll:
                return StepResult.WaitFor(new PendingDecision(PlayerId, DecisionKind.RerollMove,
                    $"You rolled {(Die2 == 0 ? $"{Die1}" : $"{Die1} + {Die2} = {Die1 + Die2}")}. Use your Hustler reroll? You get one each lap.",
                    Dice: Die2 == 0 ? [Die1] : [Die1, Die2]));

            case TurnStage.DiceKept:
                return ResolveDice(ctx, player);

            case TurnStage.PrecisionRoll:
                return PrecisionRollStage(ctx, player);

            case TurnStage.PrecisionChosen:
            {
                int first = PrecisionDice[KeptFirst], second = PrecisionDice[KeptSecond];
                PrecisionDice = [];
                return AfterDice(ctx, player, first, second);
            }

            case TurnStage.ExpressRoll:
                // Express Line: after the jump, roll 1 die (Section 13.3). One die can't be doubles, so no extra turn.
                return AfterDice(ctx, player, ctx.Dice.RollDie(), 0);

            case TurnStage.Nudge:
                return StepResult.WaitFor(new PendingDecision(PlayerId, DecisionKind.ChooseNudge,
                    $"You rolled {Die1 + Die2}. Spend Momentum tokens to move more or fewer spaces? Each space costs {ctx.Rules.NudgeCost} token.",
                    Dice: Die2 == 0 ? [Die1] : [Die1, Die2]));

            case TurnStage.Detour:
                return DetourStage(ctx, player);

            case TurnStage.Move:
                return MoveAndResolve(ctx, player, PlannedSpaces + DetourSpaces, diceTotal: Die1 + Die2);

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
                {
                    Stage = TurnStage.EndOfTurn;
                    return StepResult.Continue;
                }
                // Doubles: skip the "end your turn?" question and go straight to rolling again. The roll
                // question allows the same building, trading and cards, so nothing is lost (decision #145).
                if (ExtraTurnEarned)
                {
                    Stage = TurnStage.AfterPostMove;
                    return StepResult.Continue;
                }
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
                Stage = TurnStage.EndOfTurn;
                return StepResult.Continue;

            case TurnStage.EndOfTurn:
                // Section 7.2, step 10: discard down to the hand limit, then the turn is over.
                ctx.Push(new HandLimitStep { PlayerId = PlayerId });
                return StepResult.Done;

            default:
                throw new InvalidOperationException($"Unknown turn stage {Stage}.");
        }
    }

    public override IReadOnlyList<GameAction> LegalActions(EngineContext ctx, PendingDecision decision)
    {
        PlayerState player = ctx.Player(PlayerId);
        if (decision.Kind == DecisionKind.PlayReaction)
            return DetourWindow!.LegalActions(ctx, decision);
        IReadOnlyList<GameAction> main = decision.Kind switch
        {
            DecisionKind.ChooseMoveMethod => MoveChoices(ctx, player),
            DecisionKind.ChoosePrecisionDice => [new ChooseDice(0, 1), new ChooseDice(0, 2), new ChooseDice(1, 2)],
            DecisionKind.ChooseNudge => MomentumRules.NudgeOptions(ctx, player, Die1 + Die2).Select(n => (GameAction)new Nudge(n)).ToList(),
            DecisionKind.RerollMove => [new RerollDice(), new NoThanks()],
            DecisionKind.PostMoveActions => [new EndTurn(), .. HostileTakeovers(ctx, player)],
            // Bail is paid with cash on hand. Short? Take a loan or mortgage something first. (The Lawyer pays $0.)
            DecisionKind.JailChoice => player.Cash >= RuleModifiers.Bail(ctx.State, ctx.Content, PlayerId)
                ? [new PayBail(), new RollForDoubles()]
                : [new RollForDoubles()],
            _ => [],
        };
        // Opportunity cards that can be played at this moment of your own turn (Section 7.2).
        return [.. main, .. CardRules.OwnTurnPlays(ctx, player, decision.Kind)];
    }

    private List<GameAction> MoveChoices(EngineContext ctx, PlayerState player)
    {
        var choices = new List<GameAction> { new RollDice() };
        if (!OneDieTurn && MomentumRules.CanSpend(player, MomentumRules.PrecisionRollCost(ctx, player)))
            choices.Add(new PrecisionRoll());
        if (RoadWorkCost > 0 && !RecoveringThisTurn && MomentumRules.CanSpend(player, RoadWorkCost))
            choices.Add(new ClearRoadWork());
        choices.AddRange(ExpressLines(ctx, player));
        choices.AddRange(HostileTakeovers(ctx, player));
        return choices;
    }

    /// <summary>
    /// Start-of-turn perks (Section 7.2, step 1): Dividends first, then the Neighborhood Watch
    /// charge if you're in Jail or recovering from the Hospital. The charge is a payment pushed
    /// on top, so it is settled (raising money if needed) before the turn goes on.
    /// </summary>
    private void StartOfTurnPerks(EngineContext ctx, PlayerState player)
    {
        foreach (var (amount, source) in RuleModifiers.TurnStartIncomes(ctx.State, ctx.Content, PlayerId))
        {
            player.Cash += amount;
            ctx.Emit(new PerkIncome(PlayerId, amount, source));
        }
        bool jailedOrRecovering = player.InJail || RecoveringThisTurn;
        foreach (var (payeeId, amount, source) in RuleModifiers.TurnStartCharges(ctx.State, ctx.Content, PlayerId, jailedOrRecovering))
            ctx.Push(new PaymentStep { FromId = PlayerId, ToId = payeeId, Amount = amount, Reason = PaymentReason.Perk, PerkName = source });
    }

    /// <summary>Express Line (Section 13.3): every railroad this player could jump to, if they can pay for it.</summary>
    private static IEnumerable<GameAction> ExpressLines(EngineContext ctx, PlayerState player)
    {
        int cost = RuleModifiers.ExpressLineCost(ctx.State, ctx.Content, player.Id);
        if (cost == 0 || player.Cash < cost)
            return [];
        return ctx.State.Properties.Values
            .Where(p => p.IsOwnedOutright(player.Id) && !p.IsMortgaged && ctx.Content.Property(p.PropertyId).Type == PropertyType.Railroad)
            .OrderBy(p => ctx.State.IndexOf(p.PropertyId))
            .Select(p => new ExpressLine(p.PropertyId));
    }

    /// <summary>The Red perk (Section 8.6): every opponent property this player could force to auction, once per lap.</summary>
    private static IEnumerable<GameAction> HostileTakeovers(EngineContext ctx, PlayerState player)
    {
        if (player.TakeoverUsed || !RuleModifiers.CanHostileTakeover(ctx.State, ctx.Content, player.Id))
            return [];
        return ctx.State.Board
            .Where(s => ctx.State.Properties.TryGetValue(s.Id, out var p) && p.OwnerId is int owner && owner != player.Id
                && AuctionStep.CanForceAuction(ctx, s.Id))
            .Select(s => new HostileTakeover(s.Id));
    }

    public override bool IsLegal(EngineContext ctx, PendingDecision decision, GameAction action)
    {
        if (action is PlayCard play && decision.Kind != DecisionKind.PlayReaction)
            return CardRules.IsLegalOwnTurnPlay(ctx, ctx.Player(PlayerId), decision.Kind, play);
        return base.IsLegal(ctx, decision, action);
    }

    public override void Receive(EngineContext ctx, PendingDecision decision, GameAction action)
    {
        PlayerState player = ctx.Player(PlayerId);
        if (decision.Kind == DecisionKind.PlayReaction)
        {
            DetourWindow!.Receive(ctx, decision, action);
            return;
        }
        switch (action)
        {
            case PlayCard play:
                // The card's effect runs next, on top of this turn. Then the same question comes back,
                // unless the card moved the turn on (Shortcut, Get Out of Jail Free).
                CardRules.BeginPlay(ctx, player, play);
                break;

            case ClearRoadWork:
                player.Momentum -= RoadWorkCost;
                ctx.Emit(new RoadWorkCleared(PlayerId, RoadWorkCost));
                RoadWorkCost = 0;
                break;

            case RollDice:
                Stage = TurnStage.Roll;
                break;

            case PrecisionRoll:
                Stage = TurnStage.PrecisionRoll;
                break;

            case ExpressLine express:
                int fare = RuleModifiers.ExpressLineCost(ctx.State, ctx.Content, PlayerId);
                player.Cash -= fare;
                player.Position = ctx.State.IndexOf(express.RailroadId);
                ctx.Emit(new ExpressLineUsed(PlayerId, express.RailroadId, fare));
                Stage = TurnStage.ExpressRoll;
                break;

            case HostileTakeover takeover:
                player.TakeoverUsed = true;
                ctx.Emit(new HostileTakeoverUsed(PlayerId, takeover.PropertyId));
                // The auction runs on top of this turn; afterwards the same question comes back.
                ctx.Push(new AuctionStep
                {
                    PropertyId = takeover.PropertyId,
                    Kind = AuctionKind.Forced,
                    TieBreakStartId = PlayerId,
                    OwnerId = ctx.State.Properties[takeover.PropertyId].OwnerId,
                });
                break;

            case RerollDice:
                // The Hustler throws this roll away and rolls again (decision #152). It's used up until the next GO.
                player.MoveRerollUsed = true;
                ctx.Emit(new MoveRerolled(PlayerId, Die1, Die2));
                Stage = TurnStage.Roll;
                break;

            case NoThanks when decision.Kind == DecisionKind.RerollMove:
                Stage = TurnStage.DiceKept;
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
                    int cost = MomentumRules.NudgeCost(ctx, nudge.Spaces);
                    player.Momentum -= cost;
                    ctx.Emit(new Nudged(PlayerId, nudge.Spaces, cost));
                }
                Stage = TurnStage.Detour;
                break;

            case PayBail:
                int bail = RuleModifiers.Bail(ctx.State, ctx.Content, PlayerId);
                player.Cash -= bail;
                player.InJail = false;
                LeftJailThisTurn = true;
                ctx.Emit(new BailPaid(PlayerId, bail));
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
    /// The dice that will move the player are known. A Hustler with their reroll ready is asked
    /// first (only for a normal roll, not a Precision Roll or the Express Line); then the roll counts.
    /// </summary>
    private StepResult AfterDice(EngineContext ctx, PlayerState player, int die1, int die2, bool fromRoll = false)
    {
        ctx.Emit(new DiceRolled(PlayerId, die1, die2));
        Die1 = die1;
        Die2 = die2;
        NudgeSpaces = 0;
        DetourSpaces = 0;
        DetourWindow = null;
        DetourPlayed = false;

        if (fromRoll && !player.MoveRerollUsed && RuleModifiers.MoveReroll(ctx.State, ctx.Content, PlayerId) != null)
        {
            Stage = TurnStage.MoveReroll;
            return StepResult.Continue;
        }
        return ResolveDice(ctx, player);
    }

    /// <summary>
    /// The roll is final. Check doubles, then offer a Nudge if the player
    /// has a token to spend, otherwise move straight away.
    /// </summary>
    private StepResult ResolveDice(EngineContext ctx, PlayerState player)
    {
        var roll = new DiceRolled(PlayerId, Die1, Die2);

        if (roll.IsDoubles && !LeftJailThisTurn)
        {
            player.DoublesInARow++;
            if (player.DoublesInARow >= ctx.Rules.MaxDoublesBeforeJail)
            {
                // Three doubles in a row: go to Jail instead of moving (Section 7.2, step 9).
                Movement.SendToJail(ctx, player, $"rolled doubles {ctx.Rules.MaxDoublesBeforeJail} times in a row");
                Stage = TurnStage.EndOfTurn;
                return StepResult.Continue;
            }
        }

        ExtraTurnEarned = roll.IsDoubles && !LeftJailThisTurn;

        // Only offer a Nudge when there's a real choice (a token to spend).
        bool canNudge = MomentumRules.NudgeOptions(ctx, player, roll.Total).Any(n => n != 0);
        Stage = canNudge ? TurnStage.Nudge : TurnStage.Detour;
        return StepResult.Continue;
    }

    /// <summary>Stadium and Grand Opening boosts last until the start of the chooser's next turn (Section 15.3).</summary>
    private void EndBoosts(EngineContext ctx)
    {
        foreach (StadiumBoost boost in ctx.State.StadiumBoosts.Where(b => b.ChosenById == PlayerId).ToList())
        {
            ctx.State.StadiumBoosts.Remove(boost);
            ctx.Emit(new StadiumEnded(PlayerId, boost.GroupId));
        }
        foreach (PropertyBoost boost in ctx.State.PropertyBoosts.Where(b => b.ChosenById == PlayerId).ToList())
        {
            ctx.State.PropertyBoosts.Remove(boost);
            ctx.Emit(new RentBoostEnded(PlayerId, boost.PropertyId));
        }
    }

    /// <summary>
    /// The Detour window (Section 7.2, step 5): after the roll and Nudge, any other
    /// player holding a Detour may move this player's landing spot by a space.
    /// </summary>
    private StepResult DetourStage(EngineContext ctx, PlayerState player)
    {
        if (DetourWindow == null)
        {
            var others = CardEffects.OthersInTurnOrder(ctx.State, PlayerId).Select(p => p.Id);
            DetourWindow = ReactionWindow.Open(ctx, CardTrigger.OpponentMoving, others, moverId: PlayerId, moveSpaces: PlannedSpaces);
        }
        string prompt = NudgeSpaces != 0
            ? $"{player.Name} rolled {Die1 + Die2} and nudged to {PlannedSpaces} spaces. A Detour cancels the Nudge and moves them 1 space from the roll. Play one?"
            : $"{player.Name} is about to move {PlannedSpaces} spaces. Play a Detour?";
        if (DetourWindow.Decision(ctx, prompt) is { } ask)
            return StepResult.WaitFor(ask);
        if (DetourWindow.Played is { } play && !DetourPlayed)
        {
            // The Detour's effect runs on top of this turn. The mover may cancel it with Insurance.
            DetourPlayed = true;
            ctx.Push(new CardPlayStep { PlayerId = DetourWindow.PlayedBy, Play = play });
            return StepResult.Continue;
        }
        Stage = TurnStage.Move;
        return StepResult.Continue;
    }

    /// <summary>True once a Detour card from the window has been handed to its effect.</summary>
    public bool DetourPlayed { get; set; }

    /// <summary>Shortcut card: move this many spaces instead of rolling. It's never doubles, so no extra turn.</summary>
    public void TakeShortcut(EngineContext ctx, PlayerState player, int spaces)
    {
        ExtraTurnEarned = false;
        Die1 = Die2 = NudgeSpaces = DetourSpaces = 0;
        MoveAndResolve(ctx, player, spaces);
    }

    /// <summary>Get Out of Jail Free: leave Jail and move normally (no extra turn on doubles, like bail).</summary>
    public void LeaveJailFree(EngineContext ctx, PlayerState player)
    {
        player.InJail = false;
        LeftJailThisTurn = true;
        ctx.Emit(new LeftJail(PlayerId, "used Get Out of Jail Free"));
        Stage = TurnStage.ChooseMove;
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
            ctx.Push(new PaymentStep { FromId = PlayerId, ToId = null, Amount = RuleModifiers.Bail(ctx.State, ctx.Content, PlayerId), Reason = PaymentReason.Bail });
            return StepResult.Continue;
        }

        ctx.Emit(new StayedInJail(PlayerId, player.JailTurns));
        Stage = TurnStage.EndOfTurn;
        return StepResult.Continue;
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
