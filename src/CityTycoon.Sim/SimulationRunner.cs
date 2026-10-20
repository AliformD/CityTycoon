using CityTycoon.AI;
using CityTycoon.Engine;
using CityTycoon.Engine.Events;
using CityTycoon.Engine.Hosting;
using CityTycoon.Engine.State;

namespace CityTycoon.Sim;

public sealed record SimOptions(int Games, int Players, ulong Seed, GameMode Mode, int Rounds, string OutputFolder, BotKind Bots = BotKind.Mixed);

/// <summary>Which bots play in the Sim: all random, all Easy, or half and half (alternating seats).</summary>
public enum BotKind { Random, Easy, Mixed }

/// <summary>The numbers collected from one finished (or crashed) game.</summary>
public sealed record GameStats(
    int Game,
    ulong Seed,
    int Rounds,
    bool Finished,
    string? Error,
    IReadOnlyList<int> WinnerSeats,
    IReadOnlyList<int> WinnerTurnPositions,
    int Bankruptcies,
    int AuctionsWon,
    double AveragePriceToValue,
    int JailVisits)
{
    /// <summary>True for a Classic game stopped at the round cap because nobody had gone bankrupt enough to end it.</summary>
    public bool Stalled { get; init; }

    // Phase 3 numbers: how much building, borrowing and trading happened.
    public int BuildingsBuilt { get; init; }
    public int LoansTaken { get; init; }
    public int Mortgages { get; init; }
    public int DownPaymentWins { get; init; }
    public int Forfeits { get; init; }
    public int TradesCompleted { get; init; }
}

/// <summary>Plays many bot-vs-bot games and gathers statistics (Section 26.3).</summary>
public static class SimulationRunner
{
    /// <summary>Classic games can, in theory, go on forever. Stop them here and count them as "stalled".</summary>
    public const int ClassicRoundCap = 500;

    public static async Task<List<GameStats>> RunAsync(SimOptions options)
    {
        var results = new List<GameStats>();
        for (int game = 0; game < options.Games; game++)
            results.Add(await PlayOneAsync(options, game));
        return results;
    }

    public static async Task<GameStats> PlayOneAsync(SimOptions options, int game)
    {
        ulong seed = options.Seed + (ulong)game;
        var setup = new GameSetup
        {
            Players = Enumerable.Range(0, options.Players).Select(i => new PlayerSetup($"Bot {i + 1}")).ToList(),
            Mode = options.Mode,
            MaxRounds = options.Rounds,
            Seed = seed,
        };

        GameEngine? engine = null;
        try
        {
            engine = GameEngine.NewGame(setup);
            var controllers = engine.State.Players.ToDictionary(
                p => p.Id,
                p => CreateBot(options.Bots, p.Id, seed * 31 + (ulong)p.Id));
            var host = new GameHost(engine, controllers);
            while (!engine.State.IsOver)
            {
                if (engine.State.Round > ClassicRoundCap)
                    return Summarize(game, seed, engine, error: null) with { Stalled = true };
                await host.StepAsync();
            }
            return Summarize(game, seed, engine, error: null);
        }
        catch (Exception ex)
        {
            return engine == null
                ? new GameStats(game, seed, 0, false, ex.Message, [], [], 0, 0, 0, 0)
                : Summarize(game, seed, engine, ex.GetType().Name + ": " + ex.Message);
        }
    }

    private static IPlayerController CreateBot(BotKind kind, int playerId, ulong botSeed) => kind switch
    {
        BotKind.Random => new RandomBot(botSeed),
        BotKind.Easy => new EasyBot(botSeed),
        _ => playerId % 2 == 0 ? new EasyBot(botSeed) : new RandomBot(botSeed),
    };

    private static GameStats Summarize(int game, ulong seed, GameEngine engine, string? error)
    {
        GameState state = engine.State;
        var log = state.Log;
        var wins = log.OfType<AuctionWon>().ToList();
        var winners = state.Result?.WinnerIds ?? [];
        return new GameStats(
            game,
            seed,
            state.Round,
            state.IsOver && error == null,
            error,
            winners,
            winners.Select(id => state.Players.FindIndex(p => p.Id == id)).ToList(),
            log.OfType<PlayerBankrupt>().Count(),
            wins.Count,
            wins.Count == 0 ? 0 : wins.Average(w => (double)w.Price / w.AssessedValue),
            log.OfType<SentToJail>().Count())
        {
            BuildingsBuilt = log.OfType<BuildingBuilt>().Count(),
            LoansTaken = log.OfType<LoanTaken>().Count(),
            Mortgages = log.OfType<PropertyMortgaged>().Count(),
            DownPaymentWins = wins.Count(w => w.DownPayment),
            Forfeits = log.OfType<PropertyForfeited>().Count(),
            TradesCompleted = log.OfType<TradeCompleted>().Count(),
        };
    }
}
