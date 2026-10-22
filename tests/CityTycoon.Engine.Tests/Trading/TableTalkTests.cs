using CityTycoon.Engine.Actions;
using CityTycoon.Engine.Events;
using CityTycoon.Engine.Hosting;
using CityTycoon.Engine.Tests.Helpers;
using CityTycoon.Engine.Views;

namespace CityTycoon.Engine.Tests.Trading;

/// <summary>Decision #160: players can talk to the table. Words go in the log and change nothing else.</summary>
public class TableTalkTests
{
    [Fact]
    public void ARemark_GoesInTheLog_ForEveryone_AndChangesNothingElse()
    {
        var game = new TestGame(3);
        var pendingBefore = game.Engine.GetPendingDecisions().ToList();
        int cash = game.Player(1).Cash;

        Assert.True(game.Engine.Say(1, "Don't take that, A.", aboutId: 0, TalkTone.Warning).Accepted);

        Assert.Equal(new TableTalk(1, 0, "Don't take that, A.", TalkTone.Warning), game.State.Log[^1]);
        Assert.Equal(pendingBefore, game.Engine.GetPendingDecisions());
        Assert.Equal(cash, game.Player(1).Cash);
        Assert.Contains(game.Engine.GetView(2).Log, e => e is TableTalk);
        Assert.Equal("B: “Don't take that, A.”", EventDescriber.Describe(game.State.Log[^1], game.Engine.GetView(2), game.Engine.Content));
    }

    [Fact]
    public void BadRemarks_AreRefused()
    {
        var game = new TestGame(2);
        Assert.False(game.Engine.Say(0, "").Accepted);
        Assert.False(game.Engine.Say(0, new string('x', GameEngine.MaxTalkLength + 1)).Accepted);
        Assert.False(game.Engine.Say(0, "Talking to myself", aboutId: 0).Accepted);
        Assert.False(game.Engine.Say(5, "Who am I?").Accepted);
        game.Player(1).IsBankrupt = true;
        Assert.False(game.Engine.Say(1, "Still here").Accepted);
    }

    private sealed class Talker(GameAction answer) : IPlayerController, ITableTalker
    {
        public int Calls;
        public Task<GameAction> DecideAsync(PlayerView view, PendingDecision decision, IReadOnlyList<GameAction> legalActions) =>
            Task.FromResult(answer);

        public IReadOnlyList<Remark> Talk(IReadOnlyList<GameEvent> newEvents, int fromLogIndex, Func<PlayerView> view)
        {
            Calls++;
            return newEvents.OfType<TradeProposed>().Any() ? [new Remark("I saw that.", 0, TalkTone.Sassy)] : [];
        }
    }

    [Fact]
    public async Task TheHost_LetsTalkingSeatsCommentAfterEachMove()
    {
        var game = new TestGame(3);
        game.State.Properties["market-street"].OwnerId = 1;
        var onlooker = new Talker(new NoThanks());
        var host = new GameHost(game.Engine, new Dictionary<int, IPlayerController>
        {
            [0] = new Talker(new ProposeTrade(new TradeOffer(0, 1, 100, 0, [], ["market-street"]))),
            [1] = new Talker(new DeclineTrade()),
            [2] = onlooker,
        });

        await host.StepAsync();

        Assert.True(onlooker.Calls > 0);
        // Every seat is asked in seat order. Seat 0 aims its remark at itself, which the engine refuses, so seats 1 and 2 speak.
        var said = game.State.Log.OfType<TableTalk>().ToList();
        Assert.Equal([1, 2], said.Select(t => t.SpeakerId));
    }
}
