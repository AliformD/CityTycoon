using CityTycoon.Engine.Content;
using CityTycoon.Engine.Events;
using CityTycoon.Engine.State;
using CityTycoon.Engine.Steps;

namespace CityTycoon.Engine.Tiles;

/// <summary>Government tiles (Section 12): neighbors, revealing, and the landing effect.</summary>
public static class TileRules
{
    /// <summary>
    /// The properties right before and right after a tile (Section 12.2).
    /// A neighbor that isn't a property (a corner or a card space) is left out.
    /// </summary>
    public static IReadOnlyList<string> Neighbors(GameState state, BoardSpace tile)
    {
        int n = state.Board.Count;
        return new[] { state.Board[(tile.Index - 1 + n) % n], state.Board[(tile.Index + 1) % n] }
            .Where(s => s.Kind == SpaceKind.Property)
            .Select(s => s.Id)
            .ToList();
    }

    /// <summary>The hidden Government tiles this player hasn't peeked at yet.</summary>
    public static List<BoardSpace> HiddenTilesFor(GameState state, PlayerState player) =>
        state.Board.Where(s => s.Kind == SpaceKind.GovernmentTile && !s.IsRevealed && !player.KnownTiles.Contains(s.Id)).ToList();

    /// <summary>A player secretly looks at a hidden tile. Only they learn what it is (Inside Source, the City Planner).</summary>
    public static void Peek(EngineContext ctx, PlayerState player, string spaceId)
    {
        if (!player.KnownTiles.Contains(spaceId))
            player.KnownTiles.Add(spaceId);
        ctx.Emit(new TilePeeked(player.Id, spaceId));
    }

    /// <summary>The revealed Transit Hub next to this property, if there is one and it lets you hop.</summary>
    public static BoardSpace? TransitHubNextTo(GameState state, GameContent content, string propertyId)
    {
        int index = state.IndexOf(propertyId);
        int n = state.Board.Count;
        return new[] { state.Board[(index - 1 + n) % n], state.Board[(index + 1) % n] }
            .FirstOrDefault(s => s.Kind == SpaceKind.GovernmentTile && s.IsRevealed
                && content.GovTile(s.TileId!).Neighbor?.TransitHopCost > 0);
    }

    /// <summary>
    /// A player landed on a tile: reveal it if it was hidden, then apply its
    /// landing effect. Both happen in the same step (Section 17.3, rule 4).
    /// </summary>
    public static void Land(EngineContext ctx, PlayerState player, BoardSpace space)
    {
        GovTileDefinition tile = ctx.Content.GovTile(space.TileId!);
        if (!space.IsRevealed)
        {
            space.IsRevealed = true;
            ctx.Emit(new TileRevealed(player.Id, space.Id, tile.Id));
        }
        TileLandings.Find(tile.LandingType)!.Apply(new TileLandingContext(ctx, player, tile, space));
    }
}
