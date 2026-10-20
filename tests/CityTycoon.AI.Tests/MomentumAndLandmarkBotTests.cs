using CityTycoon.Engine;
using CityTycoon.Engine.Actions;

namespace CityTycoon.AI.Tests;

/// <summary>Phase 4 bot behaviour: Nudges, Precision Rolls and landmarks.</summary>
public class MomentumAndLandmarkBotTests
{
    private static GameEngine NewGame() => GameEngine.NewGame(new GameSetup
    {
        Players = [new("Bot"), new("Other")],
        Seed = 3,
        ShuffleTurnOrder = false,
    });

    /// <summary>Puts player 0 this many spaces before a space.</summary>
    private static void PlaceBefore(GameEngine engine, string spaceId, int spaces)
    {
        int n = engine.State.Board.Count;
        engine.State.Player(0).Position = (engine.State.IndexOf(spaceId) - spaces + n) % n;
    }

    [Fact]
    public async Task EasyBot_NudgesAwayFromBigRent()
    {
        var engine = NewGame();
        engine.State.Properties["platinum-row"].OwnerId = 1;
        engine.State.Properties["platinum-row"].Level = 4; // $1,700 rent
        PlaceBefore(engine, "platinum-row", 7);
        var decision = new PendingDecision(0, DecisionKind.ChooseNudge, "", Dice: [3, 4]);
        GameAction[] legal = [new Nudge(-2), new Nudge(-1), new Nudge(0), new Nudge(1), new Nudge(2)];

        var choice = await new EasyBot(1).DecideAsync(engine.GetView(0), decision, legal);

        Assert.IsType<Nudge>(choice);
        Assert.NotEqual(0, ((Nudge)choice).Spaces);
    }

    [Fact]
    public async Task EasyBot_DoesntWasteATokenForNothing()
    {
        var engine = NewGame();
        engine.State.Properties["marina-drive"].OwnerId = 0; // the space just before the Mall is its own
        PlaceBefore(engine, "mall", 7);
        var decision = new PendingDecision(0, DecisionKind.ChooseNudge, "", Dice: [3, 4]);

        var choice = await new EasyBot(1).DecideAsync(engine.GetView(0), decision, [new Nudge(-1), new Nudge(0)]);

        Assert.Equal(new Nudge(0), choice);
    }

    [Fact]
    public async Task EasyBot_KeepsTheDiceThatAvoidRent()
    {
        var engine = NewGame();
        engine.State.Properties["platinum-row"].OwnerId = 1;
        engine.State.Properties["platinum-row"].Level = 4;
        PlaceBefore(engine, "platinum-row", 7);
        var decision = new PendingDecision(0, DecisionKind.ChoosePrecisionDice, "", Dice: [3, 4, 5]);

        var choice = (ChooseDice)await new EasyBot(1).DecideAsync(engine.GetView(0), decision, []);
        int[] dice = [3, 4, 5];

        Assert.NotEqual(7, dice[choice.First] + dice[choice.Second]);
    }

    [Fact]
    public async Task EasyBot_WalksPastTheCasino()
    {
        var engine = NewGame();
        var decision = new PendingDecision(0, DecisionKind.CasinoBet, "", MaxBet: 1500);
        var choice = await new EasyBot(1).DecideAsync(engine.GetView(0), decision, [new NoThanks(), new BetHighLow(10, true)]);
        Assert.Equal(new NoThanks(), choice);
    }

    [Fact]
    public async Task EasyBot_PicksItsOwnGroupAtTheStadium()
    {
        var engine = NewGame();
        engine.State.Properties["skyline-drive"].OwnerId = 0;
        engine.State.Properties["platinum-row"].OwnerId = 0;
        engine.State.Properties["market-street"].OwnerId = 1;
        var decision = new PendingDecision(0, DecisionKind.ChooseStadiumGroup, "");

        var choice = await new EasyBot(1).DecideAsync(engine.GetView(0), decision, []);

        Assert.Equal(new PickStadiumGroup("dark-blue"), choice);
    }

    [Fact]
    public async Task EasyBot_BuysInsurance_OnlyWithSpareCash()
    {
        var engine = NewGame();
        var decision = new PendingDecision(0, DecisionKind.HospitalVisit, "");
        var bot = new EasyBot(1);
        GameAction[] legal = [new BuyInsurance(), new NoThanks()];

        Assert.Equal(new BuyInsurance(), await bot.DecideAsync(engine.GetView(0), decision, legal));
        engine.State.Player(0).Cash = bot.SafetyReserve;
        Assert.Equal(new NoThanks(), await bot.DecideAsync(engine.GetView(0), decision, legal));
    }
}
