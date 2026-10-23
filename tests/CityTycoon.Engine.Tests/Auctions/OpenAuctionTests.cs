using CityTycoon.Engine.Actions;
using CityTycoon.Engine.Events;
using CityTycoon.Engine.Tests.Helpers;

namespace CityTycoon.Engine.Tests.Auctions;

/// <summary>
/// docs/DECISIONS.md #91: the first property of a color is sold by sealed bids; once any other
/// property of that color has an owner, the rest are open auctions (players take turns to raise or drop out).
/// </summary>
public class OpenAuctionTests
{
    /// <summary>Three players; player 2 owns Market Street, then player 0 lands on Baker Street (also Orange).</summary>
    private static TestGame OpenAuction(string[]? characters = null)
    {
        var game = new TestGame(players: 3, characters: characters);
        game.Give(2, "market-street");
        game.RollOnto(0, "baker-street");
        return game;
    }

    [Fact]
    public void TheFirstPropertyOfAColor_IsSealed_EveryoneBidsAtOnce()
    {
        var game = new TestGame(players: 3);
        game.RollOnto(0, "market-street");
        Assert.Equal([0, 1, 2], game.Engine.GetPendingDecisions().Select(d => d.PlayerId));
        Assert.All(game.Engine.GetPendingDecisions(), d => Assert.False(d.OpenAuction));
        Assert.Contains(game.State.Log, e => e is AuctionStarted { Open: false });
    }

    [Fact]
    public void TheSecondProperty_IsOpen_AndTheLanderBidsFirst()
    {
        var game = OpenAuction();
        Assert.Contains(game.State.Log, e => e is AuctionStarted { Open: true });
        var asked = game.Engine.GetPendingDecisions();
        Assert.Equal([0], asked.Select(d => d.PlayerId));
        Assert.True(asked[0].OpenAuction);
        Assert.Null(asked[0].HighBidderId);
        Assert.Equal(90, asked[0].MinBid);
    }

    [Fact]
    public void TheQuestion_ListsTheBidders_AndWhoDroppedOut()
    {
        var game = OpenAuction();
        game.Do(0, new PlaceBid(100));
        game.Do(1, new PassBid());

        // Player 2's turn: the screen can show who leads, who is still in and who dropped out.
        var d = game.DecisionFor(2)!;
        Assert.Equal([0, 1, 2], d.Bidders);
        Assert.Equal([1], d.DroppedOut);
        Assert.Equal(0, d.HighBidderId);
        Assert.Equal(100, d.HighBid);
    }

    [Fact]
    public void ThePropertyOwnedByAnyoneCounts_EvenTheLander()
    {
        var game = new TestGame();
        game.Give(0, "market-street");
        game.RollOnto(0, "baker-street");
        Assert.True(game.DecisionFor(0)!.OpenAuction);
    }

    [Fact]
    public void PlayersTakeTurns_AndEachRaiseMustBeHigher()
    {
        var game = OpenAuction();
        game.Do(0, new PlaceBid(100));

        var next = game.DecisionFor(1)!;
        Assert.Equal([1], game.Engine.GetPendingDecisions().Select(d => d.PlayerId));
        Assert.Equal(0, next.HighBidderId);
        Assert.Equal(100, next.HighBid);
        Assert.Equal(110, next.MinBid);
        Assert.False(game.Engine.Submit(1, new PlaceBid(100)).Accepted);
        Assert.False(game.Engine.Submit(1, new PlaceBid(115)).Accepted);
        game.Do(1, new PlaceBid(110));

        Assert.Equal([2], game.Engine.GetPendingDecisions().Select(d => d.PlayerId));
        Assert.Contains(game.State.Log, e => e is BidRaised { PlayerId: 1, Amount: 110 });
    }

    [Fact]
    public void ItGoesRoundAgain_UntilEveryoneElseHasDroppedOut()
    {
        var game = OpenAuction();
        game.Do(0, new PlaceBid(100));
        game.Do(1, new PlaceBid(110));
        game.Do(2, new PassBid());
        Assert.Equal([0], game.Engine.GetPendingDecisions().Select(d => d.PlayerId)); // back to 0
        game.Do(0, new PlaceBid(120));
        game.Do(1, new PassBid());

        Assert.Equal(0, game.Property("baker-street").OwnerId);
        Assert.Contains(game.State.Log, e => e is AuctionWon { PlayerId: 0, Price: 120 });
        Assert.Equal(1500 - 120, game.Player(0).Cash);
    }

    [Fact]
    public void DroppingOut_IsForGood()
    {
        var game = OpenAuction();
        game.Do(0, new PlaceBid(100));
        game.Do(1, new PassBid());
        game.Do(2, new PlaceBid(110));
        game.Do(0, new PlaceBid(120));
        // Player 1 is out and is not asked again; player 2 is.
        Assert.Equal([2], game.Engine.GetPendingDecisions().Select(d => d.PlayerId));
    }

    [Fact]
    public void WhoeverLeads_IsNotAskedUntilSomeoneRaises()
    {
        var game = OpenAuction();
        game.Do(0, new PlaceBid(100));
        game.Do(1, new PassBid());
        game.Do(2, new PassBid());
        Assert.Equal(0, game.Property("baker-street").OwnerId);
        Assert.DoesNotContain(game.Engine.GetPendingDecisions(), d => d.Kind == DecisionKind.SubmitBid);
    }

    [Fact]
    public void IfEveryonePasses_NobodyBuys()
    {
        var game = OpenAuction();
        game.EveryonePasses();
        Assert.Null(game.Property("baker-street").OwnerId);
        Assert.Contains(game.State.Log, e => e is AuctionNoBids);
    }

    [Fact]
    public void TheLanderPassing_DoesNotEndIt_TheNextPlayerMayOpen()
    {
        var game = OpenAuction();
        game.Do(0, new PassBid());
        game.Do(1, new PlaceBid(90));
        game.Do(2, new PassBid());
        Assert.Equal(1, game.Property("baker-street").OwnerId);
    }

    [Fact]
    public void AnOpenAuction_TakesNoDownPayments()
    {
        // Decision #162: only silent auctions take down payments, so racing for a color group needs real cash.
        var game = OpenAuction();
        Assert.Equal(0, game.Engine.GetPendingDecisions()[0].MaxDownPaymentBid);
        Assert.False(game.Engine.Submit(0, new PlaceBid(200, DownPayment: true)).Accepted);
        game.Do(0, new PlaceBid(200));
        Assert.Equal(0, game.Engine.GetPendingDecisions()[0].MaxDownPaymentBid);
    }

    [Fact]
    public void AnOpenBidderShortOfCash_CannotStayInWithADownPayment()
    {
        var game = OpenAuction();
        game.Player(1).Cash = 150;
        game.Player(1).Loans = 2; // no loans left
        game.Do(0, new PlaceBid(200));
        // A $210 down payment would only need $50 now, but open auctions are pay in full, so player 1 drops out.
        Assert.Equal([2], game.Engine.GetPendingDecisions().Select(d => d.PlayerId));
        Assert.Contains(game.State.Log, e => e is AuctionPassed { PlayerId: 1 });
    }

    [Fact]
    public void WithTheHouseRule_AnOpenBid_CanBeADownPayment()
    {
        var content = TestContent.With(new() { ["startingCash"] = 1500, ["goSalary"] = 200, ["catchUpBonus"] = 100,
            ["casinoLandingFlip"] = 0, ["voteDeals"] = false, ["openAuctionDownPayments"] = true });
        var game = new TestGame(players: 3, content: content);
        game.Give(2, "market-street");
        game.RollOnto(0, "baker-street");
        game.Do(0, new PlaceBid(200, DownPayment: true));
        game.Do(1, new PassBid());
        game.Do(2, new PassBid());
        Assert.True(game.Property("baker-street").IsPending);
        Assert.Equal(160, game.Property("baker-street").PendingBalance);
        Assert.Equal(1500 - 40, game.Player(0).Cash);
    }

    [Fact]
    public void ABidderWhoCannotAffordTheNextBid_DropsOutOnTheirOwn()
    {
        var game = OpenAuction();
        game.Player(1).Cash = 20;
        game.Player(1).Loans = 2; // no loans left to reach the next bid
        game.Do(0, new PlaceBid(100));
        // Player 1 can't afford $110 and is skipped, so it is player 2's turn.
        Assert.Equal([2], game.Engine.GetPendingDecisions().Select(d => d.PlayerId));
        Assert.Contains(game.State.Log, e => e is AuctionPassed { PlayerId: 1 });
    }

    [Fact]
    public void TheFirstRailroad_IsSealed_ButTheNextOnesAreOpen()
    {
        var game = new TestGame(players: 3);
        game.RollOnto(0, "midtown-station");
        Assert.Equal(3, game.Engine.GetPendingDecisions().Count);
        game.EveryonePasses();
        game.EndTurn(0);
        game.Give(2, "old-town-station");
        game.RollOnto(1, "harbor-station");
        Assert.Equal([1], game.Engine.GetPendingDecisions().Select(d => d.PlayerId));
        Assert.True(game.DecisionFor(1)!.OpenAuction);
    }

    [Fact]
    public void Utilities_FollowTheSameRule()
    {
        var game = new TestGame(players: 3);
        game.RollOnto(0, "city-power-light");
        Assert.All(game.Engine.GetPendingDecisions(), d => Assert.False(d.OpenAuction));
        game.EveryonePasses();
        game.EndTurn(0);
        game.Give(2, "city-power-light");
        game.RollOnto(1, "city-water-works");
        Assert.True(game.DecisionFor(1)!.OpenAuction);
    }

    [Fact]
    public void RailroadsAndUtilities_AreSeparateSets()
    {
        var game = new TestGame(players: 3);
        game.Give(2, "old-town-station"); // a railroad is owned, but the utilities are not
        game.RollOnto(0, "city-power-light");
        Assert.All(game.Engine.GetPendingDecisions(), d => Assert.False(d.OpenAuction));
    }

    [Fact]
    public void AForcedAuction_IsAlwaysSealed()
    {
        var game = new TestGame(players: 3);
        game.Give(1, "market-street");
        game.GiveCard(0, "hostile-bid");
        game.Do(0, new PlayCard("hostile-bid", TargetSpaceId: "market-street"));
        Assert.All(game.Engine.GetPendingDecisions(), d => Assert.False(d.OpenAuction));
    }

    // ----- The Mogul -----

    [Fact]
    public void TheMogulsEdge_DoesNotApplyInAnOpenAuction()
    {
        var game = OpenAuction(characters: ["mogul", "hustler", "commuter"]);
        game.Do(0, new PlaceBid(100));
        game.Do(1, new PlaceBid(110)); // a plain higher bid: no $10 edge, no tie to win
        game.Do(2, new PassBid());
        game.Do(0, new PassBid());
        Assert.Equal(1, game.Property("baker-street").OwnerId);
    }

    [Fact]
    public void TheMogulsEdge_StillAppliesInASealedAuction()
    {
        var game = new TestGame(players: 2, characters: ["mogul", "hustler"]);
        game.RollOnto(0, "market-street");
        game.Do(0, new PlaceBid(100));
        game.Do(1, new PlaceBid(110));
        Assert.Equal(0, game.Property("market-street").OwnerId);
    }
}
