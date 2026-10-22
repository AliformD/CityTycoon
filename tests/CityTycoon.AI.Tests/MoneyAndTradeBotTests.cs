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
        Assert.Equal(1200 - 400, engine.State.Player(0).Cash);
    }

    [Fact]
    public async Task EasyBot_WantsMore_ForAPieceThatCompletesSomeoneElsesGroup()
    {
        var engine = NewGame();
        engine.State.Properties["mill-street"].OwnerId = 0;
        engine.State.Properties["cobble-lane"].OwnerId = 1;
        var offer = new TradeOffer(0, 1, 70, 0, [], ["cobble-lane"]);
        Assert.True(engine.Submit(0, new ProposeTrade(offer)).Accepted);

        var decision = engine.GetPendingDecisions().Single();
        var choice = await new EasyBot(1).DecideAsync(engine.GetView(1), decision, engine.GetLegalActions(1));

        // $70 for a $60 property is not enough when it finishes Brown for them: it asks for more.
        Assert.True(Assert.IsType<CounterTrade>(choice).Offer.GetCash > 70);
    }

    [Fact]
    public async Task EasyBot_CountsTheCostOfAMortgage_WhenOfferedAMortgagedProperty()
    {
        var engine = NewGame();
        engine.State.Properties["market-street"].OwnerId = 0; // assessed $180, mortgage $90
        engine.State.Properties["market-street"].IsMortgaged = true;
        // $85 for it: less than the $90 mortgage, but the bot pays a $9 fee now and $99 to lift it later.
        Assert.True(engine.Submit(0, new ProposeTrade(new TradeOffer(0, 1, 0, 85, ["market-street"], []))).Accepted);

        var decision = engine.GetPendingDecisions().Single();
        var choice = await new EasyBot(1).DecideAsync(engine.GetView(1), decision, engine.GetLegalActions(1));

        Assert.IsNotType<AcceptTrade>(choice);
    }

    [Fact]
    public async Task EasyBot_CountersALowOffer_WithOneItWouldTakeItself()
    {
        var engine = NewGame();
        engine.State.Properties["market-street"].OwnerId = 1; // assessed $180
        Assert.True(engine.Submit(0, new ProposeTrade(new TradeOffer(0, 1, 50, 0, [], ["market-street"]))).Accepted);

        var decision = engine.GetPendingDecisions().Single();
        var choice = await new EasyBot(1).DecideAsync(engine.GetView(1), decision, engine.GetLegalActions(1));

        // It asks for more than the property is worth to it, and the engine takes the counter.
        var counter = Assert.IsType<CounterTrade>(choice).Offer;
        Assert.Equal((1, 0), (counter.FromId, counter.ToId));
        Assert.Equal(["market-street"], counter.GiveProperties);
        Assert.True(counter.GetCash > 180);
        Assert.True(engine.Submit(1, choice).Accepted);
        Assert.Equal(0, engine.GetPendingDecisions().Single().PlayerId);
    }

    [Fact]
    public async Task EasyBot_Declines_WhenNoCountersAreLeft()
    {
        var engine = NewGame();
        engine.State.Properties["market-street"].OwnerId = 1;
        var offer = new TradeOffer(0, 1, 50, 0, [], ["market-street"]);
        var decision = new PendingDecision(1, DecisionKind.RespondToTrade, "", Trade: offer, CountersLeft: 0);

        var choice = await new EasyBot(1).DecideAsync(engine.GetView(1), decision, [new AcceptTrade(), new DeclineTrade()]);

        Assert.IsType<DeclineTrade>(choice);
    }

    [Fact]
    public async Task EasyBot_HagglesOverItsOwnOffer_AndAcceptsAFairCounter()
    {
        var engine = NewGame();
        // The bot owns half of Brown and wants the other half.
        engine.State.Properties["mill-street"].OwnerId = 1;
        engine.State.Properties["cobble-lane"].OwnerId = 0;
        // The bot offered for Cobble Lane; the person counters asking far too much.
        var bot = new EasyBot(1);
        var tooMuch = new TradeOffer(0, 1, 0, 400, ["cobble-lane"], []);
        var decision = new PendingDecision(1, DecisionKind.RespondToTrade, "", Trade: tooMuch, CountersLeft: 2);
        var legal = new GameAction[] { new AcceptTrade(), new DeclineTrade() };

        // $400 for a $60 property is too much, so it offers less instead.
        var counter = Assert.IsType<CounterTrade>(await bot.DecideAsync(engine.GetView(1), decision, legal)).Offer;
        Assert.Equal(["cobble-lane"], counter.GetProperties);
        Assert.InRange(counter.GiveCash, 61, 399);

        // A price just under what it offered is accepted.
        var fair = new TradeOffer(0, 1, 0, counter.GiveCash - 10, ["cobble-lane"], []);
        var answer = await bot.DecideAsync(engine.GetView(1), decision with { Trade = fair, CountersLeft = 0 }, legal);
        Assert.Equal(new AcceptTrade(), answer);
    }

    [Fact]
    public async Task APersonCanCounterABotsOffer_ThroughTheGameHost()
    {
        var engine = NewGame();
        engine.State.Properties["market-street"].OwnerId = 0;
        // The person asks too much for their property, the bot counters, then the person asks less and the bot accepts.
        var host = new GameHost(engine, new Dictionary<int, IPlayerController>
        {
            [0] = new ScriptedController(new ProposeTrade(new TradeOffer(0, 1, 0, 400, ["market-street"], [])), new CounterTrade(new TradeOffer(0, 1, 0, 150, ["market-street"], []))),
            [1] = new EasyBot(1),
        });

        await host.StepAsync(); // the person offers $400
        await host.StepAsync(); // the bot counters with less
        Assert.Equal(0, engine.GetPendingDecisions().Single().PlayerId);
        await host.StepAsync(); // the person counters at $150, enough below its $180 value
        await host.StepAsync(); // the bot accepts

        Assert.Equal(1, engine.State.Properties["market-street"].OwnerId);
        Assert.Equal(1200 + 150, engine.State.Player(0).Cash);
    }

    [Fact]
    public async Task EasyBot_RaisesMoney_ByMortgagingBeforeSellingOrBorrowing()
    {
        var engine = NewGame();
        engine.State.Properties["market-street"].OwnerId = 1;
        var decision = new PendingDecision(1, DecisionKind.RaiseMoney, "", AmountOwed: 300, Management: ManagementOptions.RaiseMoney);
        GameAction[] legal = [new TakeLoan(200), new Mortgage("market-street")];

        var choice = await new EasyBot(1).DecideAsync(engine.GetView(1), decision, legal);

        Assert.Equal(new Mortgage("market-street"), choice);
    }

    [Fact]
    public async Task EasyBot_RaisesMoney_InsteadOfForfeitingAPendingProperty()
    {
        var engine = NewGame();
        var decision = new PendingDecision(1, DecisionKind.RaiseMoney, "", AmountOwed: 300, Management: ManagementOptions.RaiseMoney);
        GameAction[] legal = [new ForfeitProperty(), new TakeLoan(200)];

        var choice = await new EasyBot(1).DecideAsync(engine.GetView(1), decision, legal);

        Assert.Equal(new TakeLoan(200), choice);
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
