using CityTycoon.AI;
using CityTycoon.Engine;
using CityTycoon.Engine.Actions;
using CityTycoon.Engine.Content;
using CityTycoon.Engine.Events;
using CityTycoon.Engine.Hosting;

namespace CityTycoon.AI.Tests;

/// <summary>Bots explain their trade answers, and don't repeat the same offer every turn.</summary>
public class TradeFeedbackBotTests
{
    /// <summary>The bot owns two of Orange plus Elm Avenue; the person owns Market Street, the last piece of Orange.</summary>
    private static GameEngine BotWantsMarket()
    {
        var engine = GameEngine.NewGame(new GameSetup { Players = [new("Human"), new("Bot")], Seed = 3, ShuffleTurnOrder = false });
        engine.State.Properties["baker-street"].OwnerId = 1;
        engine.State.Properties["fountain-square"].OwnerId = 1;
        engine.State.Properties["elm-avenue"].OwnerId = 1;
        engine.State.Properties["market-street"].OwnerId = 0;
        // These tests were written with $1,500 each (the starting cash before decision #143).
        engine.State.Player(0).Cash = 1500;
        engine.State.Player(1).Cash = 1500;
        return engine;
    }

    private static async Task<GameAction> Answer(IPlayerController bot, GameEngine engine, TradeOffer offer, int countersLeft = 2) =>
        await bot.DecideAsync(engine.GetView(1),
            new PendingDecision(1, DecisionKind.RespondToTrade, "", Trade: offer, CountersLeft: countersLeft),
            [new AcceptTrade(), new DeclineTrade()]);

    [Fact]
    public async Task ADecline_SaysWhy()
    {
        var engine = BotWantsMarket();
        // This time the bot owns Market Street and the person wants it to finish Orange.
        engine.State.Properties["market-street"].OwnerId = 1;
        engine.State.Properties["baker-street"].OwnerId = 0;
        engine.State.Properties["fountain-square"].OwnerId = 0;
        var offer = new TradeOffer(0, 1, 300, 0, [], ["market-street"]);

        var decline = Assert.IsType<DeclineTrade>(await Answer(new EasyBot(1), engine, offer, countersLeft: 0));
        Assert.Contains("worse off", decline.Why);
        Assert.Contains("whole Orange set", decline.Why);
    }

    [Fact]
    public async Task OutOfCounters_TheDeclineSaysSo()
    {
        var engine = BotWantsMarket();
        var greedy = new TradeOffer(0, 1, 0, 900, ["market-street"], []);
        var decline = Assert.IsType<DeclineTrade>(await Answer(new HardBot(1), engine, greedy, countersLeft: 0));
        Assert.Contains("worse off", decline.Why);
        Assert.Contains("out of counter-offers", decline.Why);
    }

    [Fact]
    public async Task ACounter_SaysWhy_AndTheLogShowsIt()
    {
        var engine = BotWantsMarket();
        var greedy = new TradeOffer(0, 1, 0, 900, ["market-street"], []);
        Assert.True(engine.Submit(0, new ProposeTrade(greedy)).Accepted);

        var counter = Assert.IsType<CounterTrade>(await Answer(new HardBot(1), engine, greedy));
        Assert.Contains("Here's what I'd take", counter.Why);
        Assert.True(engine.Submit(1, counter).Accepted);

        var logged = engine.State.Log.OfType<TradeCountered>().Single();
        Assert.Equal(counter.Why, logged.Why);
        Assert.Contains("Here's what I'd take", EventDescriber.Describe(logged, engine.GetView(0), GameContent.Default));
        Assert.Contains("Here's what I'd take", engine.GetPendingDecisions().Single().Prompt);
    }

    [Fact]
    public async Task WhenItCantPayForEverythingAsked_ItCountersWithoutTheExtraProperty()
    {
        var engine = BotWantsMarket();
        engine.State.Properties["skyline-drive"].OwnerId = 1;
        engine.State.Properties["platinum-row"].OwnerId = 1;
        engine.State.Player(0).Cash = 0;
        // Market Street for the whole Dark Blue set: far too much, and the person has no cash to make up the gap.
        var offer = new TradeOffer(0, 1, 0, 0, ["market-street"], ["skyline-drive", "platinum-row"]);

        var counter = Assert.IsType<CounterTrade>(await Answer(new EasyBot(1), engine, offer)).Offer;

        Assert.Empty(counter.GiveProperties);
        Assert.Equal(["market-street"], counter.GetProperties);
        Assert.True(counter.GiveCash > 0); // it still pays something for Market Street
    }

    [Fact]
    public async Task ADeclinedOffer_IsntRepeatedForAFewRounds_ThenComesBackHigher()
    {
        var engine = BotWantsMarket();
        var bot = new EasyBot(1);
        var decision = new PendingDecision(1, DecisionKind.PostMoveActions, "", Management: ManagementOptions.OwnTurn);

        async Task<TradeOffer?> Offer()
        {
            var view = engine.GetView(1);
            var choice = await bot.DecideAsync(view, decision, [new EndTurn()]);
            return (choice as ProposeTrade)?.Offer;
        }

        var first = await Offer();
        Assert.NotNull(first);
        engine.State.Log.Add(new TradeProposed(first));
        engine.State.Log.Add(new TradeDeclined(first, "declined"));

        for (int round = 2; round <= 3; round++)
        {
            engine.State.Round = round;
            engine.State.Log.Add(new TurnStarted(1, round));
            Assert.Null(await Offer());
        }

        engine.State.Round = 4;
        engine.State.Log.Add(new TurnStarted(1, 4));
        var again = await Offer();
        Assert.NotNull(again);
        Assert.True(again.GiveCash > first.GiveCash);
    }
}
