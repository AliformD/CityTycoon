using CityTycoon.Engine.Actions;
using CityTycoon.Engine.Content;
using CityTycoon.Engine.Views;

namespace CityTycoon.AI;

/// <summary>
/// Government tiles (decision #158): which hidden tile to peek at, whether the City Planner
/// swaps it, and how good landing on a known tile is.
/// </summary>
public partial class EasyBot
{
    /// <summary>Peek where it matters: next to its own properties, then next to groups it holds a piece of.</summary>
    private GameAction ChoosePeek(PlayerView view, IReadOnlyList<GameAction> legalActions)
    {
        var scored = legalActions.OfType<PeekAtTile>().Select(p => (Action: p, Score: Neighbors(view, p.SpaceId).Sum(id =>
            view.Properties[id].OwnerId == view.ViewerId ? 3.0 : HoldsPieceOf(view, id) ? 1.0 : 0.0))).ToList();
        if (scored.Count == 0)
            return legalActions[rng.NextInt(legalActions.Count)];
        double best = scored.Max(s => s.Score);
        var top = scored.Where(s => s.Score == best).ToList();
        return top[rng.NextInt(top.Count)].Action;
    }

    /// <summary>
    /// The City Planner's swap: swaps when a random tile from the ones it hasn't seen on the board would
    /// do better for it than the one it just saw, by at least $10. A bad tile next to a rival is worth
    /// keeping; a bad one next to its own property is worth swapping away.
    /// </summary>
    private GameAction ChooseSwap(PlayerView view, PendingDecision decision, IReadOnlyList<GameAction> legalActions)
    {
        string spaceId = decision.PropertyId!;
        if (view.Spaces.First(s => s.Id == spaceId).TileId is not string current)
            return new NoThanks();
        // Tiles it hasn't seen on the board: the ones not in play are among these.
        var seen = view.Spaces.Where(s => s.TileId != null).Select(s => s.TileId).ToHashSet();
        var unseen = content.GovTiles.Where(t => !seen.Contains(t.Id)).ToList();
        if (unseen.Count == 0)
            return new NoThanks();
        double now = TileWorth(view, content.GovTile(current), spaceId);
        double swapped = unseen.Average(t => TileWorth(view, t, spaceId));
        return swapped > now + 10 ? new SwapTile(spaceId) : new NoThanks();
    }

    /// <summary>
    /// Roughly what a tile on this space is worth to the bot, in dollars, from its effect on the
    /// two neighbors: rent changes and building discounts help the owner, so they help the bot on its
    /// own properties (and a little on groups it is collecting) and hurt it on a rival's.
    /// </summary>
    protected double TileWorth(PlayerView view, GovTileDefinition tile, string spaceId)
    {
        if (tile.Neighbor is not TileNeighborEffect effect)
            return 0;
        int rivals = Math.Max(1, view.Players.Count(p => !p.IsBankrupt && p.Id != view.ViewerId));
        double worth = 0;
        foreach (string id in Neighbors(view, spaceId))
        {
            PropertyDefinition def = content.Property(id);
            int? owner = view.Properties[id].OwnerId;
            double side = owner == view.ViewerId ? 1
                : owner != null ? -1.0 / rivals
                : HoldsPieceOf(view, id) ? 0.5 : 0;
            double forOwner = def.AssessedValue * effect.RentPercent / 100.0
                + def.HouseCost * 3 * effect.BuildDiscountPercent / 100.0 * 0.5
                + (effect.ProtectFromCards ? 15 : 0) + (effect.ProtectFromFire ? 10 : 0)
                - (effect.TransitHopCost > 0 ? def.AssessedValue * 0.2 : 0);
            worth += side * forOwner;
        }
        return worth;
    }

    /// <summary>What landing on a tile it knows costs or gives the bot (the City Planner pays no fees).</summary>
    private double TileLandingScore(PlayerView view, SpaceInfo space)
    {
        if (space.TileId is not string tileId)
            return 0;
        GovTileDefinition tile = content.GovTile(tileId);
        bool free = view.Me.IgnoresTileFees;
        int myLevels = view.Properties.Values.Where(p => p.OwnerId == view.ViewerId).Sum(p => p.Level);
        return tile.LandingType switch
        {
            "PayBank" => free ? 0 : -tile.Int("amount"),
            "PayOwnerOfAll" => free ? 0 : -tile.Int("amount"),
            "PayPerLevel" => free ? 0 : -myLevels * tile.Int("perLevel"),
            "GainMomentum" => TokenValue * tile.Int("amount"),
            "DrawOpportunity" => 30,
            _ => 0,
        };
    }

    /// <summary>The properties right before and after this space.</summary>
    private static IEnumerable<string> Neighbors(PlayerView view, string spaceId)
    {
        int n = view.Spaces.Count;
        int index = view.Spaces.First(s => s.Id == spaceId).Index;
        return new[] { view.Spaces[(index - 1 + n) % n], view.Spaces[(index + 1) % n] }
            .Where(s => s.Kind == SpaceKind.Property).Select(s => s.Id);
    }

    /// <summary>True if the bot owns another property in this property's color group.</summary>
    private bool HoldsPieceOf(PlayerView view, string propertyId) =>
        content.Property(propertyId).Group is string group
        && content.GroupMembers(group).Any(id => id != propertyId && view.Properties[id].OwnerId == view.ViewerId);
}
