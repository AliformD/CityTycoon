using System.Text.Json;
using CityTycoon.Engine;
using CityTycoon.Engine.Content;
using CityTycoon.Engine.Hosting;
using CityTycoon.Engine.State;

namespace CityTycoon.AI.Tests;

public class RandomBotGameTests
{
    private static async Task<GameEngine> PlayAsync(ulong seed, int players, GameMode mode = GameMode.Timed)
    {
        var engine = GameEngine.NewGame(new GameSetup
        {
            Players = Enumerable.Range(0, players).Select(i => new PlayerSetup($"Bot {i}")).ToList(),
            Seed = seed,
            Mode = mode,
            MaxRounds = 30,
        });
        var bots = engine.State.Players.ToDictionary(p => p.Id, p => (IPlayerController)new RandomBot(seed * 31 + (ulong)p.Id));
        await new GameHost(engine, bots).RunAsync();
        return engine;
    }

    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(6)]
    public async Task ManyRandomGames_FinishWithoutErrors(int players)
    {
        for (ulong seed = 1; seed <= 40; seed++)
        {
            var engine = await PlayAsync(seed, players);
            Assert.True(engine.State.IsOver);
            Assert.NotEmpty(engine.State.Result!.WinnerIds);
        }
    }

    [Fact]
    public async Task SameSeed_GivesAnIdenticalEventLog()
    {
        // Determinism (Section 20.5): same seed + same choices = same game.
        string first = JsonSerializer.Serialize((await PlayAsync(2024, 4)).State.Log, ContentLoader.JsonOptions);
        string second = JsonSerializer.Serialize((await PlayAsync(2024, 4)).State.Log, ContentLoader.JsonOptions);
        Assert.Equal(first, second);
    }

    [Fact]
    public async Task DifferentSeeds_GiveDifferentGames()
    {
        string first = JsonSerializer.Serialize((await PlayAsync(1, 4)).State.Log, ContentLoader.JsonOptions);
        string second = JsonSerializer.Serialize((await PlayAsync(2, 4)).State.Log, ContentLoader.JsonOptions);
        Assert.NotEqual(first, second);
    }

    [Fact]
    public async Task AGameSavedPartWay_LoadsBackExactly()
    {
        var engine = GameEngine.NewGame(new GameSetup
        {
            Players = [new("A"), new("B"), new("C")],
            Seed = 77,
        });
        var bots = engine.State.Players.ToDictionary(p => p.Id, p => (IPlayerController)new RandomBot((ulong)p.Id));
        var host = new GameHost(engine, bots);
        for (int i = 0; i < 200; i++)
            await host.StepAsync();

        var copy = new GameEngine(GameStateSerializer.FromJson(GameStateSerializer.ToJson(engine.State)), GameContent.Default);
        Assert.Equal(GameStateSerializer.ToJson(engine.State), GameStateSerializer.ToJson(copy.State));
    }
}
