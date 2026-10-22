using CityTycoon.Engine.Actions;
using CityTycoon.Engine.Content;
using CityTycoon.Engine.Events;
using CityTycoon.Engine.Tests.Helpers;
using CityTycoon.Engine.Tiles;

namespace CityTycoon.Engine.Tests.Characters;

/// <summary>Decision #158: the City Planner swaps peeked tiles and pays no tile fees.</summary>
public class CityPlannerTileTests
{
    private static TestGame Planner() => new(2, characters: ["city-planner", "mogul"]);

    [Fact]
    public void AfterAPeek_ThePlannerCanSwapTheTile_ForOneNotInPlay()
    {
        var game = Planner();
        var space = game.State.Board.First(s => s.Kind == SpaceKind.GovernmentTile && !s.IsRevealed);
        string before = space.TileId!;
        var unused = TileRules.UnusedTiles(game.State, game.Engine.Content);

        game.Do(0, new PeekAtTile(space.Id));
        game.Do(0, new SwapTile(space.Id));

        Assert.NotEqual(before, space.TileId);
        Assert.Contains(space.TileId, unused);
        Assert.False(space.IsRevealed);
        Assert.Contains(before, TileRules.UnusedTiles(game.State, game.Engine.Content)); // the old one is out of play now
        Assert.Equal(space.TileId, game.Engine.GetView(0).Spaces.First(s => s.Id == space.Id).TileId); // the Planner knows the new one
        Assert.Null(game.Engine.GetView(1).Spaces.First(s => s.Id == space.Id).TileId);
        Assert.Contains(game.State.Log, e => e is TileSwapped { PlayerId: 0 });
        Assert.Equal(DecisionKind.ChooseMoveMethod, game.DecisionFor(0)!.Kind);
    }

    [Fact]
    public void ASwap_WipesWhatOthersKnewAboutThatTile()
    {
        var game = Planner();
        var space = game.State.Board.First(s => s.Kind == SpaceKind.GovernmentTile && !s.IsRevealed);
        game.Player(1).KnownTiles.Add(space.Id); // the Mogul had peeked with Inside Source

        game.Do(0, new PeekAtTile(space.Id));
        game.Do(0, new SwapTile(space.Id));

        Assert.DoesNotContain(space.Id, game.Player(1).KnownTiles);
        Assert.Null(game.Engine.GetView(1).Spaces.First(s => s.Id == space.Id).TileId);
    }

    [Fact]
    public void ThePlanner_PaysNoTileFees()
    {
        var game = Planner();
        game.Do(0, new PeekAtTile(game.State.Board.First(s => s.Kind == SpaceKind.GovernmentTile).Id));
        game.Do(0, new NoThanks());
        game.SetTile("landfill", revealed: false);
        game.Player(0).Momentum = 0;
        game.RollOnto(0, "old-town-government");

        Assert.Equal(1500, game.Player(0).Cash);
        Assert.Contains(game.State.Log, e => e is TileFeeWaived { PlayerId: 0, Amount: 25 });
    }

    [Fact]
    public void OthersStillPayTileFees()
    {
        var game = Planner();
        game.Do(0, new PeekAtTile(game.State.Board.First(s => s.Kind == SpaceKind.GovernmentTile).Id));
        game.Do(0, new NoThanks());
        game.SetTile("landfill", revealed: false);
        game.SkipTurn(0);
        game.Player(1).Momentum = 0;
        game.RollOnto(1, "old-town-government");

        Assert.Equal(1500 - 25, game.Player(1).Cash);
    }
}
