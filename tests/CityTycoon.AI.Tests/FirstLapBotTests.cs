using CityTycoon.Engine;
using CityTycoon.Engine.Events;
using CityTycoon.Engine.Hosting;

namespace CityTycoon.AI.Tests;

/// <summary>Decision #165: bots buy at the listed price on their first lap when they would bid that much anyway.</summary>
public class FirstLapBotTests
{
    [Fact]
    public async Task Bots_BuyAtTheListedPrice_OnTheirFirstLap()
    {
        int listed = 0;
        for (ulong seed = 1; seed <= 3; seed++)
        {
            var engine = GameEngine.NewGame(new GameSetup
            {
                Players = Enumerable.Range(0, 4).Select(i => new PlayerSetup($"Bot {i}")).ToList(),
                Seed = seed,
                MaxRounds = 12,
            });
            await new GameHost(engine, engine.State.Players.ToDictionary(p => p.Id, p => (IPlayerController)new HardBot(seed * 7 + (ulong)p.Id))).RunAsync();
            listed += engine.State.Log.OfType<AuctionWon>().Count(w => w.ListedPrice);
            // Nobody Nudges before passing GO for the first time.
            var passedGo = new HashSet<int>();
            foreach (var e in engine.State.Log)
            {
                if (e is PassedGo g)
                    passedGo.Add(g.PlayerId);
                else if (e is Nudged n)
                    Assert.Contains(n.PlayerId, passedGo);
            }
        }
        Assert.True(listed >= 15, $"{listed} listed-price buys in 3 games");
    }
}
