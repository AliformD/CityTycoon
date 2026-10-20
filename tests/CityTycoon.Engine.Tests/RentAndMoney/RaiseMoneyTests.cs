using CityTycoon.Engine.Actions;
using CityTycoon.Engine.Events;
using CityTycoon.Engine.Tests.Helpers;

namespace CityTycoon.Engine.Tests.RentAndMoney;

/// <summary>Owing more cash than you have (Sections 9.3, 15.7, 15.10).</summary>
public class RaiseMoneyTests
{
    [Fact]
    public void ShortOfCash_YouAreAskedToRaiseMoney()
    {
        var game = new TestGame();
        game.Give(0, "mill-street");
        game.Player(0).Cash = 100;
        game.RollOnto(0, "income-tax"); // $200

        var decision = game.DecisionFor(0)!;
        Assert.Equal(DecisionKind.RaiseMoney, decision.Kind);
        Assert.Equal(200, decision.AmountOwed);
        Assert.Contains(game.State.Log, e => e is RaisingMoney { AmountOwed: 200 });
        Assert.Equal([new Mortgage("mill-street"), new TakeLoan()], game.Engine.GetLegalActions(0));
    }

    [Fact]
    public void OnceYouHaveEnough_TheDebtIsPaid_AndTheTurnGoesOn()
    {
        var game = new TestGame();
        game.Give(0, "mill-street");
        game.Player(0).Cash = 100;
        game.RollOnto(0, "income-tax");

        game.Do(0, new Mortgage("mill-street")); // $130: still short
        Assert.Equal(DecisionKind.RaiseMoney, game.DecisionFor(0)!.Kind);
        game.Do(0, new TakeLoan());               // $330: enough

        Assert.Equal(130, game.Player(0).Cash);
        Assert.Equal(DecisionKind.PostMoveActions, game.DecisionFor(0)!.Kind);
        Assert.Single(game.State.Log.OfType<RaisingMoney>());
    }

    [Fact]
    public void WhileRaisingMoney_YouCantSpendMoney()
    {
        var game = new TestGame();
        game.Give(0, "mill-street");
        game.Property("mill-street").IsMortgaged = true;
        game.Player(0).Cash = 100;
        game.RollOnto(0, "income-tax");

        Assert.Equal("You can't unmortgage right now.", game.Engine.WhyIllegal(0, new Unmortgage("mill-street")));
        Assert.Equal("You can't repay a loan right now.", game.Engine.WhyIllegal(0, new RepayLoan()));
    }

    [Fact]
    public void SellingBuildings_CanRaiseMoney()
    {
        var game = new TestGame();
        game.Give(0, "mill-street", "cobble-lane");
        game.Property("mill-street").Level = 1;
        game.Property("cobble-lane").Level = 1;
        game.Player(0).Cash = 190;
        game.Player(0).Loans = 2;
        game.PlaceBefore(0, "income-tax", 2);
        game.Roll(0, 1, 1); // doubles, but that's fine for this test

        game.Do(0, new SellBuilding("mill-street"));
        Assert.Equal(15, game.Player(0).Cash); // 190 + 25 - 200
    }
}
