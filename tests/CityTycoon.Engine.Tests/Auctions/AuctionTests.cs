using CityTycoon.Engine.Actions;
using CityTycoon.Engine.Content;
using CityTycoon.Engine.Events;
using CityTycoon.Engine.Steps;
using CityTycoon.Engine.Tests.Helpers;

namespace CityTycoon.Engine.Tests.Auctions;

/// <summary>Section 8: sealed-bid auctions (pay in full).</summary>
public class AuctionTests
{
    [Fact]
    public void LandingOnAnUnownedProperty_StartsAnAuction_ForEveryPlayer()
    {
        var game = new TestGame(players: 3);
        game.RollOnto(0, "market-street");

        var decisions = game.Engine.GetPendingDecisions();
        Assert.Equal([0, 1, 2], decisions.Select(d => d.PlayerId));
        Assert.All(decisions, d => Assert.Equal(DecisionKind.SubmitBid, d.Kind));
    }

    [Theory]
    [InlineData(60, 30)]   // Mill Street: 50% of $60 = $30
    [InlineData(350, 180)] // Skyline Drive: 50% of $350 = $175, rounded up to $180
    [InlineData(200, 100)] // a railroad
    public void MinimumBid_Is50PercentOfAssessedValue_RoundedUpTo10(int assessed, int expected)
    {
        Assert.Equal(expected, AuctionStep.MinimumBid(GameContent.Default.Rules, assessed, AuctionKind.Normal));
    }

    [Fact]
    public void HighestBidWins_AndPaysTheirOwnBidToTheBank()
    {
        var game = new TestGame(players: 3);
        game.RollOnto(0, "market-street");
        game.Do(0, new PlaceBid(100));
        game.Do(1, new PlaceBid(150));
        game.Do(2, new PassBid());

        Assert.Equal(1, game.Property("market-street").OwnerId);
        Assert.Equal(1350, game.Player(1).Cash);
        Assert.Equal(1500, game.Player(0).Cash);
        Assert.Equal(1500, game.Player(2).Cash);
    }

    [Fact]
    public void NobodyBids_ThePropertyStaysUnowned()
    {
        var game = new TestGame();
        var events = new List<GameEvent>();
        game.RollOnto(0, "market-street");
        game.EveryonePasses();

        Assert.Null(game.Property("market-street").OwnerId);
        Assert.Contains(game.State.Log, e => e is AuctionNoBids);
    }

    [Fact]
    public void BidsStaySealed_UntilEveryoneHasAnswered()
    {
        var game = new TestGame(players: 3);
        game.RollOnto(0, "market-street");
        game.Do(0, new PlaceBid(100));

        Assert.DoesNotContain(game.State.Log, e => e is BidsRevealed);
        Assert.Equal([1, 2], game.Engine.GetPendingDecisions().Select(d => d.PlayerId));
    }

    [Theory]
    [InlineData(80)]   // below the $90 minimum for Market Street ($180)
    [InlineData(95)]   // not a multiple of $10
    [InlineData(1510)] // more than the player's cash
    public void IllegalBids_AreRejected(int amount)
    {
        var game = new TestGame();
        game.RollOnto(0, "market-street");
        Assert.False(game.Engine.Submit(0, new PlaceBid(amount)).Accepted);
    }

    [Fact]
    public void Tie_TheLanderWins()
    {
        var game = new TestGame(players: 3);
        game.RollOnto(0, "market-street");
        game.Do(0, new PlaceBid(100));
        game.Do(1, new PlaceBid(100));
        game.Do(2, new PlaceBid(100));
        Assert.Equal(0, game.Property("market-street").OwnerId);
    }

    [Fact]
    public void Tie_WithoutTheLander_GoesToTheNextPlayerInTurnOrderAfterTheLander()
    {
        var game = new TestGame(players: 3);
        game.SkipTurn(0);
        game.RollOnto(1, "market-street"); // player 1 is the lander
        game.Do(0, new PlaceBid(100));
        game.Do(1, new PassBid());
        game.Do(2, new PlaceBid(100));
        Assert.Equal(2, game.Property("market-street").OwnerId); // 2 comes right after 1; 0 comes after 2
    }

    [Fact]
    public void PlayersWhoCantAffordTheMinimum_PassAutomatically()
    {
        var game = new TestGame(players: 3);
        // $10 can't cover even a down payment on $90, and they have no loans left.
        game.Player(2).Cash = 10;
        game.Player(2).Loans = 2;
        game.RollOnto(0, "market-street"); // minimum $90
        Assert.Null(game.DecisionFor(2));
    }
}
