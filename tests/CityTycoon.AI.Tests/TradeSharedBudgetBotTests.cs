using CityTycoon.AI;
using CityTycoon.Engine;
using CityTycoon.Engine.Actions;
using CityTycoon.Engine.Hosting;
using CityTycoon.Engine.Views;

namespace CityTycoon.AI.Tests;

/// <summary>
/// A trade that finishes several color groups at once: the groups share one pot of cash
/// for houses, and the bot names every group in its answer (decision #149).
/// </summary>
public class TradeSharedBudgetBotTests
{
    /// <summary>Lets a test call the bot's group sums directly.</summary>
    private sealed class OpenBot(ulong seed) : NormalBot(seed)
    {
        public double Earnings(PlayerView view, int playerId, IReadOnlyList<string> groups, int cash) =>
            GroupsEarnings(view, playerId, groups, cash);
    }

    /// <summary>
    /// Angel's example: round 10 of 40, both have $800. The person (seat 0) gives the pieces that
    /// finish Dark Blue, Green and Brown for the bot, and asks for the pieces that finish
    /// Yellow, Orange and Red for themselves.
    /// </summary>
    private static GameEngine ThreeForThree()
    {
        var engine = GameEngine.NewGame(new GameSetup { Players = [new("Angel"), new("Bot"), new("Ben"), new("Dee")], Seed = 3, ShuffleTurnOrder = false });
        foreach (string id in new[] { "harbor-boulevard", "wharf-street", "market-street", "baker-street", "pier-road", "anchor-street", "skyline-drive", "crown-street", "mill-street" })
            engine.State.Properties[id].OwnerId = 0;
        foreach (string id in new[] { "platinum-row", "summit-avenue", "grand-plaza", "cobble-lane", "marina-drive", "fountain-square", "lighthouse-lane" })
            engine.State.Properties[id].OwnerId = 1;
        engine.State.Player(0).Cash = 800;
        engine.State.Player(1).Cash = 800;
        engine.State.Round = 10;
        return engine;
    }

    private static readonly TradeOffer ThreeForThreeOffer =
        new(0, 1, 0, 0, ["skyline-drive", "crown-street", "mill-street"], ["marina-drive", "fountain-square", "lighthouse-lane"]);

    [Fact]
    public void GroupsFinishedTogether_ShareOneHouseBudget()
    {
        var view = ThreeForThree().GetView(1);
        var bot = new OpenBot(2);
        string[] groups = ["yellow", "orange", "red"];
        double together = bot.Earnings(view, 0, groups, 800);
        double separately = groups.Sum(g => bot.Earnings(view, 0, [g], 800));
        // Counted one at a time, each group gets all $800; together they must split it.
        Assert.True(together < separately, $"together ${together:0}, one at a time ${separately:0}");
        // But owning more groups is never worth less than owning the best one of them.
        Assert.True(together >= groups.Max(g => bot.Earnings(view, 0, [g], 800)));
    }

    [Fact]
    public void ASmallSumOfCash_NoLongerSwingsTheVerdictByThousands()
    {
        // Before decision #149, $150 moved the bot's sums by about $2,000 on this trade (-$1,512 to +$508).
        var view = ThreeForThree().GetView(1);
        var bot = new OpenBot(2);
        double Value(int angelCash) => bot.Earnings(view, 0, ["yellow", "orange", "red"], angelCash);
        Assert.True(Value(800) - Value(650) < 600, $"$800: {Value(800):0}, $650: {Value(650):0}");
    }

    [Fact]
    public async Task TheBotNamesEveryGroupTheTradeWouldFinishForYou()
    {
        var answer = await new NormalBot(2).DecideAsync(ThreeForThree().GetView(1),
            new PendingDecision(1, DecisionKind.RespondToTrade, "", Trade: ThreeForThreeOffer, CountersLeft: 2),
            [new AcceptTrade(), new DeclineTrade()]);
        string why = answer switch
        {
            CounterTrade c => c.Why ?? "",
            DeclineTrade d => d.Why ?? "",
            _ => "",
        };
        Assert.Contains("Yellow, Orange and Red sets", why);
    }
}
