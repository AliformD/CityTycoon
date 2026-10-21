using CityTycoon.Engine.Content;

namespace CityTycoon.Engine.Tests.Content;

public class RulesConfigTests
{
    [Fact]
    public void RulesJson_Loads_WithTheSpecDefaults()
    {
        RulesConfig rules = ContentLoader.LoadRules();

        // Decision #143: less cash at the start, more at GO, no catch-up bonus, 40 rounds.
        Assert.Equal(1200, rules.StartingCash);
        Assert.Equal(300, rules.GoSalary);
        Assert.Equal(0, rules.CatchUpBonus);
        Assert.Equal(40, rules.TimedModeDefaultRounds);
        Assert.Equal(2, rules.NudgeMaxSpaces);
        Assert.Equal(1.5, rules.SkyscraperRentMultiplier);
        Assert.Equal(new[] { 50, 100, 150 }, rules.MallPrices);
    }

    [Theory]
    [InlineData(2, 8, 2)]
    [InlineData(3, 8, 2)]
    [InlineData(4, 10, 3)]
    [InlineData(5, 12, 4)]
    [InlineData(6, 12, 4)]
    public void Permits_MatchThePlayerCountTable(int players, int hotels, int skyscrapers)
    {
        PermitSupply supply = ContentLoader.LoadRules().PermitsFor(players);

        Assert.Equal(hotels, supply.HotelPermits);
        Assert.Equal(skyscrapers, supply.SkyscraperPermits);
    }

    [Fact]
    public void MissingKey_FailsLoudly()
    {
        // "required" properties make the loader refuse a file with a key left out.
        Assert.ThrowsAny<System.Text.Json.JsonException>(() =>
            System.Text.Json.JsonSerializer.Deserialize<RulesConfig>("{ \"startingCash\": 1500 }", ContentLoader.JsonOptions));
    }
}
