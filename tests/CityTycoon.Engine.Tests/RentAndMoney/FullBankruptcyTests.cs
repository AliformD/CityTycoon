using CityTycoon.Engine.Events;
using CityTycoon.Engine.Steps;
using CityTycoon.Engine.Tests.Helpers;

namespace CityTycoon.Engine.Tests.RentAndMoney;

/// <summary>Section 15.10 with buildings, loans and pending properties.</summary>
public class FullBankruptcyTests
{
    [Fact]
    public void ToAPlayer_BuildingsAreSold_LoansRepaidFirst_ThenTheyGetTheRest()
    {
        var game = new TestGame(players: 3);
        game.Give(1, "platinum-row");
        game.Property("platinum-row").Level = 3; // $1,400 rent
        game.Give(0, "mill-street", "cobble-lane");
        game.Property("mill-street").Level = 1;
        game.Property("cobble-lane").Level = 1;
        game.Player(0).Cash = 400;
        game.Player(0).Loans = 1;

        game.RollOnto(0, "platinum-row");

        Assert.True(game.Player(0).IsBankrupt);
        // $400 + $50 from the houses - $250 for the loan = $200 to the creditor.
        Assert.Equal(1500 + 200, game.Player(1).Cash);
        Assert.Equal(1, game.Property("mill-street").OwnerId);
        Assert.Equal(0, game.Property("mill-street").Level);
        Assert.Equal(0, game.Player(0).Loans);
    }

    [Fact]
    public void PendingProperties_GoBackToTheBank_AndAreAuctioned()
    {
        var game = new TestGame(players: 3);
        game.Give(1, "platinum-row");
        game.Property("platinum-row").Level = 3;
        game.Give(0, "market-street");
        game.Property("market-street").IsPending = true;
        game.Property("market-street").PendingBalance = 100;
        game.Player(0).Cash = 100;
        game.Player(0).Loans = 2;

        game.RollOnto(0, "platinum-row");

        Assert.True(game.Player(0).IsBankrupt);
        Assert.Null(game.Property("market-street").OwnerId);
        Assert.False(game.Property("market-street").IsPending);
        Assert.Contains(game.State.Log, e => e is AuctionStarted { PropertyId: "market-street" });
    }

    [Fact]
    public void ToTheBank_BuildingsAreRemoved_PermitsReturn_AndEveryPropertyIsAuctioned()
    {
        var game = new TestGame(players: 3);
        game.Give(0, "mill-street", "cobble-lane");
        game.Property("mill-street").Level = 5;
        game.Property("cobble-lane").Level = 5;
        game.State.HotelPermits -= 2;
        int permits = game.State.HotelPermits;
        game.Player(0).Cash = 0;
        game.Player(0).Loans = 2;
        game.Property("platinum-row").OwnerId = 1;
        game.Property("platinum-row").Level = 6; // $3,000 rent: more than they can raise

        game.RollOnto(0, "platinum-row");
        Assert.True(game.Player(0).IsBankrupt);
        Assert.Equal(permits + 2, game.State.HotelPermits);
        Assert.Equal(0, game.Property("mill-street").Level);
    }

    [Fact]
    public void OwingTheBank_WithBuildings_EverythingIsAuctioned()
    {
        var game = new TestGame(players: 3);
        game.Give(0, "mill-street", "cobble-lane");
        game.Property("mill-street").Level = 1;
        game.Property("cobble-lane").Level = 1;
        game.Player(0).Cash = 0;
        game.Player(0).Loans = 2;

        // Raisable: $50 from houses + $60 in mortgages = $110, less than the $200 income tax.
        game.RollOnto(0, "income-tax");

        Assert.True(game.Player(0).IsBankrupt);
        Assert.Null(game.Property("mill-street").OwnerId);
        Assert.Null(game.Property("cobble-lane").OwnerId);

        // One auction at a time, in board order.
        Assert.Equal("mill-street", game.State.Log.OfType<AuctionStarted>().Last().PropertyId);
        game.EveryonePasses();
        Assert.Equal("cobble-lane", game.State.Log.OfType<AuctionStarted>().Last().PropertyId);
    }
}
