using CityTycoon.Engine.Actions;
using CityTycoon.Engine.Events;
using CityTycoon.Engine.Tests.Helpers;

namespace CityTycoon.Engine.Tests.History;

/// <summary>The money ledger and the property history, worked out from the game log.</summary>
public class GameHistoryTests
{
    private static void Trade(TestGame game, TradeOffer offer)
    {
        game.Do(offer.FromId, new ProposeTrade(offer));
        game.Do(offer.ToId, new AcceptTrade());
    }

    [Fact]
    public void ATrade_ShowsInBothLedgers_AndInThePropertysHistory()
    {
        var game = new TestGame();
        game.Give(1, "market-street");
        Trade(game, new TradeOffer(0, 1, 250, 0, [], ["market-street"]));

        var view = game.Engine.GetView(0);
        var ledger = GameHistory.Ledger(view, game.Engine.Content);
        Assert.Contains(ledger, l => l.PlayerId == 0 && l.Amount == -250 && l.Kind == MoneyKind.Trade && l.OtherPlayerId == 1);
        Assert.Contains(ledger, l => l.PlayerId == 1 && l.Amount == 250 && l.Kind == MoneyKind.Trade);

        var history = GameHistory.ForSpace(view, game.Engine.Content, "market-street");
        Assert.Equal(0, history.Acquired!.OwnerId);
        Assert.Equal(HistoryKind.Traded, history.Acquired.How);
        Assert.Equal(250, history.Acquired.Price);
        Assert.Equal(1, history.Acquired.Round);
    }

    [Fact]
    public void AMortgageFee_IsChargedToWhoeverReceivedTheMortgagedProperty()
    {
        var game = new TestGame();
        game.Give(1, "market-street");
        game.Property("market-street").IsMortgaged = true;
        Trade(game, new TradeOffer(0, 1, 100, 0, [], ["market-street"]));

        var completed = game.State.Log.OfType<TradeCompleted>().Single();
        Assert.True(completed.FromFees > 0);
        Assert.Equal(0, completed.ToFees);
        var ledger = GameHistory.Ledger(game.Engine.GetView(0), game.Engine.Content);
        Assert.Equal(game.Player(0).Cash, ledger.Where(l => l.PlayerId == 0).Sum(l => l.Amount));
        Assert.Equal(game.Player(1).Cash, ledger.Where(l => l.PlayerId == 1).Sum(l => l.Amount));
    }

    [Fact]
    public void Building_IsListedInTheHistory_WithItsCost()
    {
        var game = new TestGame();
        game.Give(0, "mill-street", "cobble-lane");
        game.Do(0, new Build("mill-street"));

        var history = GameHistory.ForSpace(game.Engine.GetView(1), game.Engine.Content, "mill-street");
        var built = Assert.Single(history.Entries, h => h.Kind == HistoryKind.Built);
        Assert.Equal(0, built.PlayerId);
        Assert.True(built.Amount > 0);
        Assert.Contains("1 house", built.Text);
    }
}
