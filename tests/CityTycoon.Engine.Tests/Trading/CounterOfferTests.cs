using CityTycoon.Engine.Actions;
using CityTycoon.Engine.Content;
using CityTycoon.Engine.Events;
using CityTycoon.Engine.State;
using CityTycoon.Engine.Tests.Helpers;

namespace CityTycoon.Engine.Tests.Trading;

/// <summary>Section 15.9: answering a trade offer with a counter-offer.</summary>
public class CounterOfferTests
{
    /// <summary>Player 0 offers player 1 cash for Market Street.</summary>
    private static TestGame OfferMade(int cash = 100)
    {
        var game = new TestGame();
        game.Give(1, "market-street");
        game.Do(0, new ProposeTrade(new TradeOffer(0, 1, cash, 0, [], ["market-street"])));
        return game;
    }

    /// <summary>Player 1 asks for more: Market Street for this much cash (written from player 1's side).</summary>
    private static TradeOffer AskFor(int cash) => new(1, 0, 0, cash, ["market-street"], []);

    [Fact]
    public void ACounter_GoesBackToWhoeverMadeTheOffer()
    {
        var game = OfferMade();
        Assert.Equal(3, game.DecisionFor(1)!.CountersLeft);

        game.Do(1, new CounterTrade(AskFor(250)));

        var decision = game.DecisionFor(0)!;
        Assert.Equal(DecisionKind.RespondToTrade, decision.Kind);
        Assert.Equal(AskFor(250).GetCash, decision.Trade!.GetCash);
        Assert.Equal(1, decision.Trade.FromId);
        Assert.Equal(2, decision.CountersLeft);
        Assert.Null(game.DecisionFor(1));
        Assert.Contains(game.State.Log, e => e is TradeCountered);
    }

    [Fact]
    public void AcceptingACounter_MakesTheCounterTrade()
    {
        var game = OfferMade();
        game.Do(1, new CounterTrade(AskFor(250)));
        game.Do(0, new AcceptTrade());

        Assert.Equal(0, game.Property("market-street").OwnerId);
        Assert.Equal(1500 - 250, game.Player(0).Cash);
        Assert.Equal(1500 + 250, game.Player(1).Cash);
        // The proposer's turn carries on afterwards.
        Assert.Equal(DecisionKind.ChooseMoveMethod, game.DecisionFor(0)!.Kind);
    }

    [Fact]
    public void DecliningACounter_ChangesNothing()
    {
        var game = OfferMade();
        game.Do(1, new CounterTrade(AskFor(250)));
        game.Do(0, new DeclineTrade());

        Assert.Equal(1, game.Property("market-street").OwnerId);
        Assert.Equal(1500, game.Player(0).Cash);
        Assert.Equal(DecisionKind.ChooseMoveMethod, game.DecisionFor(0)!.Kind);
    }

    [Fact]
    public void AfterThreeCounters_YouCanOnlyAcceptOrDecline()
    {
        var game = OfferMade();
        game.Do(1, new CounterTrade(AskFor(300)));
        game.Do(0, new CounterTrade(new TradeOffer(0, 1, 200, 0, [], ["market-street"])));
        game.Do(1, new CounterTrade(AskFor(250)));

        Assert.Equal(0, game.DecisionFor(0)!.CountersLeft);
        var oneMore = new CounterTrade(new TradeOffer(0, 1, 220, 0, [], ["market-street"]));
        Assert.False(game.Engine.Submit(0, oneMore).Accepted);
        Assert.Contains("counter-offers", game.Engine.WhyCantCounter(0, oneMore.Offer));

        game.Do(0, new AcceptTrade());
        Assert.Equal(1500 - 250, game.Player(0).Cash);
    }

    [Fact]
    public void ACounter_MustChangeSomething()
    {
        var game = OfferMade(100);
        // The same trade, just written from player 1's side.
        var same = new TradeOffer(1, 0, 0, 100, ["market-street"], []);
        Assert.Contains("same trade", game.Engine.WhyCantCounter(1, same));
        Assert.False(game.Engine.Submit(1, new CounterTrade(same)).Accepted);
    }

    [Fact]
    public void ACounter_MustGoToTheOtherPlayer()
    {
        var game = new TestGame(players: 3);
        game.Give(1, "market-street");
        game.Do(0, new ProposeTrade(new TradeOffer(0, 1, 100, 0, [], ["market-street"])));
        var toSomeoneElse = new TradeOffer(1, 2, 0, 50, ["market-street"], []);
        Assert.NotNull(game.Engine.WhyCantCounter(1, toSomeoneElse));
    }

    [Fact]
    public void ACounter_FollowsTheNormalTradeRules()
    {
        var game = OfferMade();
        // Player 0 doesn't have $5,000.
        Assert.Contains("cash", game.Engine.WhyCantCounter(1, AskFor(5000)));
        // A counter can ask for a property the offer didn't mention.
        game.Give(0, "mill-street");
        var withProperty = new TradeOffer(1, 0, 0, 0, ["market-street"], ["mill-street"]);
        Assert.Null(game.Engine.WhyCantCounter(1, withProperty));
    }

    [Fact]
    public void OnlyThePlayerBeingAsked_CanCounter()
    {
        var game = OfferMade();
        Assert.NotNull(game.Engine.WhyCantCounter(0, new TradeOffer(0, 1, 120, 0, [], ["market-street"])));
    }

    [Fact]
    public void CountersDontUseUpTheTradeOfferLimit()
    {
        var game = OfferMade();
        game.Do(1, new CounterTrade(AskFor(300)));
        game.Do(0, new CounterTrade(new TradeOffer(0, 1, 200, 0, [], ["market-street"])));
        game.Do(1, new DeclineTrade());
        Assert.Equal(1, game.Player(0).TradesProposedThisTurn);
    }

    [Fact]
    public void AHaggle_SurvivesSaveAndLoad()
    {
        var game = OfferMade();
        game.Do(1, new CounterTrade(AskFor(250)));

        GameState loaded = GameStateSerializer.FromJson(GameStateSerializer.ToJson(game.State));
        var engine = new GameEngine(loaded, GameContent.Default, game.Dice);
        Assert.Equal(2, engine.GetPendingDecisions().Single().CountersLeft);
        Assert.True(engine.Submit(0, new AcceptTrade()).Accepted);
        Assert.Equal(0, engine.State.Properties["market-street"].OwnerId);
    }
}
