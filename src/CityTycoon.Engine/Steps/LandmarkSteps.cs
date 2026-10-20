using CityTycoon.Engine.Actions;
using CityTycoon.Engine.Events;
using CityTycoon.Engine.Modifiers;
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
            $"You're visiting the Hospital. Buy an Insured marker for ${cost}? It cancels one Hospital bill."));
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
        ctx.Emit(new InsuranceBought(PlayerId, cost));
    }
}

/// <summary>
/// The Casino (Section 15.3): one optional bet, with no maximum. Either
/// High/Low against the bank, or a Dice Duel against another player who may say no.
/// </summary>
public sealed class CasinoStep : Step
{
    public int PlayerId { get; set; }

    /// <summary>The bet chosen, or null while waiting (and after "no thanks").</summary>
    public BetHighLow? HighLow { get; set; }
    public ChallengeDuel? Duel { get; set; }

    public bool Answered { get; set; }

    /// <summary>The High/Low roll, once made (0 means not rolled yet). A losing Hustler may reroll it once.</summary>
    public int Die1 { get; set; }
    public int Die2 { get; set; }

    /// <summary>True once the Hustler has been asked about a reroll, so they are asked at most once per visit.</summary>
    public bool RerollOffered { get; set; }

    public override StepResult Run(EngineContext ctx)
    {
        PlayerState player = ctx.Player(PlayerId);
        if (!Answered)
        {
            if (player.Cash <= 0)
                return StepResult.Done;
            return StepResult.WaitFor(new PendingDecision(PlayerId, DecisionKind.CasinoBet,
                "Welcome to the Casino. Make one bet, or walk on by.", MaxBet: StakeLimit(ctx, player.Cash)));
        }

        if (HighLow is { } bet)
        {
            if (Die1 == 0)
            {
                Die1 = ctx.Dice.RollDie();
                Die2 = ctx.Dice.RollDie();
            }
            // The Hustler may roll again, but only if this roll lost (Section 5). They are asked once.
            if (!RerollOffered && !IsRight(ctx, bet) && RuleModifiers.CasinoReroll(ctx.State, ctx.Content, PlayerId))
            {
                RerollOffered = true;
                return StepResult.WaitFor(new PendingDecision(PlayerId, DecisionKind.ChooseReroll,
                    $"You bet {(bet.High ? "High" : "Low")} and rolled {Die1} + {Die2} = {Die1 + Die2}, which loses. Use your Hustler reroll?",
                    Dice: [Die1, Die2]));
            }
            PlayHighLow(ctx, player, bet);
        }
        else if (Duel is { } duel)
        {
            ctx.Push(new DiceDuelStep { ChallengerId = PlayerId, OpponentId = duel.OpponentId, Stake = duel.Amount });
        }
        return StepResult.Done;
    }

    /// <summary>The most this player can stake: their cash, or the Casino Regulation cap if one is in force.</summary>
    private static int StakeLimit(EngineContext ctx, int cash) =>
        Math.Min(cash, RuleModifiers.MaxCasinoStake(ctx.State, ctx.Content) ?? int.MaxValue);

    private bool IsRight(EngineContext ctx, BetHighLow bet) =>
        bet.High ? Die1 + Die2 >= ctx.Rules.CasinoHighMin : Die1 + Die2 <= ctx.Rules.CasinoLowMax;

    private void PlayHighLow(EngineContext ctx, PlayerState player, BetHighLow bet)
    {
        int net = !IsRight(ctx, bet) ? -bet.Amount
            : Die1 == Die2 ? bet.Amount * ctx.Rules.CasinoDoublesPayout
            : bet.Amount;
        player.Cash += net;
        ctx.Emit(new CasinoHighLow(PlayerId, bet.Amount, bet.High, Die1, Die2, net));
    }

    public override IReadOnlyList<GameAction> LegalActions(EngineContext ctx, PendingDecision decision)
    {
        if (decision.Kind == DecisionKind.ChooseReroll)
            return [new RerollDice(), new NoThanks()];
        // Any whole-dollar amount up to the limit is legal too (see IsLegal).
        // These are just examples, so bots and the screen have something to start from.
        int example = Math.Min(10, decision.MaxBet);
        var actions = new List<GameAction> { new NoThanks(), new BetHighLow(example, High: true), new BetHighLow(example, High: false) };
        foreach (PlayerState other in Opponents(ctx))
            actions.Add(new ChallengeDuel(other.Id, Math.Min(example, other.Cash)));
        return actions;
    }

    public override bool IsLegal(EngineContext ctx, PendingDecision decision, GameAction action)
    {
        if (decision.Kind == DecisionKind.ChooseReroll)
            return action is RerollDice or NoThanks;
        int cash = ctx.Player(PlayerId).Cash;
        return action switch
        {
            NoThanks => true,
            BetHighLow bet => bet.Amount >= 1 && bet.Amount <= StakeLimit(ctx, cash),
            // Both players put in the same amount, up to whoever has less cash.
            ChallengeDuel duel => Opponents(ctx).Any(p => p.Id == duel.OpponentId)
                && duel.Amount >= 1 && duel.Amount <= StakeLimit(ctx, Math.Min(cash, ctx.Player(duel.OpponentId).Cash)),
            _ => false,
        };
    }

    public override void Receive(EngineContext ctx, PendingDecision decision, GameAction action)
    {
        if (decision.Kind == DecisionKind.ChooseReroll)
        {
            if (action is RerollDice)
            {
                ctx.Emit(new CasinoRerolled(PlayerId, "High/Low roll"));
                Die1 = Die2 = 0;   // Run rolls again
            }
            return;
        }
        Answered = true;
        HighLow = action as BetHighLow;
        Duel = action as ChallengeDuel;
    }

    /// <summary>Players who can be challenged: still in the game, with some cash.</summary>
    private IEnumerable<PlayerState> Opponents(EngineContext ctx) =>
        ctx.State.ActivePlayers.Where(p => p.Id != PlayerId && p.Cash > 0);
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
            "Big game at the Stadium! Choose a color group: its rent doubles until your next turn."));

    public override IReadOnlyList<GameAction> LegalActions(EngineContext ctx, PendingDecision decision) =>
        ctx.Content.Groups.Keys.Select(g => (GameAction)new PickStadiumGroup(g)).ToList();

    public override void Receive(EngineContext ctx, PendingDecision decision, GameAction action)
    {
        Answered = true;
        var pick = (PickStadiumGroup)action;
        // One boost per player: a second visit before their next turn moves it.
        ctx.State.StadiumBoosts.RemoveAll(b => b.ChosenById == PlayerId);
        ctx.State.StadiumBoosts.Add(new StadiumBoost(pick.GroupId, PlayerId));
        ctx.Emit(new StadiumChosen(PlayerId, pick.GroupId));
    }
}
