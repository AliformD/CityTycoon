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

    // ----- The Casino: Dice Poker and the Showdown (decision #164) -----

    [Fact]
    public async Task Bots_AlwaysPlayTheFreeChip_WithoutTheirOwnMoney()
    {
        var engine = NewGame();
        var decision = new PendingDecision(0, DecisionKind.CasinoBet, "", MaxBet: 1500, FreeChip: 40);
        foreach (var bot in new EasyBot[] { new EasyBot(1), new NormalBot(1), new HardBot(1) })
            Assert.Equal(new PlayDicePoker(0), await bot.DecideAsync(engine.GetView(0), decision, [new PlayDicePoker(0), new NoThanks()]));
    }

    [Fact]
    public async Task Bots_AddTheirOwnMoney_WhenFarBehindNearTheEnd()
    {
        // A desperate bot (last 10 rounds, worth under 60% of the leader) puts a third of its cash on the hand.
        var engine = NewGame();
        engine.State.Round = engine.State.Settings.MaxRounds - 3;
        engine.State.Player(0).Cash = 300;
        engine.State.Player(1).Cash = 3000;
        var decision = new PendingDecision(0, DecisionKind.CasinoBet, "", MaxBet: 300, FreeChip: 40);
        var choice = await new HardBot(1).DecideAsync(engine.GetView(0), decision, [new PlayDicePoker(0), new NoThanks()]);
        Assert.Equal(new PlayDicePoker(100), choice);
    }

    private static async Task<int[]> KeptFaces(EasyBot bot, params int[] dice)
    {
        var engine = NewGame();
        var decision = new PendingDecision(0, DecisionKind.KeepDice, "", Dice: dice, FreeChip: 40);
        var keep = (KeepDice)await bot.DecideAsync(engine.GetView(0), decision, [new KeepDice([0, 1, 2, 3, 4]), new KeepDice([])]);
        return [.. keep.Keep.Select(i => dice[i]).Order()];
    }

    [Fact]
    public async Task Bots_KeepDice_ByTheirSkill()
    {
        // 2 3 4 5 5: a 1 or a 6 makes a straight, so good players chase it; the Easy bot keeps its pair.
        Assert.Equal(new[] { 2, 3, 4, 5 }, await KeptFaces(new HardBot(1), 2, 3, 4, 5, 5));
        Assert.Equal(new[] { 2, 3, 4, 5 }, await KeptFaces(new NormalBot(1), 5, 3, 4, 5, 2));
        Assert.Equal(new[] { 5, 5 }, await KeptFaces(new EasyBot(1), 2, 3, 4, 5, 5));
        // 3 4 4 5 6: only a 2 makes that straight, so the Hard bot keeps the pair.
        Assert.Equal(new[] { 4, 4 }, await KeptFaces(new HardBot(1), 3, 4, 4, 5, 6));
        // A made full house is kept whole.
        Assert.Equal(new[] { 2, 2, 6, 6, 6 }, await KeptFaces(new EasyBot(1), 6, 2, 6, 2, 6));
    }

    [Theory]
    [InlineData(true, 4, 4, 4, 2, 2)]    // a full house: hard to beat, so it pays to call
    [InlineData(false, 4, 4, 2, 2, 1)]   // two pair: easy to beat, so it doesn't
    public async Task HardBot_CallsAShowdown_OnlyWithAGoodHand(bool calls, params int[] dice)
    {
        var engine = NewGame();
        var decision = new PendingDecision(0, DecisionKind.CallShowdown, "", Dice: dice);
        var choice = await new HardBot(1).DecideAsync(engine.GetView(0), decision, [new CallShowdown(1), new NoThanks()]);
        Assert.Equal(calls ? new CallShowdown(1) : new NoThanks(), choice);
    }

    [Theory]
    [InlineData(false, 4, 4, 4, 2, 2)]   // fold against a full house: it wins only about 1 time in 5
    [InlineData(true, 5, 5, 5, 1, 2)]    // play against three of a kind: close to a coin flip
    [InlineData(true, 4, 4, 2, 2, 1)]    // play against two pair
    public async Task Bots_FoldOrPlay_ByTheChanceToBeatTheHand(bool plays, params int[] dice)
    {
        var engine = NewGame();
        var decision = new PendingDecision(0, DecisionKind.RespondToShowdown, "", Showdown: new ShowdownChallenge(1, 0, 100, 25, dice));
        var choice = await new HardBot(1).DecideAsync(engine.GetView(0), decision, [new PlayShowdown(), new FoldShowdown()]);
        Assert.Equal(plays ? new PlayShowdown() : new FoldShowdown(), choice);
    }

    [Fact]
    public async Task HardBot_StopsOnTheCasino_InsteadOfNudgingOnToCityPark()
    {
        // The Casino's free chip (about $39) is worth more to it than City Park's token ($25), one space further.
        var engine = NewGame();
        engine.State.Player(0).Momentum = 2;
        PlaceBefore(engine, "casino", 5);
        var decision = new PendingDecision(0, DecisionKind.ChooseNudge, "", Dice: [2, 3]);
        var choice = await new HardBot(1).DecideAsync(engine.GetView(0), decision, [new Nudge(-1), new Nudge(0), new Nudge(1)]);
        Assert.Equal(new Nudge(0), choice);
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
