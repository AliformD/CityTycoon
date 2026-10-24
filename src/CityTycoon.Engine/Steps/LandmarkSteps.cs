using CityTycoon.Engine.Actions;
using CityTycoon.Engine.Events;
using CityTycoon.Engine.Modifiers;
using CityTycoon.Engine.Rules;
using CityTycoon.Engine.State;

namespace CityTycoon.Engine.Steps;

/// <summary>
/// Visiting the Hospital (Section 15.3): you may buy an Insured marker.
/// You can hold only one, so nobody who already has one is asked.
/// </summary>
public sealed class HospitalVisitStep : Step
{
    public int PlayerId { get; set; }
    public bool Answered { get; set; }

    public override StepResult Run(EngineContext ctx)
    {
        PlayerState player = ctx.Player(PlayerId);
        int cost = RuleModifiers.InsuredMarkerCost(ctx.State, ctx.Content, PlayerId);
        if (Answered || player.IsInsured || player.Cash < cost)
            return StepResult.Done;

        return StepResult.WaitFor(new PendingDecision(PlayerId, DecisionKind.HospitalVisit,
            $"You're visiting the Hospital. {(cost == 0 ? "Take a free Insured marker" : $"Buy an Insured marker for ${cost}")}? It cancels one Hospital bill.",
            AmountOwed: cost));
    }

    public override IReadOnlyList<GameAction> LegalActions(EngineContext ctx, PendingDecision decision) =>
        [new BuyInsurance(), new NoThanks()];

    public override void Receive(EngineContext ctx, PendingDecision decision, GameAction action)
    {
        Answered = true;
        if (action is not BuyInsurance)
            return;

        PlayerState player = ctx.Player(PlayerId);
        int cost = RuleModifiers.InsuredMarkerCost(ctx.State, ctx.Content, PlayerId);
        player.Cash -= cost;
        player.IsInsured = true;
        // The money goes to the Medic, if one is playing (decision #153).
        int? medic = cost > 0 ? RuleModifiers.HospitalCollector(ctx.State, ctx.Content, PlayerId) : null;
        if (medic is int medicId)
            ctx.Player(medicId).Cash += cost;
        ctx.Emit(new InsuranceBought(PlayerId, cost, medic));
    }
}

/// <summary>
/// The Casino (Section 15.3, decision #164): Dice Poker. The house puts a free chip on the player's hand, and they may
/// add their own money to it. They roll 5 dice, keep any, roll the rest once more, and the pay table pays the hand.
/// Then, if someone is worth more than them, they may call that player to a Showdown with the hand they made.
/// </summary>
public sealed class CasinoStep : Step
{
    public int PlayerId { get; set; }

    /// <summary>True once the player has said how much of their own money goes on the hand (or walked on by).</summary>
    public bool Answered { get; set; }
    public bool WalkedAway { get; set; }
    public int Stake { get; set; }

    /// <summary>The hand, once rolled (empty before).</summary>
    public List<int> Dice { get; set; } = [];
    public bool Rerolled { get; set; }
    public bool Paid { get; set; }
    public bool ShowdownAsked { get; set; }

    public override StepResult Run(EngineContext ctx)
    {
        PlayerState player = ctx.Player(PlayerId);
        int chip = RuleModifiers.CasinoFreeChip(ctx.State, ctx.Content, PlayerId);
        if (!Answered)
        {
            // Nothing to stake: play the free chip without asking.
            if (player.Cash > 0)
                return StepResult.WaitFor(new PendingDecision(PlayerId, DecisionKind.CasinoBet,
                    $"Welcome to the Casino! The house gives you a free ${chip} chip for a hand of Dice Poker. Add some of your own money to it?",
                    MaxBet: StakeLimit(ctx, player.Cash), FreeChip: chip));
            Answered = true;
        }
        if (WalkedAway)
            return StepResult.Done;

        if (Dice.Count == 0)
            Dice = PokerDice.RollAll(ctx, PlayerId);
        if (!Rerolled)
            return StepResult.WaitFor(new PendingDecision(PlayerId, DecisionKind.KeepDice,
                $"You rolled {DicePoker.Describe(DicePoker.Score(Dice))}. Pick the dice to keep, then roll the rest once more.",
                Dice: Dice.ToList(), FreeChip: chip, Stake: Stake));

        if (!Paid)
        {
            Paid = true;
            Pay(ctx, player, chip);
        }

        if (!ShowdownAsked)
        {
            ShowdownAsked = true;
            if (ShowdownTargets(ctx).Any())
                return StepResult.WaitFor(new PendingDecision(PlayerId, DecisionKind.CallShowdown,
                    $"You made {DicePoker.Describe(DicePoker.Score(Dice))}. Call a Showdown against someone worth more than you? They must beat your hand or fold.",
                    Dice: Dice.ToList()));
        }
        return StepResult.Done;
    }

    /// <summary>The chip and the stake ride on the hand together; the pay table says how much comes back.</summary>
    private void Pay(EngineContext ctx, PlayerState player, int chip)
    {
        int stake = Math.Min(Stake, player.Cash);   // nothing can lower cash in between, but stay safe
        PokerHand hand = DicePoker.Score(Dice);
        int payout = (chip + stake) * DicePoker.PayPercent(ctx.Rules, hand) / 100;
        int net = payout - stake;
        player.Cash += net;
        ctx.Emit(new CasinoHandPaid(PlayerId, Dice.ToList(), hand, chip, stake, net));
    }

    /// <summary>The most this player can stake: their cash, or the Casino Regulation cap if one is in force.</summary>
    internal static int StakeLimit(EngineContext ctx, int cash) =>
        Math.Min(cash, RuleModifiers.MaxCasinoStake(ctx.State, ctx.Content) ?? int.MaxValue);

    /// <summary>
    /// Who can be called to a Showdown from the Casino: players still in, with cash, who are worth more than this player.
    /// The rule keeps a Showdown a comeback move, so the leader can't pick on players who are behind.
    /// </summary>
    private IEnumerable<PlayerState> ShowdownTargets(EngineContext ctx)
    {
        if (ctx.Player(PlayerId).Cash <= 0)
            return [];
        int mine = NetWorth.Calculate(ctx.State, ctx.Content, PlayerId);
        return ctx.State.ActivePlayers.Where(p => p.Id != PlayerId && p.Cash > 0 && NetWorth.Calculate(ctx.State, ctx.Content, p.Id) > mine);
    }

    public override IReadOnlyList<GameAction> LegalActions(EngineContext ctx, PendingDecision decision) => decision.Kind switch
    {
        // Any whole-dollar stake up to the limit is legal too (see IsLegal). These are just examples.
        DecisionKind.CasinoBet => [new PlayDicePoker(0), new PlayDicePoker(Math.Min(10, decision.MaxBet)), new NoThanks()],
        DecisionKind.KeepDice => PokerDice.KeepExamples,
        _ => [.. ShowdownTargets(ctx).Select(p => (GameAction)new CallShowdown(p.Id)), new NoThanks()],
    };

    public override bool IsLegal(EngineContext ctx, PendingDecision decision, GameAction action) => decision.Kind switch
    {
        DecisionKind.CasinoBet => action is NoThanks
            || action is PlayDicePoker play && play.Stake >= 0 && play.Stake <= StakeLimit(ctx, ctx.Player(PlayerId).Cash),
        DecisionKind.KeepDice => PokerDice.IsLegalKeep(action),
        _ => base.IsLegal(ctx, decision, action),
    };

    public override void Receive(EngineContext ctx, PendingDecision decision, GameAction action)
    {
        switch (decision.Kind)
        {
            case DecisionKind.CasinoBet:
                Answered = true;
                WalkedAway = action is NoThanks;
                Stake = action is PlayDicePoker play ? play.Stake : 0;
                break;
            case DecisionKind.KeepDice:
                PokerDice.Reroll(ctx, PlayerId, Dice, ((KeepDice)action).Keep);
                Rerolled = true;
                break;
            default:
                if (action is CallShowdown call)
                    ctx.Push(new ShowdownStep
                    {
                        ChallengerId = PlayerId,
                        OpponentId = call.OpponentId,
                        Stake = ShowdownStep.StakeFor(ctx, PlayerId, call.OpponentId),
                        ChallengerDice = Dice.ToList(),
                        ChallengerDone = true,
                        Confirmed = true,
                    });
                break;
        }
    }
}

/// <summary>
/// A Showdown (decision #164): the challenger's Dice Poker hand against another player's. The challenged player
/// can't say no, but they see the hand first and may fold, paying a little instead. If they play, they roll
/// 5 dice, keep any and roll the rest once, and the better hand takes the stake from the other player.
/// From the Casino the challenger already has a hand. From the Showdown card they roll one first, and then decide
/// whether to go ahead with it (the card is used up either way).
/// </summary>
public sealed class ShowdownStep : Step
{
    public int ChallengerId { get; set; }
    public int OpponentId { get; set; }
    public int Stake { get; set; }

    public List<int> ChallengerDice { get; set; } = [];
    /// <summary>True once the challenger's hand is final (from the start, when it was made at the Casino).</summary>
    public bool ChallengerDone { get; set; }
    /// <summary>True once the challenger has gone ahead with the Showdown (from the start, at the Casino). Null: they called it off.</summary>
    public bool? Confirmed { get; set; }
    public bool Announced { get; set; }

    /// <summary>The challenged player's answer: null while waiting.</summary>
    public bool? Plays { get; set; }
    public List<int> OpponentDice { get; set; } = [];
    public bool OpponentRerolled { get; set; }

    /// <summary>What each side puts in: the Showdown stake, but no more than either has, or the Casino Regulation cap.</summary>
    public static int StakeFor(EngineContext ctx, int challengerId, int opponentId) =>
        Math.Min(Math.Min(ctx.Rules.ShowdownStake, RuleModifiers.MaxCasinoStake(ctx.State, ctx.Content) ?? int.MaxValue),
            Math.Min(ctx.Player(challengerId).Cash, ctx.Player(opponentId).Cash));

    public override StepResult Run(EngineContext ctx)
    {
        // The Showdown card: the challenger rolls their hand here first.
        if (ChallengerDice.Count == 0)
            ChallengerDice = PokerDice.RollAll(ctx, ChallengerId);
        if (!ChallengerDone)
            return StepResult.WaitFor(new PendingDecision(ChallengerId, DecisionKind.KeepDice,
                $"Showdown against {ctx.Player(OpponentId).Name}! You rolled {DicePoker.Describe(DicePoker.Score(ChallengerDice))}. Pick the dice to keep, then roll the rest once more.",
                Dice: ChallengerDice.ToList()));

        if (Confirmed == false)
            return StepResult.WaitFor(new PendingDecision(ChallengerId, DecisionKind.CallShowdown,
                $"You made {DicePoker.Describe(DicePoker.Score(ChallengerDice))}. Go ahead with the Showdown against {ctx.Player(OpponentId).Name}? They must beat it or fold.",
                Dice: ChallengerDice.ToList()));
        if (Confirmed == null)
        {
            ctx.Emit(new ShowdownCalledOff(ChallengerId, OpponentId));
            return StepResult.Done;
        }

        if (!Announced)
        {
            Announced = true;
            ctx.Emit(new ShowdownCalled(ChallengerId, OpponentId, Stake, ChallengerDice.ToList(), DicePoker.Score(ChallengerDice)));
        }

        int fold = Math.Min(ctx.Rules.ShowdownFoldPayment, Stake);
        var challenge = new ShowdownChallenge(ChallengerId, OpponentId, Stake, fold, ChallengerDice.ToList());
        if (Plays == null)
            return StepResult.WaitFor(new PendingDecision(OpponentId, DecisionKind.RespondToShowdown,
                $"{ctx.Player(ChallengerId).Name} calls you to a ${Stake} Showdown with {DicePoker.Describe(DicePoker.Score(ChallengerDice))}. Beat it, or fold and pay ${fold}.",
                Showdown: challenge));

        if (Plays == false)
        {
            int paid = Math.Min(fold, ctx.Player(OpponentId).Cash);
            ctx.Player(OpponentId).Cash -= paid;
            ctx.Player(ChallengerId).Cash += paid;
            ctx.Emit(new ShowdownFolded(ChallengerId, OpponentId, paid));
            return StepResult.Done;
        }

        if (OpponentDice.Count == 0)
            OpponentDice = PokerDice.RollAll(ctx, OpponentId);
        if (!OpponentRerolled)
            return StepResult.WaitFor(new PendingDecision(OpponentId, DecisionKind.KeepDice,
                $"You rolled {DicePoker.Describe(DicePoker.Score(OpponentDice))} against {DicePoker.Describe(DicePoker.Score(ChallengerDice))}. Pick the dice to keep, then roll the rest once more.",
                Dice: OpponentDice.ToList(), Showdown: challenge));

        Settle(ctx);
        return StepResult.Done;
    }

    private void Settle(EngineContext ctx)
    {
        int result = DicePoker.Compare(ChallengerDice, OpponentDice);
        if (result == 0)
        {
            // An exact tie pays nobody, unless someone wins ties (the Mogul, decision #93).
            string? challengerEdge = RuleModifiers.DiceTieBreaker(ctx.State, ctx.Content, ChallengerId);
            string? opponentEdge = RuleModifiers.DiceTieBreaker(ctx.State, ctx.Content, OpponentId);
            if (challengerEdge == null && opponentEdge == null)
            {
                ctx.Emit(new ShowdownTied(ChallengerId, OpponentId, DicePoker.Score(ChallengerDice)));
                return;
            }
            result = challengerEdge != null ? 1 : -1;
            ctx.Emit(new DiceTieWon(result > 0 ? ChallengerId : OpponentId, (challengerEdge ?? opponentEdge)!));
        }

        int winnerId = result > 0 ? ChallengerId : OpponentId;
        int loserId = result > 0 ? OpponentId : ChallengerId;
        // The stake may not be affordable any more; then it shrinks to what both have.
        int stake = Math.Min(Stake, Math.Min(ctx.Player(ChallengerId).Cash, ctx.Player(OpponentId).Cash));
        ctx.Player(winnerId).Cash += stake;
        ctx.Player(loserId).Cash -= stake;
        PokerHand challengerHand = DicePoker.Score(ChallengerDice), opponentHand = DicePoker.Score(OpponentDice);
        ctx.Emit(new ShowdownWon(winnerId, loserId, stake,
            result > 0 ? challengerHand : opponentHand, result > 0 ? opponentHand : challengerHand));
    }

    public override IReadOnlyList<GameAction> LegalActions(EngineContext ctx, PendingDecision decision) => decision.Kind switch
    {
        DecisionKind.KeepDice => PokerDice.KeepExamples,
        DecisionKind.CallShowdown => [new CallShowdown(OpponentId), new NoThanks()],
        _ => [new PlayShowdown(), new FoldShowdown()],
    };

    public override bool IsLegal(EngineContext ctx, PendingDecision decision, GameAction action) =>
        decision.Kind == DecisionKind.KeepDice ? PokerDice.IsLegalKeep(action) : base.IsLegal(ctx, decision, action);

    public override void Receive(EngineContext ctx, PendingDecision decision, GameAction action)
    {
        if (decision.Kind == DecisionKind.RespondToShowdown)
        {
            Plays = action is PlayShowdown;
            return;
        }
        if (decision.Kind == DecisionKind.CallShowdown)
        {
            Confirmed = action is CallShowdown ? true : null;
            return;
        }
        if (decision.PlayerId == ChallengerId && !ChallengerDone)
        {
            PokerDice.Reroll(ctx, ChallengerId, ChallengerDice, ((KeepDice)action).Keep);
            ChallengerDone = true;
        }
        else
        {
            PokerDice.Reroll(ctx, OpponentId, OpponentDice, ((KeepDice)action).Keep);
            OpponentRerolled = true;
        }
    }
}

/// <summary>Rolling Dice Poker dice: the first roll of 5, and the one reroll of the dice not kept.</summary>
internal static class PokerDice
{
    public static List<int> RollAll(EngineContext ctx, int playerId)
    {
        var dice = Enumerable.Range(0, DicePoker.DiceCount).Select(_ => ctx.Dice.RollDie()).ToList();
        ctx.Emit(new PokerDiceRolled(playerId, dice.ToList()));
        return dice;
    }

    /// <summary>Rolls every die whose position is not in <paramref name="keep"/>, first to last.</summary>
    public static void Reroll(EngineContext ctx, int playerId, List<int> dice, IReadOnlyList<int> keep)
    {
        for (int i = 0; i < dice.Count; i++)
            if (!keep.Contains(i))
                dice[i] = ctx.Dice.RollDie();
        ctx.Emit(new PokerDiceRolled(playerId, dice.ToList(), keep.Order().ToList()));
    }

    /// <summary>Every set of positions 0-4 is a legal keep, each named at most once (see IsLegalKeep). These are examples.</summary>
    public static readonly IReadOnlyList<GameAction> KeepExamples = [new KeepDice([0, 1, 2, 3, 4]), new KeepDice([])];

    public static bool IsLegalKeep(GameAction action) =>
        action is KeepDice keep && keep.Keep.All(i => i >= 0 && i < DicePoker.DiceCount) && keep.Keep.Distinct().Count() == keep.Keep.Count;
}

/// <summary>
/// The Stadium (Section 15.3): choose any color group. Its rent is doubled
/// until the start of your next turn.
/// </summary>
public sealed class StadiumStep : Step
{
    public int PlayerId { get; set; }
    public bool Answered { get; set; }

    public override StepResult Run(EngineContext ctx) => Answered
        ? StepResult.Done
        : StepResult.WaitFor(new PendingDecision(PlayerId, DecisionKind.ChooseStadiumGroup,
            $"Big game at the Stadium! Choose a color group: its rent {Times(RuleModifiers.StadiumMultiplier(ctx.State, ctx.Content, PlayerId))} until your next turn."));

    private static string Times(int multiplier) => multiplier switch { 2 => "doubles", 3 => "triples", _ => $"goes up {multiplier} times" };

    public override IReadOnlyList<GameAction> LegalActions(EngineContext ctx, PendingDecision decision) =>
        ctx.Content.Groups.Keys.Select(g => (GameAction)new PickStadiumGroup(g)).ToList();

    public override void Receive(EngineContext ctx, PendingDecision decision, GameAction action)
    {
        Answered = true;
        var pick = (PickStadiumGroup)action;
        // One boost per player: a second visit before their next turn moves it.
        ctx.State.StadiumBoosts.RemoveAll(b => b.ChosenById == PlayerId);
        ctx.State.StadiumBoosts.Add(new StadiumBoost(pick.GroupId, PlayerId, RuleModifiers.StadiumMultiplier(ctx.State, ctx.Content, PlayerId)));
        ctx.Emit(new StadiumChosen(PlayerId, pick.GroupId));
    }
}
