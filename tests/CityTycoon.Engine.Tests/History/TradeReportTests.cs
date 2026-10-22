using CityTycoon.Engine.Actions;
using CityTycoon.Engine.Content;
using CityTycoon.Engine.Events;

namespace CityTycoon.Engine.Tests.History;

/// <summary>The trade report card (decision #150): how each finished trade worked out, from the log alone.</summary>
public class TradeReportTests
{
    private static readonly GameContent Content = GameContent.Default;

    [Fact]
    public void RentAfterATrade_CountsTheWholeColorGroup_AndOnlyLaterRent()
    {
        // Player 0 buys Cobble Lane from player 1, finishing Brown (Mill Street was already theirs).
        var offer = new TradeOffer(0, 1, 120, 0, [], ["cobble-lane"]);
        var counter = offer with { GiveCash = 150 };
        List<GameEvent> log =
        [
            new RentPaid(2, 0, "mill-street", 4),        // before the trade: not counted
            new TurnStarted(0, 7),
            new TradeProposed(offer),
            new TradeCountered(offer, counter),
            new TradeCompleted(counter, 0),
            new RentPaid(2, 0, "mill-street", 8),        // the rest of the group counts
            new RentPaid(3, 0, "cobble-lane", 10),
            new RentPaid(3, 1, "cobble-lane", 99),       // paid to someone else: not counted
            new RentPaid(2, 0, "harbor-boulevard", 50),  // not part of the trade: not counted
        ];

        var trade = Assert.Single(TradeReport.Trades(log, Content));
        Assert.Equal(7, trade.Round);
        Assert.Equal(1, trade.Counters);
        Assert.Equal(4, trade.LogIndex);
        Assert.Equal(18, trade.FromRent);
        Assert.Equal(0, trade.ToRent);   // player 1 got only cash
    }

    [Fact]
    public void ANewOffer_StartsCountingCountersAgain()
    {
        var first = new TradeOffer(0, 1, 50, 0, [], ["cobble-lane"]);
        var second = new TradeOffer(2, 3, 0, 0, ["mill-street"], []);
        List<GameEvent> log =
        [
            new TradeProposed(first),
            new TradeCountered(first, first with { GiveCash = 60 }),
            new TradeProposed(second),
            new TradeCompleted(second, 0),
        ];
        Assert.Equal(0, Assert.Single(TradeReport.Trades(log, Content)).Counters);
    }
}
