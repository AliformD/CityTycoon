using CityTycoon.Engine.Content;

namespace CityTycoon.Engine.Tests.Content;

/// <summary>Content tests (Section 26.1): every file loads, IDs are unique, references are valid.</summary>
public class GameContentTests
{
    private readonly GameContent content = GameContent.LoadFromFiles();

    [Fact]
    public void AllContentFiles_Load_AndPassValidation() => Assert.NotNull(content);

    [Fact]
    public void Properties_Are28_With22Color4Railroads2Utilities()
    {
        Assert.Equal(28, content.Properties.Count);
        Assert.Equal(22, content.Properties.Values.Count(p => p.Type == PropertyType.Color));
        Assert.Equal(4, content.Properties.Values.Count(p => p.Type == PropertyType.Railroad));
        Assert.Equal(2, content.Properties.Values.Count(p => p.Type == PropertyType.Utility));
    }

    [Fact]
    public void Groups_Are8_AndEachHasMembers()
    {
        Assert.Equal(8, content.Groups.Count);
        Assert.All(content.Groups.Keys, g => Assert.NotEmpty(content.GroupMembers(g)));
    }

    [Fact]
    public void Board_Has4DistrictsOf10FixedSpaces_And48SpacesOnceTilesAreAdded()
    {
        Assert.Equal(4, content.Board.Districts.Count);
        Assert.All(content.Board.Districts, d => Assert.Equal(10, d.Spaces.Count));
        Assert.Equal(48, content.BoardSize);
    }

    [Fact]
    public void GovernmentTiles_Are7_With4UsedPerGame()
    {
        Assert.Equal(7, content.GovTiles.Count);
        Assert.Equal(4, content.GovTilesPerGame);
    }

    [Fact]
    public void PlatinumRow_MatchesTheSpecExample()
    {
        PropertyDefinition p = content.Property("platinum-row");
        Assert.Equal(400, p.AssessedValue);
        Assert.Equal(200, p.HouseCost);
        Assert.Equal(new[] { 50, 200, 600, 1400, 1700, 2000, 3000 }, p.Rents);
    }

    [Fact]
    public void Validation_CatchesABoardSpaceThatPointsAtAMissingProperty()
    {
        var badBoard = content.Board with
        {
            Districts = [.. content.Board.Districts.Skip(1),
                content.Board.Districts[0] with { Spaces = [new SpaceDefinition("no-such-street", SpaceKind.Property), .. content.Board.Districts[0].Spaces.Skip(1)] }],
        };
        Assert.Throws<ContentException>(() => new GameContent(
            content.Rules, badBoard,
            new PropertiesFile(1, content.Properties.Values.ToList()),
            new GroupsFile(1, content.Groups.Values.ToList()),
            new GovTilesFile(1, content.GovTilesPerGame, content.GovTiles)));
    }

    // ----- Perks, bonuses and characters (Phase 7) -----

    [Fact]
    public void EveryPerkAndBonusAndCharacter_ChangesAtLeastOneRule()
    {
        Assert.All(content.Groups.Values, g => Assert.NotEmpty(g.Perk.Modifiers!));
        Assert.All(content.Bonuses, b => Assert.NotEmpty(b.Modifiers!));
        Assert.All(content.Characters, c => Assert.NotEmpty(c.Modifiers!));
    }

    [Fact]
    public void Characters_AreEightWithDifferentIds()
    {
        Assert.Equal(8, content.Characters.Count);
        Assert.Equal(8, content.Characters.Select(c => c.Id).Distinct().Count());
    }

    [Fact]
    public void Bonuses_AreFive_ForRailroadsAndUtilities()
    {
        Assert.Equal(5, content.Bonuses.Count);
        Assert.Equal(3, content.Bonuses.Count(b => b.PropertyType == PropertyType.Railroad));
        Assert.Equal(2, content.Bonuses.Count(b => b.PropertyType == PropertyType.Utility));
    }

    [Fact]
    public void EveryModifierTypeInTheContent_IsRegistered()
    {
        var specs = content.Groups.Values.SelectMany(g => g.Perk.Modifiers!)
            .Concat(content.Bonuses.SelectMany(b => b.Modifiers!))
            .Concat(content.Characters.SelectMany(c => c.Modifiers!));
        Assert.All(specs, s => Assert.Contains(s.Type, Modifiers.ModifierTypes.Types));
    }

    [Fact]
    public void AModifierWithAMadeUpType_IsRejected()
    {
        var spec = new ModifierSpec("FlyingCars", 1);
        Assert.Throws<ContentException>(() => Modifiers.ModifierTypes.Validate(spec, "a test", content));
    }
}
