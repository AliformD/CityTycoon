using CityTycoon.Engine.Actions;
using CityTycoon.Engine.Events;
using CityTycoon.Engine.Steps;
using CityTycoon.Engine.Tests.Helpers;

namespace CityTycoon.Engine.Tests.Auctions;

/// <summary>Section 8.4 and 8.5: down payments and pending properties.</summary>
public class DownPaymentTests
{
    /// <summary>Player 0 lands on Market Street (minimum bid $90); player 1 passes.</summary>
    private static TestGame WinWithDownPayment(int bid)
    {
        var game = new TestGame();
        game.RollOnto(0, "market-street");
        game.Do(0, new PlaceBid(bid, DownPayment: true));
        game.Do(1, new PassBid());
        return game;
    }

    [Theory]
    [InlineData(90, 20)]   // 20% of $90 is $18, rounded up to $20
    [InlineData(100, 20)]
    [InlineData(150, 30)]
    [InlineData(160, 40)]  // $32 rounds up to $40
    public void Deposit_Is20Percent_RoundedUpTo10(int bid, int deposit) =>
        Assert.Equal(deposit, AuctionStep.Deposit(new TestGame().Engine.Content.Rules, bid));

    [Fact]
    public void WinningWithADownPayment_PaysTheDeposit_AndThePropertyIsPending()
    {
        var game = WinWithDownPayment(150);

        var owned = game.Property("market-street");
        Assert.Equal(0, owned.OwnerId);
        Assert.True(owned.IsPending);
        Assert.Equal(120, owned.PendingBalance);
        Assert.Equal(1500 - 30, game.Player(0).Cash);
        Assert.Contains(game.State.Log, e => e is AuctionWon { DownPayment: true, PaidNow: 30, Price: 150 });
    }

    [Fact]
    public void ADownPaymentBid_OnlyNeedsCashForTheDeposit()
    {
        var game = new TestGame();
        game.Player(0).Cash = 40;
        game.RollOnto(0, "market-street");

        var decision = game.DecisionFor(0)!;
        Assert.Equal(40, decision.MaxBid);
        Assert.Equal(200, decision.MaxDownPaymentBid); // a $200 bid needs a $40 deposit
        Assert.False(game.Engine.Submit(0, new PlaceBid(210, DownPayment: true)).Accepted);
        game.Do(0, new PlaceBid(200, DownPayment: true));
    }

    [Fact]
    public void APlayerWhoCanOnlyAffordADownPayment_IsStillAskedToBid()
    {
        var game = new TestGame();
        game.Player(1).Cash = 20;
        game.Player(1).Loans = 2;
        game.RollOnto(0, "market-street");
        Assert.NotNull(game.DecisionFor(1));
    }

    [Fact]
    public void YouCanHoldAtMostTwoPendingProperties()
    {
        var game = new TestGame();
        foreach (string id in new[] { "mill-street", "cobble-lane" })
        {
            game.Give(0, id);
            game.Property(id).IsPending = true;
            game.Property(id).PendingBalance = 40;
        }
        game.RollOnto(0, "market-street");

        Assert.Equal(0, game.DecisionFor(0)!.MaxDownPaymentBid);
        Assert.False(game.Engine.Submit(0, new PlaceBid(90, DownPayment: true)).Accepted);
    }

    [Fact]
    public void APendingProperty_CollectsNoRent_AndDoesntCountTowardsItsGroup()
    {
        var game = new TestGame();
        game.Give(1, "market-street", "baker-street", "fountain-square");
        game.Property("fountain-square").IsPending = true;
        game.Property("fountain-square").PendingBalance = 100;

        game.RollOnto(0, "market-street");
        Assert.Equal(1500 - 14, game.Player(0).Cash); // base rent, not doubled: the group isn't complete
    }

    [Fact]
    public void APendingProperty_CantBeMortgaged()
    {
        var game = WinWithDownPayment(150);
        game.EndTurn(0);
        game.SkipTurn(1);
        Assert.Contains("pending", game.Engine.WhyIllegal(0, new Mortgage("market-street")));
    }

    [Fact]
    public void TakingALoan_DuringAnAuction_LetsYouBidMore()
    {
        var game = new TestGame();
        game.Player(0).Cash = 100;
        game.RollOnto(0, "market-street");

        game.Do(0, new TakeLoan());

        Assert.Equal(300, game.DecisionFor(0)!.MaxBid);
        game.Do(0, new PlaceBid(250));
    }

    [Fact]
    public void YouCantDoOtherManagementActions_DuringAnAuction()
    {
        var game = new TestGame();
        game.Give(0, "mill-street");
        game.RollOnto(0, "market-street");
        Assert.Equal("You can't mortgage right now.", game.Engine.WhyIllegal(0, new Mortgage("mill-street")));
    }

    [Fact]
    public void TheBalance_IsPaidAutomatically_AtGo()
    {
        var game = new TestGame();
        game.Give(0, "market-street");
        game.Property("market-street").IsPending = true;
        game.Property("market-street").PendingBalance = 120;

        game.PlaceBefore(0, "go", 5);
        game.Roll(0, 1, 4); // passes GO

        Assert.False(game.Property("market-street").IsPending);
        Assert.Equal(0, game.Property("market-street").PendingBalance);
        Assert.Equal(1500 + 200 - 120, game.Player(0).Cash);
        Assert.Contains(game.State.Log, e => e is DownPaymentPaid { Amount: 120 });
    }

    [Fact]
    public void ShortAtGo_YouMayRaiseMoney_OrGiveThePropertyUp()
    {
        var game = new TestGame(players: 3);
        game.Give(0, "market-street");
        game.Property("market-street").IsPending = true;
        game.Property("market-street").PendingBalance = 300;
        game.Player(0).Cash = 0;

        game.PlaceBefore(0, "go", 5);
        game.Roll(0, 1, 4); // $200 salary, $300 due

        var decision = game.DecisionFor(0)!;
        Assert.Equal(DecisionKind.RaiseMoney, decision.Kind);
        Assert.Equal([new ForfeitProperty()], game.MainActions(0));

        game.Do(0, new ForfeitProperty());

        Assert.Null(game.Property("market-street").OwnerId);
        Assert.Contains(game.State.Log, e => e is PropertyForfeited { PlayerId: 0 });
        // Re-auctioned right away; the old owner can't bid.
        Assert.Equal([1, 2], game.Engine.GetPendingDecisions().Select(d => d.PlayerId));
        Assert.Equal(90, game.DecisionFor(1)!.MinBid);
    }

    [Fact]
    public void ShortAtGo_TakingALoan_PaysTheBalance()
    {
        var game = new TestGame();
        game.Give(0, "market-street");
        game.Property("market-street").IsPending = true;
        game.Property("market-street").PendingBalance = 300;
        game.Player(0).Cash = 0;
        game.PlaceBefore(0, "go", 5);
        game.Roll(0, 1, 4);

        game.Do(0, new TakeLoan());

        Assert.False(game.Property("market-street").IsPending);
        Assert.Equal(100, game.Player(0).Cash);
    }

    [Fact]
    public void ShortAtGo_WithNoWayToRaiseIt_ThePropertyIsForfeitedAutomatically()
    {
        var game = new TestGame();
        game.Give(0, "market-street");
        game.Property("market-street").IsPending = true;
        game.Property("market-street").PendingBalance = 500;
        game.Player(0).Cash = 0;
        game.Player(0).Loans = 2;

        game.PlaceBefore(0, "go", 5);
        game.Roll(0, 1, 4);

        Assert.Null(game.Property("market-street").OwnerId);
        Assert.Equal(200, game.Player(0).Cash); // the salary stays; the deposit is lost
        Assert.Equal([1], game.Engine.GetPendingDecisions().Select(d => d.PlayerId));
    }
}
