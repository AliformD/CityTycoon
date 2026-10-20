using CityTycoon.Engine.Content;
using CityTycoon.Engine.Rules;
using CityTycoon.Engine.Tests.Helpers;

namespace CityTycoon.Engine.Tests.RentAndMoney;

/// <summary>Section 13.1 (the Phase 1 parts) and Section 15.2 tax spaces. Player 1 owns; player 0 lands.</summary>
public class RentTests
{
    [Fact]
    public void BaseRent_IsPaidToTheOwner()
    {
        var game = new TestGame();
        game.Give(1, "market-street");
        game.RollOnto(0, "market-street");
        Assert.Equal(1500 - 14, game.Player(0).Cash);
        Assert.Equal(1500 + 14, game.Player(1).Cash);
    }

    [Fact]
    public void FullSet_DoublesBaseRent()
    {
        var game = new TestGame();
        game.Give(1, "market-street", "baker-street", "fountain-square");
        game.RollOnto(0, "market-street");
        Assert.Equal(1500 + 28, game.Player(1).Cash);
    }

    [Fact]
    public void AMortgagedPropertyInTheGroup_BreaksTheFullSetBonus()
    {
        var game = new TestGame();
        game.Give(1, "market-street", "baker-street", "fountain-square");
        game.Property("baker-street").IsMortgaged = true;
        game.RollOnto(0, "market-street");
        Assert.Equal(1500 + 14, game.Player(1).Cash);
    }

    [Fact]
    public void MortgagedProperties_CollectNoRent()
    {
        var game = new TestGame();
        game.Give(1, "market-street");
        game.Property("market-street").IsMortgaged = true;
        game.RollOnto(0, "market-street");
        Assert.Equal(1500, game.Player(0).Cash);
    }

    [Fact]
    public void BuildingLevels_UseTheRentTable()
    {
        var game = new TestGame();
        game.Give(1, "market-street", "baker-street", "fountain-square");
        game.Property("market-street").Level = 2; // 2 houses
        game.RollOnto(0, "market-street");
        Assert.Equal(1500 - 200, game.Player(0).Cash);
    }

    [Fact]
    public void LandingOnYourOwnProperty_CostsNothing()
    {
        var game = new TestGame();
        game.Give(0, "market-street");
        game.RollOnto(0, "market-street");
        Assert.Equal(1500, game.Player(0).Cash);
    }

    [Theory]
    [InlineData(1, 25)]
    [InlineData(2, 50)]
    [InlineData(3, 100)]
    [InlineData(4, 200)]
    public void RailroadRent_DependsOnHowManyTheOwnerHas(int owned, int rent)
    {
        var game = new TestGame();
        string[] stations = ["midtown-station", "old-town-station", "harbor-station", "uptown-station"];
        game.Give(1, stations.Take(owned).ToArray());
        game.RollOnto(0, "midtown-station");
        Assert.Equal(1500 - rent, game.Player(0).Cash);
    }

    [Theory]
    [InlineData(1, 4 * 5)]  // one utility: 4 x the dice (2 + 3)
    [InlineData(2, 10 * 5)] // both: 10 x the dice
    public void UtilityRent_IsAMultipleOfTheDiceTotal(int owned, int rent)
    {
        var game = new TestGame();
        game.Give(1, new[] { "city-power-light", "city-water-works" }.Take(owned).ToArray());
        game.RollOnto(0, "city-power-light");
        Assert.Equal(1500 - rent, game.Player(0).Cash);
    }

    [Theory]
    [InlineData("income-tax", 200)]
    [InlineData("luxury-tax", 100)]
    public void TaxSpaces_ChargeTheirAmount(string space, int tax)
    {
        var game = new TestGame();
        game.RollOnto(0, space);
        Assert.Equal(1500 - tax, game.Player(0).Cash);
    }

    [Fact]
    public void RentCalculator_GivesZero_ForUnownedProperties()
    {
        var game = new TestGame();
        Assert.Equal(0, RentCalculator.Calculate(game.State, GameContent.Default, "platinum-row", 7));
    }
}
