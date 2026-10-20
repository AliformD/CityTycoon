using System.Text.Json;
using CityTycoon.Engine.Actions;
using CityTycoon.Engine.Content;
using CityTycoon.Engine.State;
using CityTycoon.Engine.Tests.Helpers;

namespace CityTycoon.Engine.Tests.GameFlow;

public class SaveAndViewTests
{
    [Fact]
    public void GameState_SurvivesARoundTripThroughJson_InTheMiddleOfAnAuction()
    {
        var game = new TestGame(players: 3);
        game.RollOnto(0, "market-street");
        game.Do(0, new PlaceBid(120));

        GameState loaded = GameStateSerializer.FromJson(GameStateSerializer.ToJson(game.State));
        var resumed = new GameEngine(loaded, GameContent.Default, game.Dice);

        Assert.Equal([1, 2], resumed.GetPendingDecisions().Select(d => d.PlayerId));
        Assert.True(resumed.Submit(1, new PassBid()).Accepted);
        Assert.True(resumed.Submit(2, new PassBid()).Accepted);
        Assert.Equal(0, loaded.Properties["market-street"].OwnerId);
        Assert.Equal(1500 - 120, loaded.Player(0).Cash);
    }

    [Fact]
    public void PlayerView_NeverContainsHiddenGovernmentTiles()
    {
        // Fair play (Section 23.1): nothing in a view may reveal which tile is where.
        var game = new TestGame(players: 2);
        var hiddenTileIds = game.State.Board.Where(s => s.Kind == SpaceKind.GovernmentTile).Select(s => s.TileId!).ToList();

        string viewJson = JsonSerializer.Serialize(game.Engine.GetView(1), ContentLoader.JsonOptions);

        Assert.All(hiddenTileIds, id => Assert.DoesNotContain($"\"{id}\"", viewJson));
        Assert.All(game.Engine.GetView(1).Spaces.Where(s => s.IsHiddenTile), s => Assert.Null(s.TileId));
    }

    [Fact]
    public void PlayerView_ShowsARevealedTile()
    {
        var game = new TestGame();
        var tile = game.State.Board.First(s => s.Kind == SpaceKind.GovernmentTile);
        tile.IsRevealed = true;
        Assert.Equal(tile.TileId, game.Engine.GetView(0).Spaces[tile.Index].TileId);
    }
}

public class Phase3SaveTests
{
    [Fact]
    public void GameState_SurvivesARoundTrip_InTheMiddleOfATrade_WithPendingAndBuiltProperties()
    {
        var game = new TestGame();
        game.Give(0, "mill-street", "cobble-lane");
        game.Property("mill-street").Level = 1;
        game.Give(1, "market-street");
        game.Property("market-street").IsPending = true;
        game.Property("market-street").PendingBalance = 70;
        game.Give(1, "baker-street");
        game.Do(0, new ProposeTrade(new TradeOffer(0, 1, 50, 0, [], ["baker-street"])));

        GameState loaded = GameStateSerializer.FromJson(GameStateSerializer.ToJson(game.State));
        var resumed = new GameEngine(loaded, GameContent.Default, game.Dice);

        Assert.Equal(DecisionKind.RespondToTrade, resumed.GetPendingDecisions().Single().Kind);
        Assert.True(resumed.Submit(1, new AcceptTrade()).Accepted);
        Assert.Equal(0, loaded.Properties["baker-street"].OwnerId);
        Assert.True(loaded.Properties["market-street"].IsPending);
        Assert.Equal(1, loaded.Properties["mill-street"].Level);
        Assert.Equal(loaded.Zoning.Count, game.State.Zoning.Count);
    }
}
