using CityTycoon.Engine.Actions;
using CityTycoon.Engine.Events;
using CityTycoon.Engine.Rules;
using CityTycoon.Engine.Steps;
using CityTycoon.Engine.Tests.Helpers;

namespace CityTycoon.Engine.Tests.Landmarks;

/// <summary>Section 15.3: the Hospital, Stadium and City Park, and Insured markers.</summary>
public class LandmarkTests
{
    [Fact]
    public void VisitingTheHospital_OffersAnInsuredMarker()
    {
        var game = new TestGame();
        game.RollOnto(0, "hospital");
        Assert.Equal(DecisionKind.HospitalVisit, game.DecisionFor(0)!.Kind);

        game.Do(0, new BuyInsurance());

        Assert.True(game.Player(0).IsInsured);
        Assert.Equal(1450, game.Player(0).Cash);
    }

    [Fact]
    public void YouCanHoldOnlyOneInsuredMarker()
    {
        var game = new TestGame();
        game.Player(0).IsInsured = true;
        game.RollOnto(0, "hospital");
        Assert.Equal(DecisionKind.PostMoveActions, game.DecisionFor(0)!.Kind);
    }

    [Fact]
    public void SayingNoThanks_ChangesNothing()
    {
        var game = new TestGame();
        game.RollOnto(0, "hospital");
        game.Do(0, new NoThanks());
        Assert.False(game.Player(0).IsInsured);
        Assert.Equal(1500, game.Player(0).Cash);
    }

    [Fact]
    public void SentToTheHospital_YouPayTheBill_AndRollOneDieNextTurn()
    {
        var game = new TestGame();
        var ctx = new EngineContext(game.State, game.Engine.Content, game.Dice);

        Movement.SendToHospital(ctx, game.Player(1), "a test");

        Assert.Equal(game.State.IndexOf("hospital"), game.Player(1).Position);
        Assert.True(game.Player(1).IsRecovering);
        var bill = Assert.IsType<PaymentStep>(game.State.Stack[^1]);
        Assert.Equal(100, bill.Amount);
        Assert.Equal(PaymentReason.Hospital, bill.Reason);
    }

    [Fact]
    public void SentToTheHospital_WithAnInsuredMarker_ItsFree_AndYouRollNormally()
    {
        var game = new TestGame();
        game.Player(1).IsInsured = true;
        var ctx = new EngineContext(game.State, game.Engine.Content, game.Dice);
        int stackBefore = game.State.Stack.Count;

        Movement.SendToHospital(ctx, game.Player(1), "a test");

        Assert.False(game.Player(1).IsInsured);
        Assert.False(game.Player(1).IsRecovering);
        Assert.Equal(stackBefore, game.State.Stack.Count);
        Assert.Contains(game.State.Log, e => e is InsuranceUsed { PlayerId: 1 });
    }

    [Fact]
    public void Recovering_YouRollOneDie_AndCantPrecisionRoll()
    {
        var game = new TestGame();
        game.Player(0).IsRecovering = true;
        // The flag is read when a turn starts, so play this turn and the next player's first.
        game.SkipTurn(0);
        game.SkipTurn(1);

        game.Player(0).Momentum = 3;
        Assert.DoesNotContain(new PrecisionRoll(), game.Engine.GetLegalActions(0));
        game.PlaceBefore(0, "jail", 4);
        game.Dice.Add(4);
        game.Do(0, new RollDice());
        game.Do(0, new Nudge(0));

        Assert.Equal(game.State.IndexOf("jail"), game.Player(0).Position);
        Assert.Contains(game.State.Log, e => e is DiceRolled { IsOneDie: true, Die1: 4 });
        Assert.False(game.Player(0).IsRecovering);
    }

    [Fact]
    public void TheStadium_DoublesAGroupsRent_UntilTheChoosersNextTurn()
    {
        var game = new TestGame(players: 3);
        game.Give(2, "market-street");
        game.RollOnto(0, "stadium");
        Assert.Equal(DecisionKind.ChooseStadiumGroup, game.DecisionFor(0)!.Kind);
        game.Do(0, new PickStadiumGroup("orange"));
        game.EndTurn(0);

        game.RollOnto(1, "market-street");
        Assert.Equal(1500 - 28, game.Player(1).Cash); // $14 doubled
        game.EndTurn(1);
        game.SkipTurn(2);

        // Player 0's next turn: the boost is over.
        Assert.Empty(game.State.StadiumBoosts);
        Assert.Contains(game.State.Log, e => e is StadiumEnded { GroupId: "orange" });
    }

    [Fact]
    public void CityPark_CanGiveAToken()
    {
        var game = new TestGame();
        game.RollOnto(0, "city-park");
        Assert.Equal(DecisionKind.CityParkChoice, game.DecisionFor(0)!.Kind);
        game.Do(0, new TakeMomentum());
        Assert.Equal(2, game.Player(0).Momentum);
    }
}
