using CityTycoon.AI;
using CityTycoon.Engine;
using CityTycoon.Engine.Actions;
using CityTycoon.Engine.Hosting;

namespace CityTycoon.AI.Tests;

/// <summary>
/// Normal and Hard bots judge a trade that hands over a color group by what the
/// group can earn: the owner's cash to build, time left and ordinances.
/// </summary>
public class TradeJudgementBotTests
{
    /// <summary>The person owns two of Orange; the bot owns Market Street, the third.</summary>
    private static GameEngine OrangeAlmostDone(int personCash)
    {
        var engine = GameEngine.NewGame(new GameSetup { Players = [new("Human"), new("Bot")], Seed = 3, ShuffleTurnOrder = false });
        engine.State.Properties["baker-street"].OwnerId = 0;
        engine.State.Properties["fountain-square"].OwnerId = 0;
        engine.State.Properties["market-street"].OwnerId = 1;
        engine.State.Player(0).Cash = personCash;
        return engine;
    }

    private static async Task<GameAction> Answer(IPlayerController bot, GameEngine engine, TradeOffer offer, int countersLeft = 0)
    {
        var decision = new PendingDecision(1, DecisionKind.RespondToTrade, "", Trade: offer, CountersLeft: countersLeft);
        return await bot.DecideAsync(engine.GetView(1), decision, [new AcceptTrade(), new DeclineTrade()]);
    }

    private static readonly TradeOffer ThreeHundredForMarket = new(0, 1, 300, 0, [], ["market-street"]);

    [Fact]
    public async Task HardBot_SellsTheLastPiece_ToSomeoneWithNoCashLeftToBuild()
    {
        var engine = OrangeAlmostDone(personCash: 350);
        Assert.Equal(new AcceptTrade(), await Answer(new HardBot(1), engine, ThreeHundredForMarket));
    }

    [Fact]
    public async Task HardBot_WontSellTheSamePiece_ToSomeoneRichEnoughToBuildRightAway()
    {
        var engine = OrangeAlmostDone(personCash: 1500);
        Assert.IsType<DeclineTrade>(await Answer(new HardBot(1), engine, ThreeHundredForMarket));
    }

    [Fact]
    public async Task HardBot_CountersARichBuyer_InsteadOfRefusingOutright()
    {
        var engine = OrangeAlmostDone(personCash: 1500);
        var choice = await Answer(new HardBot(1), engine, ThreeHundredForMarket, countersLeft: 2);

        var counter = Assert.IsType<CounterTrade>(choice).Offer;
        Assert.Equal(["market-street"], counter.GiveProperties);
        Assert.True(counter.GetCash > 300);
        Assert.True(engine.Submit(0, new ProposeTrade(ThreeHundredForMarket)).Accepted);
        Assert.True(engine.Submit(1, choice).Accepted);
    }

    [Fact]
    public async Task HardBot_ExpectsLessForTheGroup_DuringABuildingMoratorium()
    {
        var rich = OrangeAlmostDone(personCash: 1500);
        var counterNormally = Assert.IsType<CounterTrade>(await Answer(new HardBot(1), rich, ThreeHundredForMarket, 2)).Offer;

        var frozen = OrangeAlmostDone(personCash: 1500);
        frozen.State.ActiveOrdinances.Add(new("building-moratorium", null, 2));
        var counterFrozen = Assert.IsType<CounterTrade>(await Answer(new HardBot(1), frozen, ThreeHundredForMarket, 2)).Offer;

        Assert.True(counterFrozen.GetCash < counterNormally.GetCash);
    }

    [Fact]
    public async Task EasyBot_StillUsesTheSimpleRule_AndWontCounterAGroupSale()
    {
        var engine = OrangeAlmostDone(personCash: 1500);
        Assert.IsType<DeclineTrade>(await Answer(new EasyBot(1), engine, ThreeHundredForMarket, countersLeft: 2));
    }

    [Fact]
    public async Task HardBot_SwapsForAGroupOfItsOwn_WhenItEndsUpAhead()
    {
        // A swap that gives each player a group: the bot gets Pink, the person gets Orange.
        var engine = OrangeAlmostDone(personCash: 200);
        engine.State.Properties["gallery-row"].OwnerId = 1;
        engine.State.Properties["theater-way"].OwnerId = 1;
        engine.State.Properties["studio-street"].OwnerId = 0;
        var swap = new TradeOffer(0, 1, 0, 0, ["studio-street"], ["market-street"]);

        // The bot has the cash to build on its group right away; the person doesn't.

        Assert.Equal(new AcceptTrade(), await Answer(new HardBot(1), engine, swap));
        // The other way round, the person would build first, so it's a bad swap for the bot.
        engine.State.Player(0).Cash = 3000;
        engine.State.Player(1).Cash = 200;
        Assert.NotEqual(new AcceptTrade(), await Answer(new HardBot(1), engine, swap));
    }

    [Fact]
    public async Task ABotValuesAGetOutOfJailFreeCard_MoreWhenItIsInJail()
    {
        var engine = OrangeAlmostDone(personCash: 1500);
        engine.State.Properties["baker-street"].OwnerId = null;
        // $120 plus the card for Market Street (assessed $180): not quite enough on a normal day.
        var offer = new TradeOffer(0, 1, 120, 0, [], ["market-street"]) { GiveCards = ["get-out-of-jail-free"] };
        Assert.IsType<DeclineTrade>(await Answer(new HardBot(1), engine, offer));

        engine.State.Player(1).InJail = true;
        Assert.Equal(new AcceptTrade(), await Answer(new HardBot(1), engine, offer));
    }
}
