using CityTycoon.Engine;
using CityTycoon.Engine.Actions;
using CityTycoon.Engine.Views;
using Xunit.Abstractions;

namespace CityTycoon.AI.Tests;

/// <summary>Decision #156: bids count the set's perk and the bot's character; trades count the time left.</summary>
public class BidAndTimeBotTests(ITestOutputHelper output)
{
    private static GameEngine NewGame(int players = 4) => GameEngine.NewGame(new GameSetup
    {
        Players = Enumerable.Range(0, players).Select(i => new PlayerSetup($"P{i}")).ToList(),
        Seed = 3,
        ShuffleTurnOrder = false,
    });

    [Theory]
    [InlineData(10, true)]
    [InlineData(38, false)]
    public async Task ALateSet_IsNotWorthOverpayingARival(int round, bool accepts)
    {
        // The report's Game 1546: $450 for Crown Street (assessed $320), the last piece of Green.
        var engine = NewGame();
        engine.State.Round = round;
        engine.State.Properties["summit-avenue"].OwnerId = 1;
        engine.State.Properties["grand-plaza"].OwnerId = 1;
        engine.State.Properties["crown-street"].OwnerId = 0;
        engine.State.Player(1).Cash = 919;
        engine.State.Player(0).Cash = 76;
        Assert.True(engine.Submit(0, new ProposeTrade(new TradeOffer(0, 1, 0, 450, ["crown-street"], []))).Accepted);

        var choice = await new HardBot(1).DecideAsync(engine.GetView(1), engine.GetPendingDecisions().Single(), engine.GetLegalActions(1));

        output.WriteLine(choice.ToString());
        Assert.Equal(accepts, choice is AcceptTrade);
    }

    /// <summary>A Normal bot that ignores the end of the game, to compare with.</summary>
    private sealed class TimelessBot() : NormalBot(1)
    {
        protected override double EndgameWeight(PlayerView view) => 0;
    }

    /// <summary>The bot's best and final offer for the last piece of Orange when asked $2,800 for it.</summary>
    private static async Task<int> FinalOffer(EasyBot bot, int round)
    {
        var engine = NewGame();
        engine.State.Round = round;
        engine.State.Properties["market-street"].OwnerId = 1;
        engine.State.Properties["baker-street"].OwnerId = 1;
        engine.State.Properties["fountain-square"].OwnerId = 0;
        engine.State.Player(1).Cash = 3000;
        Assert.True(engine.Submit(0, new ProposeTrade(new TradeOffer(0, 1, 0, 2800, ["fountain-square"], []))).Accepted);
        var decision = engine.GetPendingDecisions().Single() with { CountersLeft = 1 };
        var choice = await bot.DecideAsync(engine.GetView(1), decision, engine.GetLegalActions(1));
        return Assert.IsType<CounterTrade>(choice).Offer.GiveCash;
    }

    [Fact]
    public async Task NearTheEnd_ABotPaysARivalLess()
    {
        int early = await FinalOffer(new NormalBot(1), 10), earlyTimeless = await FinalOffer(new TimelessBot(), 10);
        int late = await FinalOffer(new NormalBot(1), 36), lateTimeless = await FinalOffer(new TimelessBot(), 36);
        output.WriteLine($"early {early} / {earlyTimeless}, late {late} / {lateTimeless}");

        Assert.Equal(earlyTimeless, early);   // no change early on
        Assert.True(late < lateTimeless);     // but less near the end
    }

    /// <summary>Lets the tests read the most a Normal bot would bid.</summary>
    private class OpenBot(double perkShare = 0.5) : NormalBot(1)
    {
        protected override double BidPerkShare => perkShare;
        public double Worth(GameEngine engine, string propertyId) =>
            PropertyWorth(engine.GetView(1), new PendingDecision(1, DecisionKind.SubmitBid, "", propertyId));
    }

    [Fact]
    public void ABid_ForTheLastPiece_CountsTheGroupsPerk()
    {
        var engine = NewGame();
        engine.State.Properties["mill-street"].OwnerId = 1; // Brown: $50 more at GO
        double with = new OpenBot().Worth(engine, "cobble-lane");
        double without = new OpenBot(perkShare: 0).Worth(engine, "cobble-lane");
        output.WriteLine($"{with:F0} vs {without:F0}");
        Assert.True(with > without + 100);
    }

    [Theory]
    [InlineData("contractor", "crown-street", true)]   // dear houses, 25% off each
    [InlineData("contractor", "mill-street", true)]    // cheap houses: a smaller bump
    [InlineData("commuter", "elm-avenue", true)]       // Light Blue's extra token room
    [InlineData("commuter", "market-street", false)]
    [InlineData("lawyer", "crown-street", false)]
    public void ABid_CountsWhatSuitsTheCharacter(string character, string propertyId, bool more)
    {
        var engine = NewGame();
        double plain = new OpenBot().Worth(engine, propertyId);
        engine.State.Player(1).CharacterId = character;
        double fit = new OpenBot().Worth(engine, propertyId);
        Assert.Equal(more, fit > plain);
    }

    [Fact]
    public void NoCharacterBump_WhenSomeoneElseAlreadyHasAPieceOfTheGroup()
    {
        var engine = NewGame();
        engine.State.Properties["summit-avenue"].OwnerId = 2;
        double plain = new OpenBot().Worth(engine, "crown-street");
        engine.State.Player(1).CharacterId = "contractor";
        Assert.Equal(plain, new OpenBot().Worth(engine, "crown-street"));
    }
}
