using CityTycoon.Engine;
using CityTycoon.Engine.Actions;
using CityTycoon.Engine.Hosting;

namespace CityTycoon.AI.Tests;

/// <summary>
/// Bots bid to stop one opponent finishing a color group (docs/DECISIONS.md #135), and much harder
/// while nobody holds a whole group yet (decision #162).
/// The table from Angel's game: Cal (player 3) owns Summit Avenue and Crown Street, and the
/// last green, Grand Plaza ($300), is in an open auction. Dee (player 2) has plenty of cash.
/// Unless a test says otherwise, player 1 already holds both browns, so this would not be the first group.
/// </summary>
public class BlockingBidTests
{
    private const string Last = "grand-plaza";

    private static GameEngine GreenTable(int rivalCash = 1500, bool firstGroup = false)
    {
        var engine = GameEngine.NewGame(new GameSetup
        {
            Players = Enumerable.Range(0, 4).Select(i => new PlayerSetup($"P{i}")).ToList(),
            Seed = 1,
        });
        engine.State.Player(2).Cash = 1306;
        engine.State.Player(3).Cash = rivalCash;
        engine.State.Properties["summit-avenue"].OwnerId = 3;
        engine.State.Properties["crown-street"].OwnerId = 3;
        if (!firstGroup)
        {
            engine.State.Properties["cobble-lane"].OwnerId = 1;
            engine.State.Properties["mill-street"].OwnerId = 1;
        }
        return engine;
    }

    /// <summary>An open-auction question for Dee: the rival leads, and Dee must raise to at least this much.</summary>
    private static PendingDecision Raise(int to) =>
        new(2, DecisionKind.SubmitBid, "", Last, to, 1306, 10, MaxDownPaymentBid: 0, OpenAuction: true, HighBidderId: 3, HighBid: to - 10);

    private static async Task<bool> KeepsBidding(IPlayerController bot, GameEngine engine, int to) =>
        await bot.DecideAsync(engine.GetView(2), Raise(to), [new PassBid(), new PlaceBid(to)]) is PlaceBid;

    private static IEnumerable<IPlayerController> Bots(Func<ulong, IPlayerController> make) =>
        Enumerable.Range(1, 40).Select(s => make((ulong)s));

    /// <summary>
    /// Angel's game: at $360 most Easy bots (their own limit is 90% to 130% of $300) used to drop out
    /// and hand the rival the set. Now every Easy, Normal and Hard bot with spare cash keeps going.
    /// A Reserved bot (decision #144) pays 5% less for everything, so it gives up a little sooner, at $340.
    /// </summary>
    [Fact]
    public async Task EveryBot_KeepsBidding_ToStopARivalFinishingTheGreens()
    {
        var engine = GreenTable();
        foreach (var bot in Bots(s => new EasyBot(s)).Concat(Bots(s => new NormalBot(s))).Concat(Bots(s => new HardBot(s))))
        {
            int price = ((EasyBot)bot).Personality == BotPersonality.Reserved ? 340 : 360;
            Assert.True(await KeepsBidding(bot, engine, price), $"{bot.GetType().Name} {((EasyBot)bot).Personality}");
        }
    }

    [Fact]
    public async Task WithNoRival_EasyBots_StopAtTheirOwnPrice()
    {
        var engine = GreenTable();
        engine.State.Properties["crown-street"].OwnerId = null;   // Cal would only have two of three after winning
        // Even an Aggressive Easy bot stops by 130% x 1.06, about $413.
        foreach (var bot in Bots(s => new EasyBot(s)))
            Assert.False(await KeepsBidding(bot, engine, 440));
    }

    /// <summary>A human can't bait a bot into paying any price: blocking stops at 150% of the assessed value.</summary>
    [Fact]
    public async Task BlockingBids_StopAtOneAndAHalfTimesTheValue()
    {
        var engine = GreenTable();
        foreach (var bot in Bots(s => new EasyBot(s)).Concat(Bots(s => new NormalBot(s))))
            Assert.False(await KeepsBidding(bot, engine, 460));
    }

    /// <summary>
    /// If the rival can't really pay (no cash to spare, nothing to mortgage, no loans left), a bid
    /// they win falls back to the bank at GO anyway, so there's no point paying extra to stop them.
    /// </summary>
    [Fact]
    public async Task ARivalWhoCannotPay_IsNotWorthOverpayingToBlock()
    {
        var engine = GreenTable(rivalCash: 60);
        engine.State.Player(3).Loans = 2;
        engine.State.Properties["summit-avenue"].IsMortgaged = true;
        engine.State.Properties["crown-street"].IsMortgaged = true;
        foreach (var bot in Bots(s => new EasyBot(s)))
            Assert.False(await KeepsBidding(bot, engine, 440));
    }

    /// <summary>Blocking never pushes a bot into its cash reserve.</summary>
    [Fact]
    public async Task BlockingBids_StillKeepTheCashReserve()
    {
        var engine = GreenTable();
        engine.State.Player(2).Cash = 420;
        foreach (var bot in Bots(s => new EasyBot(s)))
        {
            var easy = (EasyBot)bot;
            var decision = Raise(400) with { MaxBid = 420 };
            if (await bot.DecideAsync(engine.GetView(2), decision, [new PassBid(), new PlaceBid(400)]) is PlaceBid { DownPayment: false } bid)
                Assert.True(bid.Amount <= 420 - easy.SafetyReserve);
        }
    }

    // ----- The first group on the board (decision #162) -----

    /// <summary>Before anyone holds a group, every bot fights far past 150% to stop the first one.</summary>
    [Fact]
    public async Task TheFirstGroup_EveryBotFightsFarHarder()
    {
        var engine = GreenTable(firstGroup: true);
        foreach (var bot in Bots(s => new EasyBot(s)).Concat(Bots(s => new NormalBot(s))).Concat(Bots(s => new HardBot(s))))
            Assert.True(await KeepsBidding(bot, engine, 700), $"{bot.GetType().Name} {((EasyBot)bot).Personality}");
    }

    /// <summary>
    /// Open auctions are pay in full, so the rival can only bid their cash and loans. One step past that is enough:
    /// Cal has $430 and no loans left, so Easy bots (their own price tops out near $413) stay in at $440 and stop at $450.
    /// </summary>
    [Fact]
    public async Task TheFirstGroup_BlockingStopsAtWhatTheRivalCanReallyBid()
    {
        var engine = GreenTable(rivalCash: 430, firstGroup: true);
        engine.State.Player(3).Loans = 2;
        foreach (var bot in Bots(s => new EasyBot(s)))
        {
            Assert.True(await KeepsBidding(bot, engine, 440));
            Assert.False(await KeepsBidding(bot, engine, 450));
        }
    }

    /// <summary>Before the first group, bots also push back on the second of three: Cal owns one green and the third is unsold.</summary>
    [Fact]
    public async Task TheFirstGroup_BotsAlsoFightForTheSecondOfThree()
    {
        var engine = GreenTable(firstGroup: true);
        engine.State.Properties["crown-street"].OwnerId = null;
        foreach (var bot in Bots(s => new EasyBot(s)).Concat(Bots(s => new HardBot(s))))
            Assert.True(await KeepsBidding(bot, engine, 500));
        // Even then, never past twice the value.
        foreach (var bot in Bots(s => new EasyBot(s)))
            Assert.False(await KeepsBidding(bot, engine, 610));
    }

    /// <summary>Short of cash, a bot borrows to stop the first group, and keeps half its reserve.</summary>
    [Fact]
    public async Task TheFirstGroup_IsWorthALoan()
    {
        var engine = GreenTable(firstGroup: true);
        engine.State.Player(2).Cash = 300;
        GameAction[] legal = [new PassBid(), new TakeLoan(100), new TakeLoan(200), new TakeLoan(400)];
        foreach (var bot in Bots(s => new EasyBot(s)).Concat(Bots(s => new HardBot(s))))
            Assert.IsType<TakeLoan>(await bot.DecideAsync(engine.GetView(2), Raise(400) with { MaxBid = 300 }, legal));
    }
}
