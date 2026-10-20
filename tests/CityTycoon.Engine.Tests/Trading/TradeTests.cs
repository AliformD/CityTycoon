using CityTycoon.Engine.Actions;
using CityTycoon.Engine.Events;
using CityTycoon.Engine.Tests.Helpers;

namespace CityTycoon.Engine.Tests.Trading;

/// <summary>Section 15.9: trading.</summary>
public class TradeTests
{
    private static TradeOffer CashForProperty(int cash, string propertyId) =>
        new(FromId: 0, ToId: 1, GiveCash: cash, GetCash: 0, GiveProperties: [], GetProperties: [propertyId]);

    [Fact]
    public void ATrade_IsOffered_ThenTheOtherPlayerAnswers()
    {
        var game = new TestGame();
        game.Give(1, "market-street");
        game.Do(0, new ProposeTrade(CashForProperty(250, "market-street")));

        var decision = game.DecisionFor(1)!;
        Assert.Equal(DecisionKind.RespondToTrade, decision.Kind);
        Assert.Equal(CashForProperty(250, "market-street").GetProperties, decision.Trade!.GetProperties);
        Assert.Null(game.DecisionFor(0)); // the proposer waits for the answer
    }

    [Fact]
    public void Accepting_SwapsEverything_AndTheProposerCarriesOn()
    {
        var game = new TestGame();
        game.Give(1, "market-street");
        game.Give(0, "mill-street");
        var offer = new TradeOffer(0, 1, 100, 20, ["mill-street"], ["market-street"]);
        game.Do(0, new ProposeTrade(offer));
        game.Do(1, new AcceptTrade());

        Assert.Equal(0, game.Property("market-street").OwnerId);
        Assert.Equal(1, game.Property("mill-street").OwnerId);
        Assert.Equal(1500 - 100 + 20, game.Player(0).Cash);
        Assert.Equal(1500 + 100 - 20, game.Player(1).Cash);
        Assert.Contains(game.State.Log, e => e is TradeCompleted);
        Assert.Equal(DecisionKind.ChooseMoveMethod, game.DecisionFor(0)!.Kind);
    }

    [Fact]
    public void Declining_ChangesNothing()
    {
        var game = new TestGame();
        game.Give(1, "market-street");
        game.Do(0, new ProposeTrade(CashForProperty(250, "market-street")));
        game.Do(1, new DeclineTrade());

        Assert.Equal(1, game.Property("market-street").OwnerId);
        Assert.Equal(1500, game.Player(0).Cash);
        Assert.Contains(game.State.Log, e => e is TradeDeclined);
    }

    [Fact]
    public void AMortgagedProperty_StaysMortgaged_AndTheReceiverPaysTenPercent()
    {
        var game = new TestGame();
        game.Give(1, "market-street"); // mortgage value $90, fee $9
        game.Property("market-street").IsMortgaged = true;
        game.Do(0, new ProposeTrade(CashForProperty(100, "market-street")));
        game.Do(1, new AcceptTrade());

        Assert.True(game.Property("market-street").IsMortgaged);
        Assert.Equal(1500 - 100 - 9, game.Player(0).Cash);
        Assert.Contains(game.State.Log, e => e is TradeCompleted { MortgageFees: 9 });
    }

    [Fact]
    public void PendingProperties_CantBeTraded()
    {
        var game = new TestGame();
        game.Give(1, "market-street");
        game.Property("market-street").IsPending = true;
        Assert.Contains("pending", game.Engine.WhyIllegal(0, new ProposeTrade(CashForProperty(100, "market-street"))));
    }

    [Fact]
    public void PropertiesInAGroupWithBuildings_CantBeTraded()
    {
        var game = new TestGame();
        game.Give(1, "mill-street", "cobble-lane");
        game.Property("cobble-lane").Level = 1;
        Assert.Contains("buildings", game.Engine.WhyIllegal(0, new ProposeTrade(CashForProperty(100, "mill-street"))));
    }

    [Fact]
    public void YouCantOfferMoreCashThanYouHave()
    {
        var game = new TestGame();
        game.Give(1, "market-street");
        Assert.NotNull(game.Engine.WhyIllegal(0, new ProposeTrade(CashForProperty(5000, "market-street"))));
    }

    [Fact]
    public void YouCantOfferSomeoneElsesProperty()
    {
        var game = new TestGame();
        game.Give(1, "market-street");
        var offer = new TradeOffer(0, 1, 0, 0, ["market-street"], []);
        Assert.NotNull(game.Engine.WhyIllegal(0, new ProposeTrade(offer)));
    }

    [Fact]
    public void OnlyThePlayerWhoseTurnItIs_CanOfferATrade()
    {
        var game = new TestGame();
        game.Give(0, "market-street");
        var offer = new TradeOffer(1, 0, 100, 0, [], ["market-street"]);
        Assert.False(game.Engine.Submit(1, new ProposeTrade(offer)).Accepted);
        Assert.False(game.Engine.CanProposeTrade(1));
    }

    [Fact]
    public void AtMostThreeOffersPerTurn()
    {
        var game = new TestGame();
        game.Give(1, "market-street");
        for (int i = 0; i < 3; i++)
        {
            game.Do(0, new ProposeTrade(CashForProperty(10, "market-street")));
            game.Do(1, new DeclineTrade());
        }
        Assert.False(game.Engine.CanProposeTrade(0));
        Assert.False(game.Engine.Submit(0, new ProposeTrade(CashForProperty(10, "market-street"))).Accepted);
    }

    [Fact]
    public void ATradeCanCompleteAColorGroup_SoYouCanBuild()
    {
        var game = new TestGame();
        game.Give(0, "mill-street");
        game.Give(1, "cobble-lane");
        game.Do(0, new ProposeTrade(CashForProperty(100, "cobble-lane")));
        game.Do(1, new AcceptTrade());
        Assert.Contains(new Build("mill-street"), game.Engine.GetLegalActions(0));
    }
}
