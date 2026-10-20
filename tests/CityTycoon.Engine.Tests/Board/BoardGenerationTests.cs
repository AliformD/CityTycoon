using CityTycoon.Engine.Board;
using CityTycoon.Engine.Content;
using CityTycoon.Engine.Randomness;
using CityTycoon.Engine.State;

namespace CityTycoon.Engine.Tests.Board;

/// <summary>Section 4.3: a new random board every game.</summary>
public class BoardGenerationTests
{
    private static List<BoardSpace> Generate(ulong seed) => BoardGenerator.Generate(GameContent.Default, new Pcg32(seed));

    [Fact]
    public void Board_Has48Spaces_NumberedInOrder()
    {
        var board = Generate(1);
        Assert.Equal(48, board.Count);
        Assert.Equal(Enumerable.Range(0, 48), board.Select(s => s.Index));
    }

    [Theory]
    [InlineData("go", 0)]
    [InlineData("jail", 12)]
    [InlineData("city-park", 24)]
    [InlineData("go-to-jail", 36)]
    public void Corners_AreAlwaysAtFixedIndexes(string id, int index)
    {
        for (ulong seed = 0; seed < 20; seed++)
            Assert.Equal(index, Generate(seed).FindIndex(s => s.Id == id));
    }

    [Fact]
    public void EachDistrict_HasExactlyOneHiddenTile_NeverOnACorner()
    {
        for (ulong seed = 0; seed < 50; seed++)
        {
            var board = Generate(seed);
            var tiles = board.Where(s => s.Kind == SpaceKind.GovernmentTile).ToList();
            Assert.Equal(4, tiles.Count);
            Assert.Equal(4, tiles.Select(t => t.DistrictId).Distinct().Count());
            Assert.All(tiles, t => Assert.False(t.IsRevealed));
            Assert.All(tiles, t => Assert.NotEqual(0, t.Index % 12));
        }
    }

    [Fact]
    public void FourDifferentTileTypes_ArePickedFromTheSeven()
    {
        var tiles = Generate(9).Where(s => s.Kind == SpaceKind.GovernmentTile).Select(s => s.TileId).ToList();
        Assert.Equal(4, tiles.Distinct().Count());
        Assert.All(tiles, t => Assert.Contains(GameContent.Default.GovTiles, g => g.Id == t));
    }

    [Fact]
    public void TilePositions_AndTypes_ChangeBetweenSeeds()
    {
        var layouts = Enumerable.Range(0, 30)
            .Select(seed => string.Join(",", Generate((ulong)seed).Where(s => s.Kind == SpaceKind.GovernmentTile).Select(s => $"{s.Index}:{s.TileId}")))
            .Distinct()
            .Count();
        Assert.True(layouts > 20, $"Only {layouts} different layouts in 30 seeds.");
    }

    [Fact]
    public void TilesCanLandInEveryOneOfThe11Slots()
    {
        // Old Town's tile can be anywhere from index 1 (before Mill Street) to 11 (after the Hospital).
        var seen = Enumerable.Range(0, 400)
            .Select(seed => Generate((ulong)seed).First(s => s.Kind == SpaceKind.GovernmentTile).Index)
            .ToHashSet();
        Assert.Equal(Enumerable.Range(1, 11).ToHashSet(), seen);
    }

    [Fact]
    public void SameSeed_GivesTheSameBoard()
    {
        Assert.Equal(Generate(77).Select(s => s.Id + s.TileId), Generate(77).Select(s => s.Id + s.TileId));
    }

    [Fact]
    public void FixedSpaces_KeepTheirOrderWithinEachDistrict()
    {
        var board = Generate(5);
        foreach (DistrictDefinition district in GameContent.Default.Board.Districts)
        {
            var onBoard = board.Where(s => s.DistrictId == district.Id && s.Kind != SpaceKind.GovernmentTile).Select(s => s.Id);
            Assert.Equal(district.Spaces.Select(s => s.Id), onBoard);
        }
    }
}
