using CityTycoon.Engine;
using CityTycoon.Engine.Actions;
using CityTycoon.Engine.Events;
using CityTycoon.Engine.Hosting;

namespace CityTycoon.AI.Tests;

/// <summary>Decision #160: bots watch trades, speak up about lopsided ones, and keep a temperament toward each player.</summary>
public class TableTalkBotTests
{
    private static GameEngine NewGame() => GameEngine.NewGame(new GameSetup
    {
        Players = [new("Ana"), new("Bo"), new("Cy")],
        Seed = 3,
        ShuffleTurnOrder = false,
    });

    /// <summary>Player 0 proposes the offer; returns what player 2 (the onlooker bot) says about it.</summary>
    private static IReadOnlyList<Remark> OnlookerSays(GameEngine engine, TradeOffer offer, EasyBot? bot = null)
    {
        int before = engine.State.Log.Count;
        Assert.True(engine.Submit(0, new ProposeTrade(offer)).Accepted);
        var newEvents = engine.State.Log.Skip(before).ToList();
        return (bot ?? new HardBot(1)).Talk(newEvents, before, () => engine.GetView(2));
    }

    [Fact]
    public void AnOnlooker_WarnsAPlayerAboutToHandOverAColorGroup()
    {
        var engine = NewGame();
        engine.State.Properties["mill-street"].OwnerId = 0;
        engine.State.Properties["cobble-lane"].OwnerId = 1;

        var said = OnlookerSays(engine, new TradeOffer(0, 1, 70, 0, [], ["cobble-lane"]));

        Remark warning = Assert.Single(said);
        Assert.Equal(TalkTone.Warning, warning.Tone);
        Assert.Equal(1, warning.AboutId);
        Assert.Contains("Brown", warning.Text);
    }

    [Fact]
    public void AnOnlooker_StaysQuiet_AboutAFairTrade()
    {
        var engine = NewGame();
        engine.State.Properties["market-street"].OwnerId = 1;   // $180, and no group gets finished
        Assert.Empty(OnlookerSays(engine, new TradeOffer(0, 1, 190, 0, [], ["market-street"])));
    }

    [Fact]
    public void BigRentPayments_TurnABotHostile_AndItSaysSo()
    {
        var engine = NewGame();
        var bot = new HardBot(1);
        Assert.Equal(Mood.Neutral, bot.MoodToward(engine.GetView(2), 0));

        int before = engine.State.Log.Count;
        engine.State.Log.Add(new RentPaid(2, 0, "market-street", 4000));
        var said = bot.Talk(engine.State.Log.Skip(before).ToList(), before, () => engine.GetView(2));

        Assert.Equal(Mood.Hostile, bot.MoodToward(engine.GetView(2), 0));
        Assert.Equal(TalkTone.Threat, Assert.Single(said).Tone);
    }

    [Fact]
    public void AFinishedTrade_MakesThemFriends()
    {
        var engine = NewGame();
        var bot = new HardBot(1);
        int before = engine.State.Log.Count;
        engine.State.Log.Add(new TradeCompleted(new TradeOffer(0, 2, 100, 0, [], []), 0));
        var said = bot.Talk(engine.State.Log.Skip(before).ToList(), before, () => engine.GetView(2));

        Assert.Equal(Mood.Friendly, bot.MoodToward(engine.GetView(2), 0));
        Assert.Equal(TalkTone.Friendly, Assert.Single(said).Tone);
    }

    [Fact]
    public async Task ABotAsksMoreFromSomeoneItDislikes()
    {
        // Ana sells Market Street ($180) to the bot. Find the prices a neutral bot takes and a hostile one turns down.
        async Task<bool> Takes(bool hostile, int price)
        {
            var engine = NewGame();
            engine.State.Properties["market-street"].OwnerId = 0;
            if (hostile)
                engine.State.Log.Add(new RentPaid(1, 0, "market-street", 4000));
            var offer = new TradeOffer(0, 1, 0, price, ["market-street"], []);
            // With counters left it holds out for its full margin (on the last word it takes any deal that helps it).
            var decision = new PendingDecision(1, DecisionKind.RespondToTrade, "", Trade: offer, CountersLeft: 2);
            return await new HardBot(1).DecideAsync(engine.GetView(1), decision, [new AcceptTrade(), new DeclineTrade()]) is AcceptTrade;
        }

        int neutralTop = 0, hostileTop = 0;
        for (int price = 100; price <= 300; price += 10)
        {
            if (await Takes(false, price)) neutralTop = price;
            if (await Takes(true, price)) hostileTop = price;
        }
        Assert.True(neutralTop > hostileTop, $"neutral pays up to {neutralTop}, hostile up to {hostileTop}");
    }

    [Fact]
    public async Task HardBots_TalkDuringAFullGame()
    {
        int remarks = 0;
        for (ulong seed = 1; seed <= 5; seed++)
        {
            var engine = GameEngine.NewGame(new GameSetup
            {
                Players = Enumerable.Range(0, 4).Select(i => new PlayerSetup($"Bot {i}")).ToList(),
                Seed = seed,
            });
            await new GameHost(engine, engine.State.Players.ToDictionary(p => p.Id, p => (IPlayerController)new HardBot(seed * 7 + (ulong)p.Id))).RunAsync();
            remarks += engine.State.Log.OfType<TableTalk>().Count();
            Assert.All(engine.State.Log.OfType<TableTalk>(), t => Assert.NotEqual(t.SpeakerId, t.AboutId));
        }
        Assert.True(remarks > 0);
    }
}
