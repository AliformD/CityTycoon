using CityTycoon.Engine;
using CityTycoon.Engine.Actions;

namespace CityTycoon.AI.Tests;

/// <summary>Decision #157: no accepting past the limit, splitting the difference, swaps, and the Lawyer's spare jail card.</summary>
public class TradeFixesBotTests
{
    private static GameEngine NewGame(int players = 2) => GameEngine.NewGame(new GameSetup
    {
        Players = Enumerable.Range(0, players).Select(i => new PlayerSetup($"P{i}")).ToList(),
        Seed = 3,
        ShuffleTurnOrder = false,
    });

    private static async Task<GameAction> Answer(GameEngine engine, TradeOffer offer, int countersLeft)
    {
        Assert.True(engine.Submit(offer.FromId, new ProposeTrade(offer)).Accepted);
        var decision = engine.GetPendingDecisions().Single() with { CountersLeft = countersLeft };
        return await new HardBot(1).DecideAsync(engine.GetView(1), decision, engine.GetLegalActions(1));
    }

    [Fact]
    public async Task ABot_DoesntAccept_AnOfferPastItsLimit()
    {
        // $175 for Market Street (worth $180): $5 better off, but its margin is $18.
        var engine = NewGame();
        engine.State.Properties["market-street"].OwnerId = 0;
        var choice = await Answer(engine, new TradeOffer(0, 1, 0, 175, ["market-street"], []), countersLeft: 2);
        Assert.IsNotType<AcceptTrade>(choice);
    }

    [Fact]
    public async Task WhenClose_ABotSplitsTheDifference()
    {
        var engine = NewGame();
        engine.State.Properties["market-street"].OwnerId = 0;
        var choice = await Answer(engine, new TradeOffer(0, 1, 0, 175, ["market-street"], []), countersLeft: 2);

        var counter = Assert.IsType<CounterTrade>(choice);
        Assert.Contains("split the difference", counter.Why);
        Assert.InRange(counter.Offer.GiveCash, 160, 170); // between its limit and the $175 asked
    }

    [Fact]
    public async Task OnTheLastWord_ABotTakesACloseDeal_ThatStillHelpsIt()
    {
        var engine = NewGame();
        engine.State.Properties["market-street"].OwnerId = 0;
        var choice = await Answer(engine, new TradeOffer(0, 1, 0, 175, ["market-street"], []), countersLeft: 0);
        Assert.IsType<AcceptTrade>(choice);
    }

    [Fact]
    public async Task Bots_OfferASwap_ThatFinishesASetForEachOfThem()
    {
        var engine = NewGame();
        foreach (string id in new[] { "market-street", "baker-street", "pier-road" })
            engine.State.Properties[id].OwnerId = 1;
        foreach (string id in new[] { "anchor-street", "lighthouse-lane", "fountain-square" })
            engine.State.Properties[id].OwnerId = 0;
        var decision = new PendingDecision(1, DecisionKind.ChooseMoveMethod, "", Management: ManagementOptions.Trade);

        var choice = await new HardBot(1).DecideAsync(engine.GetView(1), decision, [new RollDice()]);

        var offer = Assert.IsType<ProposeTrade>(choice).Offer;
        Assert.Equal(["pier-road"], offer.GiveProperties);
        Assert.Equal(["fountain-square"], offer.GetProperties);
    }

    [Fact]
    public async Task TheLawyer_KeepsItsJailCard_WhenBailIsFree()
    {
        var engine = NewGame();
        engine.State.Player(0).CharacterId = "lawyer";
        var decision = new PendingDecision(0, DecisionKind.JailChoice, "");
        GameAction[] legal = [new RollForDoubles(), new PayBail(), new PlayCard("get-out-of-jail-free")];

        Assert.Equal(new PayBail(), await new EasyBot(1).DecideAsync(engine.GetView(0), decision, legal));
    }

    [Fact]
    public async Task TheLawyer_SellsItsSpareJailCard_ToSomeoneInJail()
    {
        var engine = NewGame(3);
        engine.State.Player(0).CharacterId = "lawyer"; // player 0 is the one whose turn it is
        engine.State.Player(0).Hand.Add("get-out-of-jail-free");
        engine.State.Player(2).InJail = true;
        var decision = new PendingDecision(0, DecisionKind.ChooseMoveMethod, "", Management: ManagementOptions.Trade);

        var offer = Assert.IsType<ProposeTrade>(await new HardBot(1).DecideAsync(engine.GetView(0), decision, [new RollDice()])).Offer;

        Assert.Equal(2, offer.ToId);
        Assert.Equal(["get-out-of-jail-free"], offer.GiveCards);
        Assert.True(offer.GetCash > 0);

        // And the jailed bot thinks it's a good deal.
        Assert.True(engine.Submit(0, new ProposeTrade(offer)).Accepted);
        var answer = await new HardBot(2).DecideAsync(engine.GetView(2), engine.GetPendingDecisions().Single(), engine.GetLegalActions(2));
        Assert.IsType<AcceptTrade>(answer);
    }
}
