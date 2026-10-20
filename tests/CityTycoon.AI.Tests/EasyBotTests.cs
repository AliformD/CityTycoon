using CityTycoon.Engine;
using CityTycoon.Engine.Actions;
using CityTycoon.Engine.Hosting;

namespace CityTycoon.AI.Tests;

public class EasyBotTests
{
    [Fact]
    public async Task EasyBots_FinishGamesWithoutErrors()
    {
        for (ulong seed = 1; seed <= 30; seed++)
        {
            var engine = GameEngine.NewGame(new GameSetup
            {
                Players = Enumerable.Range(0, 4).Select(i => new PlayerSetup($"Bot {i}")).ToList(),
                Seed = seed,
            });
            var bots = engine.State.Players.ToDictionary(p => p.Id, p => (IPlayerController)new EasyBot(seed * 7 + (ulong)p.Id));
            await new GameHost(engine, bots).RunAsync();
            Assert.True(engine.State.IsOver);
        }
    }

    [Fact]
    public async Task EasyBot_NeverBidsIntoItsSafetyReserve()
    {
        var bot = new EasyBot(5);
        var engine = GameEngine.NewGame(new GameSetup { Players = [new("A"), new("B")], Seed = 1 });
        var view = engine.GetView(0);
        var decision = new PendingDecision(0, DecisionKind.SubmitBid, "", "platinum-row", 200, 1500, 10);

        for (int i = 0; i < 100; i++)
        {
            var action = await bot.DecideAsync(view, decision, [new PassBid(), new PlaceBid(200)]);
            if (action is PlaceBid bid)
                Assert.True(bid.Amount <= view.Me.Cash - bot.SafetyReserve);
        }
    }
}
