using CityTycoon.Engine.Actions;
using CityTycoon.Engine.Content;
using CityTycoon.Engine.Events;
using CityTycoon.Engine.State;
using CityTycoon.Engine.Tests.Helpers;

namespace CityTycoon.Engine.Tests.Trading;

/// <summary>Section 15.9: Opportunity cards can be traded.</summary>
public class CardTradeTests
{
    [Fact]
    public void ACardCanBeOffered_AndMovesWhenAccepted()
    {
        var game = new TestGame();
        game.GiveCard(0, "audit");
        game.Give(1, "market-street");
        var offer = new TradeOffer(0, 1, 50, 0, [], ["market-street"]) { GiveCards = ["audit"] };
        game.Do(0, new ProposeTrade(offer));
        game.Do(1, new AcceptTrade());

        Assert.Empty(game.Player(0).Hand);
        Assert.Equal(["audit"], game.Player(1).Hand);
        Assert.Equal(0, game.Property("market-street").OwnerId);
    }

    [Fact]
    public void JustACard_IsATrade()
    {
        var game = new TestGame();
        game.GiveCard(0, "audit");
        var offer = new TradeOffer(0, 1, 0, 100, [], []) { GiveCards = ["audit"] };
        Assert.Null(game.Engine.WhyIllegal(0, new ProposeTrade(offer)));
    }

    [Fact]
    public void YouCantOfferACardYouDontHave()
    {
        var game = new TestGame();
        var offer = new TradeOffer(0, 1, 0, 100, [], []) { GiveCards = ["audit"] };
        Assert.Contains("cards", game.Engine.WhyIllegal(0, new ProposeTrade(offer)));
    }

    [Fact]
    public void AnOffer_CantAskForTheOtherPlayersCards_BecauseHandsAreSecret()
    {
        var game = new TestGame();
        game.GiveCard(1, "audit");
        var offer = new TradeOffer(0, 1, 100, 0, [], []) { GetCards = ["audit"] };
        Assert.Contains("secret", game.Engine.WhyIllegal(0, new ProposeTrade(offer)));
    }

    [Fact]
    public void ACounter_CanAddYourOwnCards_AndAskForCardsYouWereOffered()
    {
        var game = new TestGame();
        game.GiveCard(0, "audit");
        game.GiveCard(1, "get-out-of-jail-free");
        game.Do(0, new ProposeTrade(new TradeOffer(0, 1, 0, 100, [], []) { GiveCards = ["audit"] }));

        // Player 1 keeps the Audit card in, and adds their Get Out of Jail Free instead of cash.
        var counter = new TradeOffer(1, 0, 0, 0, [], []) { GiveCards = ["get-out-of-jail-free"], GetCards = ["audit"] };
        game.Do(1, new CounterTrade(counter));
        game.Do(0, new AcceptTrade());

        Assert.Equal(["get-out-of-jail-free"], game.Player(0).Hand);
        Assert.Equal(["audit"], game.Player(1).Hand);
        Assert.Equal(1500, game.Player(0).Cash);
    }

    [Fact]
    public void ACounter_CantAskForCardsThatWerentOffered()
    {
        var game = new TestGame();
        game.GiveCard(0, "audit", "insurance");
        game.Do(0, new ProposeTrade(new TradeOffer(0, 1, 0, 100, [], []) { GiveCards = ["audit"] }));
        var greedy = new TradeOffer(1, 0, 0, 0, [], []) { GetCards = ["audit", "insurance"] };
        Assert.Contains("offered to you", game.Engine.WhyCantCounter(1, greedy));
    }

    [Fact]
    public void TheLog_NamesTheCards()
    {
        var game = new TestGame();
        game.GiveCard(0, "audit");
        game.Do(0, new ProposeTrade(new TradeOffer(0, 1, 0, 100, [], []) { GiveCards = ["audit"] }));
        var line = EventDescriber.Describe(game.State.Log.OfType<TradeProposed>().Last(), game.Engine.GetView(1), GameContent.Default);
        Assert.Contains("Audit card for $100", line);
    }

    [Fact]
    public void ACardTrade_SurvivesSaveAndLoad()
    {
        var game = new TestGame();
        game.GiveCard(0, "audit");
        game.Do(0, new ProposeTrade(new TradeOffer(0, 1, 0, 100, [], []) { GiveCards = ["audit"] }));

        GameState loaded = GameStateSerializer.FromJson(GameStateSerializer.ToJson(game.State));
        var engine = new GameEngine(loaded, GameContent.Default, game.Dice);
        Assert.Equal(["audit"], engine.GetPendingDecisions().Single().Trade!.GiveCards);
        Assert.True(engine.Submit(1, new AcceptTrade()).Accepted);
        Assert.Equal(["audit"], loaded.Player(1).Hand);
    }
}
