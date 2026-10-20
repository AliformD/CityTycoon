using CityTycoon.Engine.Actions;
using CityTycoon.Engine.Content;
using CityTycoon.Engine.Events;
using CityTycoon.Engine.Tests.Helpers;

namespace CityTycoon.Engine.Tests.Building;

/// <summary>
/// Section 9 (building) and Section 10 (zoning and permits). Player 0 owns the
/// Brown group (Mill Street and Cobble Lane, $50 per level) in Old Town, and
/// it's player 0's turn, so they may build before rolling.
/// </summary>
public class BuildingTests
{
    private static TestGame BrownOwner(ZoningLevel zoning = ZoningLevel.Residential)
    {
        var game = new TestGame();
        game.Give(0, "mill-street", "cobble-lane");
        game.State.Zoning["old-town"] = zoning;
        return game;
    }

    private static void SetLevels(TestGame game, int mill, int cobble)
    {
        game.Property("mill-street").Level = mill;
        game.Property("cobble-lane").Level = cobble;
    }

    [Fact]
    public void Building_CostsTheHouseCost_AndRaisesTheLevel()
    {
        var game = BrownOwner();
        game.Do(0, new Build("mill-street"));

        Assert.Equal(1, game.Property("mill-street").Level);
        Assert.Equal(1450, game.Player(0).Cash);
        Assert.Contains(game.State.Log, e => e is BuildingBuilt { PropertyId: "mill-street", NewLevel: 1, Cost: 50 });
    }

    [Fact]
    public void YouNeedTheWholeGroup()
    {
        var game = new TestGame();
        game.Give(0, "mill-street");
        Assert.DoesNotContain(new Build("mill-street"), game.Engine.GetLegalActions(0));
        Assert.Contains("whole color group", game.Engine.WhyIllegal(0, new Build("mill-street")));
    }

    [Fact]
    public void AMortgagedProperty_InTheGroup_BlocksBuilding()
    {
        var game = BrownOwner();
        game.Property("cobble-lane").IsMortgaged = true;
        Assert.Contains("mortgaged", game.Engine.WhyIllegal(0, new Build("mill-street")));
    }

    [Fact]
    public void APendingProperty_InTheGroup_BlocksBuilding()
    {
        var game = BrownOwner();
        game.Property("cobble-lane").IsPending = true;
        game.Property("cobble-lane").PendingBalance = 40;
        Assert.NotNull(game.Engine.WhyIllegal(0, new Build("mill-street")));
    }

    [Fact]
    public void EvenBuildRule_BuildOnTheLowestPropertyFirst()
    {
        var game = BrownOwner();
        game.Do(0, new Build("mill-street"));

        Assert.Contains("Even-build", game.Engine.WhyIllegal(0, new Build("mill-street")));
        Assert.Null(game.Engine.WhyIllegal(0, new Build("cobble-lane")));
    }

    [Fact]
    public void BuildLimit_IsThreeLevelsPerTurn_AndResetsNextTurn()
    {
        var game = BrownOwner();
        game.Do(0, new Build("mill-street"));
        game.Do(0, new Build("cobble-lane"));
        game.Do(0, new Build("mill-street"));
        Assert.Contains("3 levels", game.Engine.WhyIllegal(0, new Build("cobble-lane")));

        game.SkipTurn(0);
        game.SkipTurn(1);
        game.Do(0, new Build("cobble-lane"));
        Assert.Equal(2, game.Property("cobble-lane").Level);
    }

    [Fact]
    public void YouCantBuild_OnSomeoneElsesTurn()
    {
        var game = BrownOwner();
        game.Give(1, "mill-street", "cobble-lane");
        Assert.False(game.Engine.Submit(1, new Build("mill-street")).Accepted);
    }

    [Fact]
    public void YouCantBuild_InJail()
    {
        var game = new TestGame();
        game.Give(1, "mill-street", "cobble-lane");
        game.JailThenStartTheirTurn(1);
        Assert.Equal("You can't build right now.", game.Engine.WhyIllegal(1, new Build("mill-street")));
    }

    [Theory]
    [InlineData(ZoningLevel.Restricted, 2)]
    [InlineData(ZoningLevel.Residential, 4)]
    [InlineData(ZoningLevel.Commercial, 5)]
    public void Zoning_CapsTheBuildingLevel(ZoningLevel zoning, int cap)
    {
        var game = BrownOwner(zoning);
        SetLevels(game, cap, cap);
        Assert.Contains("zoning", game.Engine.WhyIllegal(0, new Build("mill-street")));

        SetLevels(game, cap - 1, cap);
        Assert.Null(game.Engine.WhyIllegal(0, new Build("mill-street")));
    }

    [Fact]
    public void AHotel_UsesAHotelPermit()
    {
        var game = BrownOwner(ZoningLevel.Commercial);
        SetLevels(game, 4, 4);
        int permits = game.State.HotelPermits;

        game.Do(0, new Build("mill-street"));

        Assert.Equal(5, game.Property("mill-street").Level);
        Assert.Equal(permits - 1, game.State.HotelPermits);
    }

    [Fact]
    public void NoHotelPermitsLeft_MeansNoHotels()
    {
        var game = BrownOwner(ZoningLevel.Commercial);
        SetLevels(game, 4, 4);
        game.State.HotelPermits = 0;
        Assert.Contains("hotel permits", game.Engine.WhyIllegal(0, new Build("mill-street")));
    }

    [Fact]
    public void ASkyscraper_NeedsHotelsOnTheWholeGroup_AndHighRiseZoning()
    {
        var game = BrownOwner(ZoningLevel.HighRise);
        SetLevels(game, 5, 4);
        // Cobble Lane is the lowest, so it's next; Mill Street can't jump ahead.
        Assert.NotNull(game.Engine.WhyIllegal(0, new Build("mill-street")));

        game.State.Zoning["old-town"] = ZoningLevel.Commercial;
        SetLevels(game, 5, 5);
        Assert.Contains("zoning", game.Engine.WhyIllegal(0, new Build("mill-street")));
    }

    [Fact]
    public void ASkyscraper_CostsThreeTimesTheHouseCost_UsesASkyscraperPermit_AndReturnsTheHotelPermit()
    {
        var game = BrownOwner(ZoningLevel.HighRise);
        SetLevels(game, 5, 5);
        int hotels = game.State.HotelPermits;
        int skyscrapers = game.State.SkyscraperPermits;

        game.Do(0, new Build("mill-street"));

        Assert.Equal(6, game.Property("mill-street").Level);
        Assert.Equal(1500 - 150, game.Player(0).Cash);
        Assert.Equal(hotels + 1, game.State.HotelPermits);
        Assert.Equal(skyscrapers - 1, game.State.SkyscraperPermits);
    }

    [Fact]
    public void YouNeedTheCash()
    {
        var game = BrownOwner();
        game.Player(0).Cash = 40;
        Assert.Contains("cash", game.Engine.WhyIllegal(0, new Build("mill-street")));
    }

    [Fact]
    public void Rent_UsesTheBuildingLevel()
    {
        var game = new TestGame();
        game.Give(1, "market-street", "baker-street", "fountain-square");
        game.Property("market-street").Level = 2;
        game.RollOnto(0, "market-street");
        Assert.Equal(1500 - 200, game.Player(0).Cash); // the 2-house rent
    }

    [Fact]
    public void Grandfathering_BuildingsAboveTheCapStay_ButYouCantAddMore()
    {
        var game = BrownOwner(ZoningLevel.Restricted);
        SetLevels(game, 3, 3); // built before the district was downzoned
        Assert.Contains("zoning", game.Engine.WhyIllegal(0, new Build("mill-street")));
        Assert.Equal(3, game.Property("mill-street").Level);
    }
}
