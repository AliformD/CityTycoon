using CityTycoon.Engine;
using CityTycoon.Engine.Actions;

namespace CityTycoon.AI.Tests;

/// <summary>Bots use the new Nudge cost (decision #151) and the stronger character abilities (decision #152).</summary>
public class StrongerAbilityBotTests
{
    private static GameEngine NewGame(string? character = null) => GameEngine.NewGame(new GameSetup
    {
        Players = [new("Bot", character), new("Other")],
        Seed = 3,
        ShuffleTurnOrder = false,
    });

    private static void PlaceBefore(GameEngine engine, string spaceId, int spaces)
    {
        int n = engine.State.Board.Count;
        engine.State.Player(0).Position = (engine.State.IndexOf(spaceId) - spaces + n) % n;
    }

    /// <summary>Platinum Row with a hotel: $1,700 rent for the bot.</summary>
    private static void BigRent(GameEngine engine)
    {
        engine.State.Properties["platinum-row"].OwnerId = 1;
        engine.State.Properties["platinum-row"].Level = 4;
    }

    [Theory]
    [InlineData("easy")]
    [InlineData("normal")]
    [InlineData("hard")]
    public async Task Bots_NudgeOnlyAsFarAsTheyNeed_SinceEachSpaceCostsAToken(string kind)
    {
        var engine = NewGame();
        BigRent(engine);
        engine.State.Player(0).Cash = 100; // too little to want an unowned property, so every other space is about the same
        PlaceBefore(engine, "platinum-row", 7);
        EasyBot bot = kind switch { "easy" => new EasyBot(1), "normal" => new NormalBot(1), _ => new HardBot(1) };
        var decision = new PendingDecision(0, DecisionKind.ChooseNudge, "", Dice: [3, 4]);
        GameAction[] legal = [new Nudge(-2), new Nudge(-1), new Nudge(0), new Nudge(1), new Nudge(2)];

        var choice = (Nudge)await bot.DecideAsync(engine.GetView(0), decision, legal);

        Assert.Equal(1, Math.Abs(choice.Spaces)); // one space is enough to dodge the rent
    }

    [Fact]
    public async Task Hustler_RerollsARollThatLandsOnBigRent()
    {
        var engine = NewGame("hustler");
        engine.State.Player(0).Momentum = 0;
        BigRent(engine);
        PlaceBefore(engine, "platinum-row", 7);
        var decision = new PendingDecision(0, DecisionKind.RerollMove, "", Dice: [3, 4]);

        var choice = await new EasyBot(1).DecideAsync(engine.GetView(0), decision, [new RerollDice(), new NoThanks()]);

        Assert.Equal(new RerollDice(), choice);
    }

    [Fact]
    public async Task Hustler_KeepsAnOrdinaryRoll()
    {
        var engine = NewGame("hustler");
        engine.State.Player(0).Momentum = 0;
        PlaceBefore(engine, "jail", 7);
        var decision = new PendingDecision(0, DecisionKind.RerollMove, "", Dice: [3, 4]);

        var choice = await new HardBot(1).DecideAsync(engine.GetView(0), decision, [new RerollDice(), new NoThanks()]);

        Assert.Equal(new NoThanks(), choice);
    }

    [Fact]
    public async Task Medic_AlwaysTakesTheFreeInsuredMarker()
    {
        var engine = NewGame("medic");
        engine.State.Player(0).Cash = 60; // too poor to buy one at $50
        var decision = new PendingDecision(0, DecisionKind.HospitalVisit, "", AmountOwed: 0);

        var choice = await new EasyBot(1).DecideAsync(engine.GetView(0), decision, [new BuyInsurance(), new NoThanks()]);

        Assert.Equal(new BuyInsurance(), choice);
    }

    [Fact]
    public async Task CityPlanner_ReversesAnOrdinanceThatHurtsIt()
    {
        var engine = NewGame("city-planner");
        engine.State.Properties["mill-street"].OwnerId = 0;
        engine.State.Properties["cobble-lane"].OwnerId = 0;
        engine.State.Properties["mill-street"].Level = 3; // it has built the most, so a tax per building hurts it
        var decision = new PendingDecision(0, DecisionKind.ReverseCouncilVote, "", OrdinanceId: "property-tax-hike", Passed: true);

        var choice = await new NormalBot(1).DecideAsync(engine.GetView(0), decision, [new ReverseVote(), new NoThanks()]);

        Assert.Equal(new ReverseVote(), choice);
    }

    [Fact]
    public async Task CityPlanner_LetsAGoodResultStand()
    {
        var engine = NewGame("city-planner");
        var decision = new PendingDecision(0, DecisionKind.ReverseCouncilVote, "", OrdinanceId: "stimulus-check", Passed: true);

        var choice = await new NormalBot(1).DecideAsync(engine.GetView(0), decision, [new ReverseVote(), new NoThanks()]);

        Assert.Equal(new NoThanks(), choice);
    }
}
