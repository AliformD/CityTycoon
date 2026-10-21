using CityTycoon.Engine;
using CityTycoon.Engine.Events;
using CityTycoon.Engine.Hosting;
using CityTycoon.Engine.State;

namespace CityTycoon.AI.Tests;

/// <summary>
/// The money ledger and property history are worked out from the game log. These tests play
/// whole bot games and check that nothing is missing: every dollar that moved shows up in the
/// ledger, and the history always names the property's real owner.
/// </summary>
public class HistoryTests
{
    private static async Task<GameEngine> PlayAsync(ulong seed, int players, bool smartBots)
    {
        var engine = GameEngine.NewGame(new GameSetup
        {
            Players = Enumerable.Range(0, players).Select(i => new PlayerSetup($"Bot {i}")).ToList(),
            Seed = seed,
            MaxRounds = 30,
            Characters = CharacterMode.Random,
        });
        var bots = engine.State.Players.ToDictionary(p => p.Id, p => smartBots
            ? (IPlayerController)new HardBot(seed * 31 + (ulong)p.Id)
            : new RandomBot(seed * 31 + (ulong)p.Id));
        await new GameHost(engine, bots).RunAsync();
        return engine;
    }

    [Theory]
    [InlineData(false, 2)]
    [InlineData(false, 4)]
    [InlineData(false, 6)]
    [InlineData(true, 3)]
    [InlineData(true, 5)]
    public async Task EveryDollar_IsInTheLedger(bool smartBots, int players)
    {
        for (ulong seed = 1; seed <= 25; seed++)
        {
            var engine = await PlayAsync(seed, players, smartBots);
            var view = engine.GetView(engine.State.Players[0].Id);
            var ledger = GameHistory.Ledger(view, engine.Content);
            foreach (PlayerState player in engine.State.Players)
            {
                int total = ledger.Where(l => l.PlayerId == player.Id).Sum(l => l.Amount);
                Assert.True(total == player.Cash, $"Seed {seed}: {player.Name}'s ledger adds up to ${total}, but they have ${player.Cash}.");
            }
        }
    }

    [Theory]
    [InlineData(false, 4)]
    [InlineData(true, 4)]
    public async Task PropertyHistory_NamesTheRealOwner(bool smartBots, int players)
    {
        for (ulong seed = 1; seed <= 25; seed++)
        {
            var engine = await PlayAsync(seed, players, smartBots);
            var view = engine.GetView(engine.State.Players[0].Id);
            foreach (PropertyState property in engine.State.Properties.Values)
            {
                var history = GameHistory.ForSpace(view, engine.Content, property.PropertyId);
                Assert.True(property.OwnerId == history.Acquired?.OwnerId,
                    $"Seed {seed}: {property.PropertyId} is owned by {property.OwnerId?.ToString() ?? "the bank"}, but its history says {history.Acquired?.OwnerId.ToString() ?? "the bank"}.");
            }
        }
    }

    [Fact]
    public async Task EverySpace_CountsItsLandings()
    {
        var engine = await PlayAsync(3, 4, smartBots: true);
        var view = engine.GetView(0);
        foreach (var space in engine.State.Board)
        {
            var history = GameHistory.ForSpace(view, engine.Content, space.Id);
            Assert.Equal(engine.State.Log.OfType<LandedOn>().Count(l => l.SpaceId == space.Id), history.TimesLandedOn);
        }
        var tax = GameHistory.ForSpace(view, engine.Content, "income-tax");
        Assert.True(tax.TimesLandedOn > 0);
        Assert.True(tax.FeesCollected > 0);
    }

    [Fact]
    public async Task OnePlayersLedger_HasOnlyTheirLines_InTurnOrder()
    {
        var engine = await PlayAsync(9, 4, smartBots: true);
        var view = engine.GetView(0);
        var mine = GameHistory.Ledger(view, engine.Content, playerId: 0);
        Assert.All(mine, l => Assert.Equal(0, l.PlayerId));
        Assert.Equal(MoneyKind.Start, mine[0].Kind);
        // Rounds only ever go up.
        Assert.Equal(mine.Select(l => l.Round).Order(), mine.Select(l => l.Round));
    }
}
