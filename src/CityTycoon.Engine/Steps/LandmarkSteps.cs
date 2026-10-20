using CityTycoon.Engine.Actions;
using CityTycoon.Engine.Events;
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
        if (Answered || player.IsInsured || player.Cash < ctx.Rules.InsuredMarkerCost)
            return StepResult.Done;

        return StepResult.WaitFor(new PendingDecision(PlayerId, DecisionKind.HospitalVisit,
            $"You're visiting the Hospital. Buy an Insured marker for ${ctx.Rules.InsuredMarkerCost}? It cancels one Hospital bill."));
    }

    public override IReadOnlyList<GameAction> LegalActions(EngineContext ctx, PendingDecision decision) =>
        [new BuyInsurance(), new NoThanks()];

    public override void Receive(EngineContext ctx, PendingDecision decision, GameAction action)
    {
        Answered = true;
        if (action is not BuyInsurance)
            return;

        PlayerState player = ctx.Player(PlayerId);
        player.Cash -= ctx.Rules.InsuredMarkerCost;
        player.IsInsured = true;
        ctx.Emit(new InsuranceBought(PlayerId, ctx.Rules.InsuredMarkerCost));
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

    public override StepResult Run(EngineContext ctx)
    {
        PlayerState player = ctx.Player(PlayerId);
        if (!Answered)
        {
            if (player.Cash <= 0)
                return StepResult.Done;
            return StepResult.WaitFor(new PendingDecision(PlayerId, DecisionKind.CasinoBet,
                "Welcome to the Casino. Make one bet, or walk on by.", MaxBet: player.Cash));
        }

        if (HighLow is { } bet)
            PlayHighLow(ctx, player, bet);
        else if (Duel is { } duel)
            ctx.Push(new DiceDuelStep { ChallengerId = PlayerId, OpponentId = duel.OpponentId, Stake = duel.Amount });
        return StepResult.Done;
    }

    private void PlayHighLow(EngineContext ctx, PlayerState player, BetHighLow bet)
    {
        int die1 = ctx.Dice.RollDie(), die2 = ctx.Dice.RollDie();
        int total = die1 + die2;
        bool right = bet.High ? total >= ctx.Rules.CasinoHighMin : total <= ctx.Rules.CasinoLowMax;
        int net = !right ? -bet.Amount
            : die1 == die2 ? bet.Amount * ctx.Rules.CasinoDoublesPayout
            : bet.Amount;
        player.Cash += net;
        ctx.Emit(new CasinoHighLow(PlayerId, bet.Amount, bet.High, die1, die2, net));
    }

    public override IReadOnlyList<GameAction> LegalActions(EngineContext ctx, PendingDecision decision)
    {
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
        int cash = ctx.Player(PlayerId).Cash;
        return action switch
        {
            NoThanks => true,
            BetHighLow bet => bet.Amount >= 1 && bet.Amount <= cash,
            // Both players put in the same amount, up to whoever has less cash.
            ChallengeDuel duel => Opponents(ctx).Any(p => p.Id == duel.OpponentId)
                && duel.Amount >= 1 && duel.Amount <= Math.Min(cash, ctx.Player(duel.OpponentId).Cash),
            _ => false,
        };
    }

    public override void Receive(EngineContext ctx, PendingDecision decision, GameAction action)
    {
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
