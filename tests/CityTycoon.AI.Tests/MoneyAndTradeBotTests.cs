using CityTycoon.Engine;
using CityTycoon.Engine.Actions;
using CityTycoon.Engine.Hosting;
using CityTycoon.Engine.Views;

namespace CityTycoon.AI.Tests;

/// <summary>Phase 3 bot behaviour: trades, raising money and building.</summary>
public class MoneyAndTradeBotTests
{
    private static GameEngine NewGame() => GameEngine.NewGame(new GameSetup
    {
        Players = [new("Human"), new("Bot")],
        Seed = 3,
        ShuffleTurnOrder = false,
    });

    /// <summary>A stand-in for a person: always gives the answers it was handed, in order.</summary>
    private sealed class ScriptedController(params GameAction[] answers) : IPlayerController
    {
        private readonly Queue<GameAction> queue = new(answers);

        public Task<GameAction> DecideAsync(PlayerView view, PendingDecision decision, IReadOnlyList<GameAction> legalActions) =>
            Task.FromResult(queue.Dequeue());
    }

    [Fact]
    public async Task APersonCanTradeWithABot_ThroughTheGameHost()
    {
        var engine = NewGame();
        engine.State.Properties["market-street"].OwnerId = 1;
        // A generous offer: $400 for a $180 property.
        var offer = new TradeOffer(0, 1, 400, 0, [], ["market-street"]);
        var host = new GameHost(engine, new Dictionary<int, IPlayerController>
        {
            [0] = new ScriptedController(new ProposeTrade(offer)),
            [1] = new EasyBot(1),
        });

        await host.StepAsync(); // the person offers
        await host.StepAsync(); // the bot answers

        Assert.Equal(0, engine.State.Properties["market-street"].OwnerId);
        Assert.Equal(1100, engine.State.Player(0).Cash);
    }

    [Fact]
    public async Task EasyBot_DeclinesATradeThatCompletesSomeoneElsesGroup()
    {
        var engine = NewGame();
        engine.State.Properties["mill-street"].OwnerId = 0;
        engine.State.Properties["cobble-lane"].OwnerId = 1;
        var offer = new TradeOffer(0, 1, 70, 0, [], ["cobble-lane"]);
        Assert.True(engine.Submit(0, new ProposeTrade(offer)).Accepted);

        var decision = engine.GetPendingDecisions().Single();
        var choice = await new EasyBot(1).DecideAsync(engine.GetView(1), decision, engine.GetLegalActions(1));

        Assert.Equal(new DeclineTrade(), choice);
    }

    [Fact]
    public async Task EasyBot_RaisesMoney_ByMortgagingBeforeSellingOrBorrowing()
    {
        var engine = NewGame();
        engine.State.Properties["market-street"].OwnerId = 1;
        var decision = new PendingDecision(1, DecisionKind.RaiseMoney, "", AmountOwed: 300, Management: ManagementOptions.RaiseMoney);
        GameAction[] legal = [new TakeLoan(), new Mortgage("market-street")];

        var choice = await new EasyBot(1).DecideAsync(engine.GetView(1), decision, legal);

        Assert.Equal(new Mortgage("market-street"), choice);
    }

    [Fact]
    public async Task EasyBot_RaisesMoney_InsteadOfForfeitingAPendingProperty()
    {
        var engine = NewGame();
        var decision = new PendingDecision(1, DecisionKind.RaiseMoney, "", AmountOwed: 300, Management: ManagementOptions.RaiseMoney);
        GameAction[] legal = [new ForfeitProperty(), new TakeLoan()];

        var choice = await new EasyBot(1).DecideAsync(engine.GetView(1), decision, legal);

        Assert.Equal(new TakeLoan(), choice);
    }

    [Fact]
    public async Task EasyBot_BuildsWhenItCanAffordTo()
    {
        var engine = NewGame();
        engine.State.Properties["mill-street"].OwnerId = 0;
        engine.State.Properties["cobble-lane"].OwnerId = 0;
        var decision = engine.GetPendingDecisions().Single();

        var choice = await new EasyBot(1).DecideAsync(engine.GetView(0), decision, engine.GetLegalActions(0));

        Assert.IsType<Build>(choice);
    }

    [Fact]
    public async Task EasyBot_UsesADownPayment_WhenShortOfCash()
    {
        var bot = new EasyBot(2);
        var engine = NewGame();
        engine.State.Player(0).Cash = bot.SafetyReserve + 60;
        var view = engine.GetView(0);
        var decision = new PendingDecision(0, DecisionKind.SubmitBid, "", "market-street",
            MinBid: 90, MaxBid: view.Me.Cash / 10 * 10, BidIncrement: 10, MaxDownPaymentBid: 500);

        var choice = await bot.DecideAsync(view, decision, [new PassBid(), new PlaceBid(90)]);

        Assert.Equal(new PlaceBid(90, DownPayment: true), choice);
    }
}
