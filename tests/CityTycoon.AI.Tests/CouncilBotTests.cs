using CityTycoon.Engine;
using CityTycoon.Engine.Events;
using CityTycoon.Engine.Hosting;

namespace CityTycoon.AI.Tests;

/// <summary>Bots must be able to set the agenda, pick districts and vote, and a whole game must run with several Council sessions.</summary>
public class CouncilBotTests
{
    private static async Task<GameEngine> Play(ulong seed, Func<int, IPlayerController> bot, int rounds = 40, string[]? characters = null)
    {
        var engine = GameEngine.NewGame(new GameSetup
        {
            Players = Enumerable.Range(0, 4).Select(i => new PlayerSetup($"Bot {i}", CharacterId: characters?[i])).ToList(),
            Seed = seed,
            MaxRounds = rounds,
        });
        await new GameHost(engine, engine.State.Players.ToDictionary(p => p.Id, p => bot(p.Id))).RunAsync();
        Assert.True(engine.State.IsOver);
        return engine;
    }

    [Fact]
    public async Task EasyBots_PlayAFullGame_WithSeveralCouncilSessions()
    {
        for (ulong seed = 1; seed <= 10; seed++)
        {
            var engine = await Play(seed, id => new EasyBot(seed * 11 + (ulong)id));
            Assert.True(engine.State.Log.OfType<CouncilStarted>().Count() >= 5, $"seed {seed}");
            Assert.NotEmpty(engine.State.Log.OfType<CouncilResolved>());
        }
    }

    [Fact]
    public async Task RandomBots_VoteInEveryWay_WithoutErrors()
    {
        for (ulong seed = 1; seed <= 20; seed++)
        {
            var engine = await Play(seed, id => new RandomBot(seed * 17 + (ulong)id), rounds: 30);
            Assert.All(engine.State.Players, p => Assert.True(p.Cash >= 0 || p.IsBankrupt || p.Loans > 0));
        }
    }

    [Fact]
    public async Task HardBots_PlayFullGames_WithVoteContracts()
    {
        // Contracts are rare (about 1 in 10 games), and the first-lap rules (decision #165) make early votes
        // matter less, so look at enough games to see some.
        int bids = 0, contracts = 0;
        for (ulong seed = 1; seed <= 30; seed++)
        {
            var engine = await Play(seed, id => new HardBot(seed * 13 + (ulong)id), characters: ["politician", "lawyer", "medic", "mogul"]);
            bids += engine.State.Log.OfType<VoteContractBid>().Count();
            contracts += engine.State.Log.OfType<VoteDealMade>().Count();
        }
        Assert.True(bids > 0 && contracts > 0, $"bids {bids}, contracts {contracts}");
    }

    [Fact]
    public async Task Bots_ActuallyPassAndFailOrdinances()
    {
        int passed = 0, failed = 0;
        for (ulong seed = 1; seed <= 10; seed++)
        {
            var engine = await Play(seed, id => new EasyBot(seed * 5 + (ulong)id));
            passed += engine.State.Log.OfType<CouncilResolved>().Count(r => r.Passed);
            failed += engine.State.Log.OfType<CouncilResolved>().Count(r => !r.Passed);
        }
        Assert.True(passed > 0 && failed > 0, $"passed {passed}, failed {failed}");
    }
}
