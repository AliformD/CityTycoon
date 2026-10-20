using CityTycoon.Engine.Content;
using CityTycoon.Engine.Randomness;
using CityTycoon.Engine.State;

namespace CityTycoon.Engine.Board;

/// <summary>Builds a new random board for each game (Section 4.3).</summary>
public static class BoardGenerator
{
    public static List<BoardSpace> Generate(GameContent content, Pcg32 rng)
    {
        // Steps 1 and 2: shuffle all the tile types, keep the first few (one per
        // district). Because the list is shuffled, which tile goes to which
        // district is random too. Nobody is told which tiles were left out.
        var tiles = content.GovTiles.Select(t => t.Id).ToList();
        rng.Shuffle(tiles);
        tiles = tiles.Take(content.GovTilesPerGame).ToList();

        var board = new List<BoardSpace>();
        for (int side = 0; side < content.Board.Corners.Count; side++)
        {
            board.Add(FromDefinition(content, content.Board.Corners[side], districtId: null));

            DistrictDefinition district = content.Board.Districts[side];
            var spaces = district.Spaces.Select(s => FromDefinition(content, s, district.Id)).ToList();

            // Step 3: slot the tile into one of the 11 gaps: before the first
            // space, between two spaces, or after the last one. Never on a corner.
            int slot = rng.NextInt(spaces.Count + 1);
            spaces.Insert(slot, new BoardSpace
            {
                Id = $"{district.Id}-government",
                Name = "Government Tile",
                Kind = SpaceKind.GovernmentTile,
                DistrictId = district.Id,
                TileId = tiles[side],
                IsRevealed = false, // Step 4: face-down.
            });

            board.AddRange(spaces);
        }

        // Step 5: number the finished ring.
        for (int i = 0; i < board.Count; i++)
            board[i].Index = i;

        return board;
    }

    private static BoardSpace FromDefinition(GameContent content, SpaceDefinition def, string? districtId) => new()
    {
        Id = def.Id,
        Name = def.Kind == SpaceKind.Property ? content.Property(def.Id).Name : def.Name!,
        Kind = def.Kind,
        DistrictId = districtId,
        Tax = def.Tax,
        Landmark = def.Landmark,
    };
}
