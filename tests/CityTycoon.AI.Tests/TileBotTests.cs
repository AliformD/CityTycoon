using CityTycoon.Engine;
using CityTycoon.Engine.Actions;

namespace CityTycoon.AI.Tests;

/// <summary>Phase 6 bot behaviour: Government tiles.</summary>
public class TileBotTests
{
    private static GameEngine NewGame() => GameEngine.NewGame(new GameSetup
    {
        Players = [new("Bot"), new("Other")],
        Seed = 3,
        ShuffleTurnOrder = false,
    });

    [Theory]
    [InlineData(200, true)]
    [InlineData(10, false)]
    public async Task EasyBot_HopsOntoTheTransitHub_OnlyWhenTheRentIsBigger(int rent, bool hops)
    {
        var engine = NewGame();
        var decision = new PendingDecision(0, DecisionKind.TransitHop, "", "maple-avenue", AmountOwed: rent);
        GameAction[] legal = [new NoThanks(), new HopToTransitHub()];

        var choice = await new EasyBot(1).DecideAsync(engine.GetView(0), decision, legal);

        Assert.Equal(hops ? new HopToTransitHub() : new NoThanks(), choice);
    }

    [Fact]
    public async Task EasyBot_PlaysWholeGamesWithEveryTileRevealed()
    {
        // Every tile face-up from the start, so their effects come up a lot.
        var engine = NewGame();
        foreach (var space in engine.State.Board.Where(s => s.TileId != null))
            space.IsRevealed = true;
        var host = new Engine.Hosting.GameHost(engine, new Dictionary<int, Engine.Hosting.IPlayerController>
        {
            [0] = new EasyBot(1),
            [1] = new RandomBot(2),
        });
        for (int i = 0; i < 3000 && !engine.State.IsOver; i++)
            await host.StepAsync();
        Assert.True(engine.State.IsOver);
    }
}
