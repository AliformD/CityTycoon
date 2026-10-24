using CityTycoon.Engine;
using CityTycoon.Engine.Actions;
using CityTycoon.Engine.Events;
using CityTycoon.Engine.Hosting;

namespace CityTycoon.AI.Tests;

/// <summary>Bots must be able to answer every new question the characters and perks ask.</summary>
public class CharacterBotTests
{
    [Theory]
    [InlineData(CharacterMode.Pick)]
    [InlineData(CharacterMode.Random)]
    public async Task EasyBots_FinishGamesWithCharacters(CharacterMode mode)
    {
        for (ulong seed = 1; seed <= 25; seed++)
        {
            var engine = GameEngine.NewGame(new GameSetup
            {
                Players = Enumerable.Range(0, 4).Select(i => new PlayerSetup($"Bot {i}")).ToList(),
                Seed = seed,
                Characters = mode,
            });
            var bots = engine.State.Players.ToDictionary(p => p.Id, p => (IPlayerController)new EasyBot(seed * 7 + (ulong)p.Id));
            await new GameHost(engine, bots).RunAsync();
            Assert.True(engine.State.IsOver);
            Assert.All(engine.State.Players, p => Assert.NotNull(p.CharacterId));
        }
    }

    [Fact]
    public async Task RandomBots_AnswerEveryNewQuestion_WithoutErrors()
    {
        // The random bot takes any legal action, so it also tries every new action type.
        for (ulong seed = 1; seed <= 25; seed++)
        {
            var engine = GameEngine.NewGame(new GameSetup
            {
                Players = Enumerable.Range(0, 6).Select(i => new PlayerSetup($"Bot {i}")).ToList(),
                Seed = seed,
                Characters = CharacterMode.Pick,
                MaxRounds = 30,
            });
            var bots = engine.State.Players.ToDictionary(p => p.Id, p => (IPlayerController)new RandomBot(seed * 31 + (ulong)p.Id));
            await new GameHost(engine, bots).RunAsync();
            Assert.True(engine.State.IsOver);
        }
    }

    [Fact]
    public async Task EasyBot_PicksAnAvailableCharacter_KeepsACard_AndPeeksAtATile()
    {
        var engine = GameEngine.NewGame(new GameSetup
        {
            Players = [new("A"), new("B")],
            Seed = 3,
            Characters = CharacterMode.Pick,
            ShuffleTurnOrder = false,
        });
        var bot = new EasyBot(1);
        var pick = engine.GetPendingDecisions()[0];
        var action = await bot.DecideAsync(engine.GetView(pick.PlayerId), pick, engine.GetLegalActions(pick.PlayerId));
        Assert.IsType<PickCharacter>(action);

        var keep = await bot.DecideAsync(engine.GetView(0),
            new PendingDecision(0, DecisionKind.KeepCard, "", Cards: ["audit", "insurance"]),
            [new KeepCard("audit"), new KeepCard("insurance")]);
        Assert.IsType<KeepCard>(keep);

        var peek = await bot.DecideAsync(engine.GetView(0),
            new PendingDecision(0, DecisionKind.PeekTile, ""), [new PeekAtTile("old-town-government"), new PeekAtTile("midtown-government")]);
        Assert.IsType<PeekAtTile>(peek);
    }

    [Fact]
    public async Task EasyBot_InAnOpenAuction_RaisesOneStepAtATime()
    {
        var engine = GameEngine.NewGame(new GameSetup { Players = [new("A"), new("B")], Seed = 3 });
        var decision = new PendingDecision(0, DecisionKind.SubmitBid, "", "market-street", MinBid: 130, MaxBid: 1500, BidIncrement: 10,
            OpenAuction: true, HighBidderId: 1, HighBid: 120);
        for (int i = 0; i < 20; i++)
        {
            var action = await new EasyBot((ulong)i).DecideAsync(engine.GetView(0), decision, [new PassBid(), new PlaceBid(130)]);
            Assert.Equal(new PlaceBid(130), action);
        }
    }

    [Fact]
    public async Task EasyBot_InAnOpenAuction_DropsOutAtItsLimit()
    {
        var engine = GameEngine.NewGame(new GameSetup { Players = [new("A"), new("B")], Seed = 3 });
        var decision = new PendingDecision(0, DecisionKind.SubmitBid, "", "market-street", MinBid: 900, MaxBid: 1500, BidIncrement: 10,
            OpenAuction: true, HighBidderId: 1, HighBid: 890);
        var action = await new EasyBot(1).DecideAsync(engine.GetView(0), decision, [new PassBid(), new PlaceBid(900)]);
        Assert.IsType<PassBid>(action);
    }
}
