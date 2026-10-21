using CityTycoon.Engine;
using CityTycoon.Engine.Actions;
using CityTycoon.Engine.Hosting;

namespace CityTycoon.AI.Tests;

/// <summary>
/// Bots bid to stop one opponent finishing a color group (docs/DECISIONS.md #135).
/// The table from Angel's game: Cal (player 3) owns Summit Avenue and Crown Street, and the
/// last green, Grand Plaza ($300), is in an open auction. Dee (player 2) has plenty of cash.
/// </summary>
public class BlockingBidTests
{
    private const string Last = "grand-plaza";

    private static GameEngine GreenTable(int rivalCash = 1500)
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
    /// </summary>
    [Fact]
    public async Task EveryBot_KeepsBidding_ToStopARivalFinishingTheGreens()
    {
        var engine = GreenTable();
        foreach (var bot in Bots(s => new EasyBot(s)).Concat(Bots(s => new NormalBot(s))).Concat(Bots(s => new HardBot(s))))
            Assert.True(await KeepsBidding(bot, engine, 360), bot.GetType().Name);
    }

    [Fact]
    public async Task WithNoRival_EasyBots_StopAtTheirOwnPrice()
    {
        var engine = GreenTable();
        engine.State.Properties["crown-street"].OwnerId = null;   // Cal would only have two of three after winning
        foreach (var bot in Bots(s => new EasyBot(s)))
            Assert.False(await KeepsBidding(bot, engine, 400));
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
            Assert.False(await KeepsBidding(bot, engine, 400));
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
}
