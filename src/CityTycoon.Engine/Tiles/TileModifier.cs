using CityTycoon.Engine.Content;
using CityTycoon.Engine.Modifiers;
using CityTycoon.Engine.State;

namespace CityTycoon.Engine.Tiles;

/// <summary>
/// A revealed Government tile's neighbor effect (Section 12.3), as a rule
/// modifier. Every tile uses this one class: the numbers come from the
/// tile's "neighbor" settings in govtiles.json.
/// </summary>
public sealed class TileModifier(GameState state, BoardSpace tile, GovTileDefinition definition) : IRuleModifier
{
    private readonly IReadOnlyList<string> neighbors = TileRules.Neighbors(state, tile);
    private readonly TileNeighborEffect effect = definition.Neighbor ?? new TileNeighborEffect();

    public string Source => $"{definition.Name} next door";

    private bool IsNeighbor(string propertyId) => neighbors.Contains(propertyId);

    public int RentPercent(GameState s, string propertyId) => IsNeighbor(propertyId) ? effect.RentPercent : 0;

    public int BuildDiscountPercent(GameState s, string propertyId) => IsNeighbor(propertyId) ? effect.BuildDiscountPercent : 0;

    public bool CanBeTargetedByCards(GameState s, string propertyId) => !(IsNeighbor(propertyId) && effect.ProtectFromCards);

    public bool ProtectsFromFire(GameState s, string propertyId) => IsNeighbor(propertyId) && effect.ProtectFromFire;
}
