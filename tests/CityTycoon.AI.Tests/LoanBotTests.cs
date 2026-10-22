using CityTycoon.Engine;
using CityTycoon.Engine.Actions;

namespace CityTycoon.AI.Tests;

/// <summary>Decision #154: three loan sizes, and bots borrow when it helps.</summary>
public class LoanBotTests
{
    private static readonly string[] Green = ["summit-avenue", "grand-plaza", "crown-street"];

    private static GameEngine NewGame(int players = 2) => GameEngine.NewGame(new GameSetup
    {
        Players = Enumerable.Range(0, players).Select(i => new PlayerSetup($"P{i}")).ToList(),
        Seed = 3,
        ShuffleTurnOrder = false,
    });

    private static readonly PendingDecision OwnTurn =
        new(1, DecisionKind.PostMoveActions, "", Management: ManagementOptions.Build | ManagementOptions.TakeLoan);

    private static GameAction[] BuildOrBorrow(string propertyId) =>
        [new Build(propertyId), new TakeLoan(100), new TakeLoan(200), new TakeLoan(400), new EndTurn()];

    [Fact]
    public async Task Bots_TakeTheSmallestLoanThatCoversWhatTheyOwe()
    {
        var engine = NewGame();
        engine.State.Player(1).Cash = 150;
        var decision = new PendingDecision(1, DecisionKind.RaiseMoney, "", AmountOwed: 300, Management: ManagementOptions.RaiseMoney);
        GameAction[] legal = [new TakeLoan(100), new TakeLoan(200), new TakeLoan(400)];

        var choice = await new HardBot(1).DecideAsync(engine.GetView(1), decision, legal);

        Assert.Equal(new TakeLoan(200), choice); // $150 short: $100 isn't enough, $400 costs more interest
    }

    [Fact]
    public async Task Bots_BorrowToBuild_WhenTheRentPaysBackTheInterest()
    {
        var engine = NewGame(4);
        foreach (string id in Green)
            engine.State.Properties[id].OwnerId = 1;
        engine.State.Player(1).Cash = 150; // not enough for a $200 house

        var choice = await new HardBot(1).DecideAsync(engine.GetView(1), OwnTurn, BuildOrBorrow("crown-street"));

        Assert.IsType<TakeLoan>(choice);
    }

    [Fact]
    public async Task Bots_DontBorrowToBuild_InTheLastRound()
    {
        var engine = NewGame(4);
        foreach (string id in Green)
            engine.State.Properties[id].OwnerId = 1;
        engine.State.Player(1).Cash = 150;
        engine.State.Round = engine.State.Settings.MaxRounds;

        var choice = await new HardBot(1).DecideAsync(engine.GetView(1), OwnTurn, BuildOrBorrow("crown-street"));

        Assert.IsNotType<TakeLoan>(choice);
    }

    [Fact]
    public async Task Bots_RepayTheBiggestLoanFirst_WhenRich()
    {
        var engine = NewGame();
        engine.State.Player(1).LoanBook.AddRange([100, 400]);
        engine.State.Player(1).Cash = 3000;
        var decision = OwnTurn with { Management = ManagementOptions.RepayLoan };

        var choice = await new HardBot(1).DecideAsync(engine.GetView(1), decision, [new RepayLoan(100), new RepayLoan(400), new EndTurn()]);

        Assert.Equal(new RepayLoan(400), choice);
    }
}
