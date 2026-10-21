using CityTycoon.AI;
using CityTycoon.Engine;
using CityTycoon.Engine.Actions;
using CityTycoon.Engine.Events;
using CityTycoon.Engine.Hosting;

namespace CityTycoon.AI.Tests;

/// <summary>
/// Bots value a color group by what it earns for the rest of the game (rent and its perk),
/// and haggle toward the other player instead of repeating themselves (decision #145).
/// </summary>
public class TradeNegotiationBotTests
{
    /// <summary>
    /// Angel's game: four players, the person (seat 0) owns Mill Street, the bot (seat 1) owns
    /// Cobble Lane, the other half of Brown, plus a couple of other streets.
    /// </summary>
    private static GameEngine BrownSplit(int round, int botCash)
    {
        var engine = GameEngine.NewGame(new GameSetup { Players = [new("Angel"), new("Cal"), new("Ben"), new("Dee")], Seed = 3, ShuffleTurnOrder = false });
        engine.State.Properties["mill-street"].OwnerId = 0;
        engine.State.Properties["birch-avenue"].OwnerId = 0;
        engine.State.Properties["cobble-lane"].OwnerId = 1;
        engine.State.Properties["harbor-boulevard"].OwnerId = 1;
        engine.State.Properties["studio-street"].OwnerId = 1;
        engine.State.Player(0).Cash = 510;
        engine.State.Player(1).Cash = botCash;
        engine.State.Round = round;
        return engine;
    }

    private static async Task<TradeOffer?> Opening(EasyBot bot, GameEngine engine)
    {
        var decision = new PendingDecision(1, DecisionKind.PostMoveActions, "", Management: ManagementOptions.OwnTurn);
        var choice = await bot.DecideAsync(engine.GetView(1), decision, [new EndTurn()]);
        return (choice as ProposeTrade)?.Offer;
    }

    private static async Task<GameAction> Answer(EasyBot bot, GameEngine engine, TradeOffer offer, int countersLeft) =>
        await bot.DecideAsync(engine.GetView(1),
            new PendingDecision(1, DecisionKind.RespondToTrade, "", Trade: offer, CountersLeft: countersLeft),
            [new AcceptTrade(), new DeclineTrade()]);

    /// <summary>The person asks the bot for $350 for Mill Street.</summary>
    private static readonly TradeOffer AskFor350 = new(0, 1, 0, 350, ["mill-street"], []);

    [Fact]
    public async Task EarlyInTheGame_TheBotOffersFarMoreThanAssessedValue_ForTheLastPieceOfBrown()
    {
        // Brown pays $50 extra at GO every lap, and there are 39 rounds left to collect it.
        var offer = await Opening(new NormalBot(2), BrownSplit(round: 2, botCash: 900));
        Assert.NotNull(offer);
        Assert.Equal(["mill-street"], offer.GetProperties);
        Assert.True(offer.GiveCash >= 200, $"opened at ${offer.GiveCash}");
    }

    [Fact]
    public async Task EarlyInTheGame_ABotWithCashToSpare_TakesAFairAsk()
    {
        var engine = BrownSplit(round: 2, botCash: 900);
        Assert.Equal(new AcceptTrade(), await Answer(new NormalBot(2), engine, AskFor350, countersLeft: 2));
    }

    [Fact]
    public async Task LateInTheGame_BrownIsWorthLess()
    {
        async Task<int> Most(int round)
        {
            var engine = BrownSplit(round, botCash: 900);
            // With one counter left the bot sends its limit: the most it would pay.
            var answer = await Answer(new NormalBot(2), engine, new TradeOffer(0, 1, 0, 900, ["mill-street"], []), countersLeft: 1);
            return Assert.IsType<CounterTrade>(answer).Offer.GiveCash;
        }
        int early = await Most(2), middle = await Most(20), late = await Most(36);
        Assert.True(late < middle && middle < early, $"round 2: ${early}, round 20: ${middle}, round 36: ${late}");
    }

    [Fact]
    public async Task ACounter_MeetsThePersonPartway_AndNeverGoesBackDown()
    {
        // Cal's situation: not much cash, so the bot can't pay $350, but it moves toward it.
        var engine = BrownSplit(round: 2, botCash: 490);
        var bot = new NormalBot(2);
        var opening = await Opening(bot, engine);
        Assert.NotNull(opening);
        engine.State.Log.Add(new TradeProposed(opening));

        var last = opening;
        for (int left = 3; left >= 1; left -= 2)
        {
            engine.State.Log.Add(new TradeCountered(last, AskFor350));
            var counter = Assert.IsType<CounterTrade>(await Answer(bot, engine, AskFor350, left));
            Assert.True(counter.Offer.GiveCash > opening.GiveCash, $"{counter.Offer.GiveCash} after opening at {opening.GiveCash}");
            Assert.True(counter.Offer.GiveCash >= last.GiveCash);
            Assert.True(counter.Offer.GiveCash < 350);
            Assert.Contains("$350 is more than I'd pay", counter.Why);
            if (left == 1)
                Assert.Contains("final offer", counter.Why);
            engine.State.Log.Add(new TradeCountered(AskFor350, counter.Offer));
            last = counter.Offer;
        }
    }

    [Fact]
    public async Task AggressiveBots_OfferMoreForASet_ThanReservedOnes()
    {
        var engine = BrownSplit(round: 2, botCash: 900);
        var bots = Enumerable.Range(1, 40).Select(s => new NormalBot((ulong)s)).ToList();
        async Task<double> AverageOpening(BotPersonality p)
        {
            var offers = new List<int>();
            foreach (var bot in bots.Where(b => b.Personality == p))
                offers.Add((await Opening(bot, engine))!.GiveCash);
            return offers.Average();
        }
        Assert.True(await AverageOpening(BotPersonality.Aggressive) > await AverageOpening(BotPersonality.Reserved));
    }

    [Fact]
    public async Task ABotWontSellTheLastPieceOfBrown_ForAssessedValue()
    {
        // Now the bot owns Mill Street and the person owns Cobble Lane.
        var engine = BrownSplit(round: 2, botCash: 900);
        engine.State.Properties["mill-street"].OwnerId = 1;
        engine.State.Properties["cobble-lane"].OwnerId = 0;
        engine.State.Player(0).Cash = 1200;
        var cheap = new TradeOffer(0, 1, 60, 0, [], ["mill-street"]);

        var counter = Assert.IsType<CounterTrade>(await Answer(new NormalBot(2), engine, cheap, countersLeft: 2));
        Assert.True(counter.Offer.GetCash > 150, $"asked for ${counter.Offer.GetCash}");
        Assert.Contains("whole Brown set", counter.Why);
    }
}
