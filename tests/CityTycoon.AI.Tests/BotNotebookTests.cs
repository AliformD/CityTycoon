using System.Text.Json;
using CityTycoon.AI;
using CityTycoon.Engine;
using CityTycoon.Engine.Actions;
using CityTycoon.Engine.Content;
using CityTycoon.Engine.Events;
using CityTycoon.Engine.Hosting;

namespace CityTycoon.AI.Tests;

/// <summary>The bots' private notebook and the game log download (decision #150).</summary>
public class BotNotebookTests
{
    private static async Task<(GameEngine Engine, List<EasyBot> Bots)> PlayAsync(ulong seed)
    {
        var engine = GameEngine.NewGame(new GameSetup
        {
            Players = Enumerable.Range(0, 4).Select(i => new PlayerSetup($"Bot {i}")).ToList(),
            Seed = seed,
        });
        var bots = engine.State.Players.ToDictionary(p => p.Id, p => (EasyBot)new HardBot(seed * 7 + (ulong)p.Id));
        await new GameHost(engine, bots.ToDictionary(b => b.Key, b => (IPlayerController)b.Value)).RunAsync();
        return (engine, bots.Values.ToList());
    }

    [Fact]
    public async Task EveryTradeABotOffersOrAnswers_GetsOneNote()
    {
        int offered = 0;
        for (ulong seed = 1; seed <= 8; seed++)
        {
            var (engine, bots) = await PlayAsync(seed);
            var notes = bots.SelectMany(b => b.Notebook).ToList();
            var log = engine.State.Log;
            int proposed = log.OfType<TradeProposed>().Count();
            int countered = log.OfType<TradeCountered>().Count();
            Assert.Equal(proposed, notes.Count(n => n.Kind == "Offered"));
            // Every offer and every counter-offer is answered by a bot.
            Assert.Equal(proposed + countered, notes.Count(n => n.Kind == "Answered"));
            // A note sits right where the bot chose: an offer note just before its TradeProposed event.
            foreach (var note in notes.Where(n => n.Kind == "Offered"))
                Assert.Equal(note.Offer, Assert.IsType<TradeProposed>(log[note.LogIndex]).Offer);
            offered += proposed;
        }
        Assert.True(offered > 0, "these games should have some trades to check");
    }

    [Fact]
    public async Task AnAnswerNote_HoldsTheBotsSumsAndChoice()
    {
        var engine = GameEngine.NewGame(new GameSetup { Players = [new("Angel"), new("Bot")], Seed = 3, ShuffleTurnOrder = false });
        engine.State.Properties["mill-street"].OwnerId = 0;
        engine.State.Properties["cobble-lane"].OwnerId = 1;
        var offer = new TradeOffer(0, 1, 10, 0, [], ["cobble-lane"]);
        var bot = new NormalBot(2);
        int logBefore = engine.State.Log.Count;
        var answer = await bot.DecideAsync(engine.GetView(1),
            new PendingDecision(1, DecisionKind.RespondToTrade, "", Trade: offer, CountersLeft: 2), [new AcceptTrade(), new DeclineTrade()]);

        var note = Assert.Single(bot.Notebook);
        Assert.Equal("Answered", note.Kind);
        Assert.Equal(offer, note.Offer);
        Assert.Equal(answer switch { CounterTrade => "Counter", DeclineTrade => "Decline", _ => "Accept" }, note.Choice);
        Assert.True(note.Theirs > 0, "selling the last Brown piece finishes Brown for Angel");
        Assert.True(note.Limit < -10, "the bot wants much more than $10 for it");
        Assert.Equal(logBefore, engine.State.Log.Count);   // nothing secret was written to the game log
    }

    [Fact]
    public async Task Notes_SurviveASaveAndLoad()
    {
        var (_, bots) = await PlayAsync(2);
        var notes = bots.SelectMany(b => b.Notebook).ToList();
        string json = JsonSerializer.Serialize(notes);
        var loaded = JsonSerializer.Deserialize<List<BotNote>>(json)!;
        Assert.Equal(notes.Count, loaded.Count);
        Assert.Equal(JsonSerializer.Serialize(loaded), json);

        var fresh = new HardBot(1);
        fresh.RestoreNotebook(loaded);
        Assert.Equal(loaded.Count, fresh.Notebook.Count);
    }

    [Fact]
    public async Task TheGameLog_HasEveryEvent_TheBotNotes_AndATradeReport()
    {
        var (engine, bots) = await PlayAsync(3);
        string text = GameLogText.Write(engine.GetView(viewerId: -1), GameContent.Default,
            engine.State.Players.ToDictionary(p => p.Id, _ => "Hard bot"), bots.SelectMany(b => b.Notebook));
        Assert.StartsWith("City Tycoon game log", text);
        Assert.Contains("Bot 0: Hard bot", text);
        Assert.Contains("Round 1: ", text);
        Assert.Contains("Trade report card", text);
        if (bots.Any(b => b.Notebook.Count > 0))
            Assert.Contains("[Bot notes]", text);
    }
}
