using CityTycoon.Engine.Actions;
using CityTycoon.Engine.Content;
using CityTycoon.Engine.Events;
using CityTycoon.Engine.Tests.Helpers;

namespace CityTycoon.Engine.Tests.Building;

/// <summary>Section 9.3: selling buildings back to the bank.</summary>
public class SellingTests
{
    private static TestGame BrownOwner(int mill, int cobble)
    {
        var game = new TestGame();
        game.Give(0, "mill-street", "cobble-lane");
        game.State.Zoning["old-town"] = ZoningLevel.HighRise;
        game.Property("mill-street").Level = mill;
        game.Property("cobble-lane").Level = cobble;
        return game;
    }

    [Fact]
    public void Selling_GivesBackHalfTheCost()
    {
        var game = BrownOwner(2, 2);
        game.Do(0, new SellBuilding("mill-street"));

        Assert.Equal(1, game.Property("mill-street").Level);
        Assert.Equal(1500 + 25, game.Player(0).Cash);
        Assert.Contains(game.State.Log, e => e is BuildingSold { Refund: 25, NewLevel: 1 });
    }

    [Fact]
    public void Selling_FollowsTheEvenBuildRule()
    {
        var game = BrownOwner(2, 1);
        Assert.Contains("Even-build", game.Engine.WhyIllegal(0, new SellBuilding("cobble-lane")));
        Assert.Null(game.Engine.WhyIllegal(0, new SellBuilding("mill-street")));
    }

    [Fact]
    public void SellingAHotel_ReturnsItsPermit()
    {
        var game = BrownOwner(5, 5);
        int permits = game.State.HotelPermits;
        game.Do(0, new SellBuilding("mill-street"));
        Assert.Equal(permits + 1, game.State.HotelPermits);
    }

    [Fact]
    public void SellingASkyscraper_TakesAHotelPermitBack()
    {
        var game = BrownOwner(6, 5);
        int hotels = game.State.HotelPermits;
        int skyscrapers = game.State.SkyscraperPermits;

        game.Do(0, new SellBuilding("mill-street"));

        Assert.Equal(5, game.Property("mill-street").Level);
        Assert.Equal(hotels - 1, game.State.HotelPermits);
        Assert.Equal(skyscrapers + 1, game.State.SkyscraperPermits);
        Assert.Equal(1500 + 75, game.Player(0).Cash); // half of 3 x $50
    }

    [Fact]
    public void SellingASkyscraper_WithNoHotelPermitFree_DropsStraightToFourHouses()
    {
        var game = BrownOwner(6, 5);
        game.State.HotelPermits = 0;

        game.Do(0, new SellBuilding("mill-street"));

        Assert.Equal(4, game.Property("mill-street").Level);
        Assert.Equal(1500 + 75 + 25, game.Player(0).Cash); // half of both levels
    }

    [Fact]
    public void NothingToSell()
    {
        var game = BrownOwner(0, 0);
        Assert.Contains("no buildings", game.Engine.WhyIllegal(0, new SellBuilding("mill-street")));
    }
}
