using CityTycoon.Engine.Actions;
using CityTycoon.Engine.Events;
using CityTycoon.Engine.Tests.Helpers;

namespace CityTycoon.Engine.Tests.RentAndMoney;

/// <summary>
/// Section 15.10. In these tests the player has already used up their loans and
/// has nothing left to mortgage, so they can't raise enough and go bankrupt at once.
/// </summary>
public class BankruptcyTests
{
    [Fact]
    public void OwingAPlayer_TheyGetYourCashAndProperties()
    {
        var game = new TestGame(players: 3);
        game.Give(1, "platinum-row");
        game.Give(0, "mill-street");
        game.Player(0).Cash = 30;
        game.Player(0).Loans = 2;
        game.Property("mill-street").IsMortgaged = true;
        game.RollOnto(0, "platinum-row"); // rent $50

        Assert.True(game.Player(0).IsBankrupt);
        // The $30 went to the bank first, to pay back the two loans (Section 15.10).
        Assert.Equal(1500, game.Player(1).Cash);
        Assert.Equal(1, game.Property("mill-street").OwnerId);
    }

    [Fact]
    public void OwingTheBank_YourPropertiesAreAuctioned_AndYouCantBid()
    {
        var game = new TestGame(players: 3);
        game.Give(0, "market-street");
        game.Player(0).Cash = 150;
        game.Player(0).Loans = 2;
        game.Property("market-street").IsMortgaged = true;
        game.RollOnto(0, "income-tax"); // $200

        Assert.True(game.Player(0).IsBankrupt);
        Assert.Null(game.Property("market-street").OwnerId);
        Assert.Contains(game.State.Log, e => e is AuctionStarted a && a.PropertyId == "market-street");
        Assert.Equal([1, 2], game.Engine.GetPendingDecisions().Select(d => d.PlayerId));
    }

    [Fact]
    public void BankruptPlayers_AreSkippedInTurnOrder()
    {
        var game = new TestGame(players: 3);
        game.SkipTurn(0);
        game.Player(1).Cash = 100;
        game.Player(1).Loans = 2;
        game.RollOnto(1, "income-tax");

        Assert.True(game.Player(1).IsBankrupt);
        Assert.Equal(DecisionKind.ChooseMoveMethod, game.DecisionFor(2)!.Kind);
        game.SkipTurn(2);
        Assert.Equal(DecisionKind.ChooseMoveMethod, game.DecisionFor(0)!.Kind);
        Assert.Equal(2, game.State.Round);
    }
}
