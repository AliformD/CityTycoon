using System.Reflection;
using CityTycoon.Engine;
using CityTycoon.Engine.Actions;
using CityTycoon.Engine.Content;
using CityTycoon.Engine.Hosting;
using CityTycoon.Engine.State;

namespace CityTycoon.AI.Tests;

public class SmartBotTests
{
    private static GameEngine NewEngine(int players = 2, ulong seed = 1) =>
        GameEngine.NewGame(new GameSetup { Players = Enumerable.Range(0, players).Select(i => new PlayerSetup($"P{i}")).ToList(), Seed = seed });

    private static async Task<GameEngine> PlayAsync(ulong seed, Func<ulong, int, IPlayerController> makeBot)
    {
        var engine = GameEngine.NewGame(new GameSetup
        {
            Players = Enumerable.Range(0, 4).Select(i => new PlayerSetup($"Bot {i}")).ToList(),
            Seed = seed,
        });
        var bots = engine.State.Players.ToDictionary(p => p.Id, p => makeBot(seed * 7 + (ulong)p.Id, p.Id));
        await new GameHost(engine, bots).RunAsync();
        return engine;
    }

    [Fact]
    public async Task NormalAndHardBots_FinishGamesWithoutErrors()
    {
        for (ulong seed = 1; seed <= 20; seed++)
        {
            Assert.True((await PlayAsync(seed, (s, _) => new NormalBot(s))).State.IsOver);
            Assert.True((await PlayAsync(seed, (s, _) => new HardBot(s))).State.IsOver);
            Assert.True((await PlayAsync(seed, (s, id) => id % 2 == 0 ? new HardBot(s) : new EasyBot(s))).State.IsOver);
        }
    }

    [Fact]
    public async Task SameSeeds_GiveTheSameHardBotGame()
    {
        var a = await PlayAsync(9, (s, _) => new HardBot(s));
        var b = await PlayAsync(9, (s, _) => new HardBot(s));
        Assert.Equal(a.State.Log.Count, b.State.Log.Count);
        Assert.Equal(a.State.Result!.WinnerIds, b.State.Result!.WinnerIds);
    }

    /// <summary>Section 23.1: bots are handed a PlayerView only. No bot type may hold or take the full game state.</summary>
    [Fact]
    public void Bots_NeverTouchTheFullGameState()
    {
        var forbidden = new[] { typeof(GameState), typeof(GameEngine) };
        var flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        foreach (var type in new[] { typeof(EasyBot), typeof(NormalBot), typeof(HardBot) })
        {
            var used = type.GetFields(flags).Select(f => f.FieldType)
                .Concat(type.GetProperties(flags).Select(p => p.PropertyType))
                .Concat(type.GetMethods(flags).SelectMany(m => m.GetParameters().Select(p => p.ParameterType).Append(m.ReturnType)));
            Assert.DoesNotContain(used, t => forbidden.Contains(t));
        }
    }

    /// <summary>A bid that would finish the bot's color group is worth more than a plain one.</summary>
    [Fact]
    public async Task NormalBot_PaysMoreForTheLastPieceOfAColorGroup()
    {
        var content = GameContent.Default;
        string group = content.Property("maple-avenue").Group!;
        var members = content.GroupMembers(group).ToList();
        string last = members[^1];
        var decision = new PendingDecision(0, DecisionKind.SubmitBid, "", last, content.Property(last).AssessedValue / 2, 5000, 10);

        var engine = NewEngine();
        var plain = engine.GetView(0);
        foreach (string id in members.Take(members.Count - 1))
            engine.State.Properties[id].OwnerId = 0;
        var finishing = engine.GetView(0);

        async Task<int> HighestBid(PlayerViewHolder holder)
        {
            int best = 0;
            for (ulong seed = 1; seed <= 30; seed++)
            {
                var action = await new NormalBot(seed).DecideAsync(holder.View, decision with { MaxBid = 5000 }, [new PassBid(), new PlaceBid(decision.MinBid)]);
                if (action is PlaceBid bid) best = Math.Max(best, bid.Amount);
            }
            return best;
        }

        Assert.True(await HighestBid(new(finishing)) > await HighestBid(new(plain)));
    }

    private sealed record PlayerViewHolder(CityTycoon.Engine.Views.PlayerView View);

    [Fact]
    public async Task HardBot_NeverBidsMoreThanItsCash()
    {
        var engine = NewEngine();
        var view = engine.GetView(0);
        var decision = new PendingDecision(0, DecisionKind.SubmitBid, "", "platinum-row", 200, 1500, 10);
        for (ulong seed = 1; seed <= 20; seed++)
        {
            var action = await new HardBot(seed).DecideAsync(view, decision, [new PassBid(), new PlaceBid(200)]);
            if (action is PlaceBid bid)
                Assert.True(bid.Amount <= view.Me.Cash);
        }
    }
}
