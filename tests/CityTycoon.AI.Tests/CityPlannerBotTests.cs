using CityTycoon.Engine;
using CityTycoon.Engine.Actions;
using CityTycoon.Engine.Content;

namespace CityTycoon.AI.Tests;

/// <summary>Decision #158: bots peek where it matters, and the City Planner swaps a tile that hurts it.</summary>
public class CityPlannerBotTests
{
    private static GameEngine NewGame() => GameEngine.NewGame(new GameSetup
    {
        Players = [new("Planner"), new("Other")],
        Seed = 3,
        ShuffleTurnOrder = false,
    });

    /// <summary>The properties on either side of a tile space.</summary>
    private static List<string> NeighborsOf(GameEngine engine, string spaceId)
    {
        var board = engine.State.Board;
        int i = board.FindIndex(s => s.Id == spaceId), n = board.Count;
        return new[] { board[(i - 1 + n) % n], board[(i + 1) % n] }.Where(s => s.Kind == SpaceKind.Property).Select(s => s.Id).ToList();
    }

    [Fact]
    public async Task Bots_PeekNextToTheirOwnProperties()
    {
        var engine = NewGame();
        var tiles = engine.State.Board.Where(s => s.Kind == SpaceKind.GovernmentTile).Select(s => s.Id).ToList();
        foreach (string id in NeighborsOf(engine, tiles[2]))
            engine.State.Properties[id].OwnerId = 0;
        var decision = new PendingDecision(0, DecisionKind.PeekTile, "");

        var choice = await new HardBot(1).DecideAsync(engine.GetView(0), decision, tiles.Select(t => (GameAction)new PeekAtTile(t)).ToList());

        Assert.Equal(new PeekAtTile(tiles[2]), choice);
    }

    [Theory]
    [InlineData(0, true)]   // the Landfill cuts its own rent: swap it away
    [InlineData(1, false)]  // it cuts a rival's rent: leave it there
    public async Task ThePlanner_SwapsALandfill_NextToItsOwnProperty(int neighborOwner, bool swaps)
    {
        var engine = NewGame();
        var space = engine.State.Board.First(s => s.Kind == SpaceKind.GovernmentTile && NeighborsOf(engine, s.Id).Count == 2);
        space.TileId = "landfill";
        engine.State.Player(0).KnownTiles.Add(space.Id);
        foreach (string id in NeighborsOf(engine, space.Id))
            engine.State.Properties[id].OwnerId = neighborOwner;
        var decision = new PendingDecision(0, DecisionKind.SwapTile, "", PropertyId: space.Id);

        var choice = await new HardBot(1).DecideAsync(engine.GetView(0), decision, [new SwapTile(space.Id), new NoThanks()]);

        Assert.Equal(swaps ? new SwapTile(space.Id) : new NoThanks(), choice);
    }
}
