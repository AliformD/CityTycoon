using CityTycoon.Engine;
using CityTycoon.Engine.Actions;
using CityTycoon.Engine.Content;
using CityTycoon.Engine.Views;

namespace CityTycoon.AI.Tests;

/// <summary>Decision #155: bots count Green's dividend, Dark Blue's cheap skyscrapers and the Contractor's discount.</summary>
public class BuildHigherBotTests
{
    private static readonly string[] Green = ["summit-avenue", "grand-plaza", "crown-street"];
    private static readonly string[] Orange = ["market-street", "baker-street", "fountain-square"];
    private static readonly string[] LightBlue = ["elm-avenue", "maple-avenue", "birch-avenue"];

    private static GameEngine NewGame() => GameEngine.NewGame(new GameSetup
    {
        Players = [new("Human"), new("Bot")],
        Seed = 3,
        ShuffleTurnOrder = false,
    });

    private static void Give(GameEngine engine, int playerId, string[] group, int level = 0)
    {
        foreach (string id in group)
        {
            engine.State.Properties[id].OwnerId = playerId;
            engine.State.Properties[id].Level = level;
        }
    }

    /// <summary>Lets the tests ask for the bot's private sums.</summary>
    private sealed class OpenBot() : NormalBot(1)
    {
        public double SetValue(PlayerView view, int ownerId, string group) => GroupEarnings(view, ownerId, group, 2000, 4000).Earnings;
        public int HowMuchItHelps(PlayerView view, string ordinanceId, string districtId) => Benefit(view, ordinanceId, districtId);
    }

    private static readonly PendingDecision BuildTime =
        new(1, DecisionKind.PostMoveActions, "", Management: ManagementOptions.Build);

    [Theory]
    [InlineData(true, "market-street")]  // a hotel: its rent plus $20 every turn
    [InlineData(false, "elm-avenue")]    // without Green, the third Light Blue house pays more per dollar
    public async Task GreenOwners_PreferHotels_ForTheDividend(bool ownsGreen, string expected)
    {
        var engine = NewGame();
        Give(engine, 1, Orange, level: 4);
        Give(engine, 1, LightBlue, level: 2);
        if (ownsGreen)
            Give(engine, 1, Green);
        engine.State.Player(1).Cash = 2000;

        var choice = await new HardBot(1).DecideAsync(engine.GetView(1), BuildTime,
            [new Build("elm-avenue"), new Build("market-street"), new EndTurn()]);

        Assert.Equal(new Build(expected), choice);
    }

    [Fact]
    public void GreenOwners_PlanHotels_WhereTheZoningAllowsThem()
    {
        var engine = NewGame();
        Give(engine, 1, Green);
        string district = engine.State.Space("crown-street").DistrictId!;
        var bot = new OpenBot();
        engine.State.Zoning[district] = ZoningLevel.Residential; // up to 4 houses
        double houses = bot.SetValue(engine.GetView(1), 1, "green");
        engine.State.Zoning[district] = ZoningLevel.Commercial;  // hotels allowed
        double hotels = bot.SetValue(engine.GetView(1), 1, "green");

        // Before, every owner planned 3 houses, so the zoning made no difference.
        Assert.True(hotels > houses, $"With hotels {hotels:F0} should beat {houses:F0}");
    }

    [Fact]
    public void TheContractor_ValuesASetHigher_BecauseItsHousesAreCheaper()
    {
        var engine = NewGame();
        Give(engine, 1, Orange);
        var bot = new OpenBot();
        double plain = bot.SetValue(engine.GetView(1), 1, "orange");

        engine.State.Player(1).CharacterId = "contractor";
        double contractor = bot.SetValue(engine.GetView(1), 1, "orange");

        Assert.True(contractor > plain, $"Contractor {contractor:F0} should beat {plain:F0}");
    }

    [Fact]
    public void ASetBuiltToTheZoningLimit_WantsRezoningUp()
    {
        var engine = NewGame();
        string district = engine.State.Space("market-street").DistrictId!;
        engine.State.Zoning[district] = ZoningLevel.Residential; // up to 4 houses
        Give(engine, 1, Orange, level: 4);
        var bot = new OpenBot();
        Assert.Equal(2, bot.HowMuchItHelps(engine.GetView(1), "rezone-up", district));

        engine.State.Properties["market-street"].Level = 3;
        Assert.Equal(1, bot.HowMuchItHelps(engine.GetView(1), "rezone-up", district));
    }
}
