using CityTycoon.AI;
using CityTycoon.Engine;
using CityTycoon.Engine.Events;
using CityTycoon.Engine.Hosting;
using CityTycoon.Engine.Content;
using CityTycoon.Engine.Rules;
using CityTycoon.Engine.State;

namespace CityTycoon.Sim;

/// <param name="Characters">None plays the plain rules; Random deals every bot a different character.</param>
public sealed record SimOptions(int Games, int Players, ulong Seed, GameMode Mode, int Rounds, string OutputFolder,
    BotKind Bots = BotKind.Mixed, CharacterMode Characters = CharacterMode.Random);

/// <summary>Which bots play in the Sim: all one kind, or two kinds in alternating seats (even seats get the first, listed one, so HardVsEasy gives Hard bots seats 0, 2, 4).</summary>
public enum BotKind { Random, Easy, Normal, Hard, Mixed, NormalVsEasy, HardVsEasy, HardVsNormal }

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

    // Phase 4 numbers: Momentum and landmarks.
    public int Nudges { get; init; }
    public int PrecisionRolls { get; init; }
    public int CasinoBets { get; init; }
    /// <summary>What players won (positive) or lost (negative) at High/Low in total.</summary>
    public int CasinoNet { get; init; }
    public int Duels { get; init; }
    public int InsuranceBought { get; init; }

    // Phase 5 numbers: cards.
    public int CardsPlayed { get; init; }
    public int CardsCancelled { get; init; }
    public int EventsDrawn { get; init; }
    public int OpportunitiesDrawn { get; init; }
    public int MallBuys { get; init; }
    public int Reshuffles { get; init; }

    // Phase 7 numbers: characters and perks.
    /// <summary>The character each player had, by seat (empty when the game had no characters).</summary>
    public IReadOnlyList<string> Characters { get; init; } = [];
    public IReadOnlyList<string> WinnerCharacters { get; init; } = [];

    /// <summary>For each color group: how many players held the whole group when the game ended, and how many winners did.</summary>
    public IReadOnlyDictionary<string, int> GroupHolders { get; init; } = new Dictionary<string, int>();
    public IReadOnlyDictionary<string, int> GroupWinners { get; init; } = new Dictionary<string, int>();

    /// <summary>How often each railroad or utility bonus came into play (the log says so: Express Line jumps, Toll Collector tokens...).</summary>
    public int ExpressLines { get; init; }

    /// <summary>Sealed and open auctions, sealed auctions where two or more players made the same top bid, and sealed auctions the Mogul won on their edge or a tie.</summary>
    public int SealedAuctions { get; init; }
    public int OpenAuctions { get; init; }
    public int TiedSealedAuctions { get; init; }
    public int MogulEdgeWins { get; init; }
    public int HostileTakeovers { get; init; }

    /// <summary>City Council sessions held, how many ordinances passed, the total lobbying money spent, and the most any one vote swung (Yes percent).</summary>
    public int CouncilSessions { get; init; }
    public int OrdinancesPassed { get; init; }
    public int LobbyingSpent { get; init; }
    public int CouncilCancelled { get; init; }
    public Dictionary<string, int> OrdinanceCounts { get; init; } = [];
    public int PerkPayments { get; init; }
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
            Characters = options.Characters,
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

    /// <summary>For each color group, how many of these players own the whole group (none of it mortgaged or pending).</summary>
    private static Dictionary<string, int> GroupHoldings(GameState state, GameContent content, IEnumerable<int> playerIds)
    {
        var ids = playerIds.Where(id => !state.Player(id).IsBankrupt).ToList();
        return content.Groups.Keys.ToDictionary(
            group => group,
            group => ids.Count(id => RentCalculator.OwnsFullGroup(state, content, id, group)));
    }

    private static IPlayerController CreateBot(BotKind kind, int playerId, ulong botSeed) => kind switch
    {
        BotKind.Random => new RandomBot(botSeed),
        BotKind.Easy => new EasyBot(botSeed),
        BotKind.Normal => new NormalBot(botSeed),
        BotKind.Hard => new HardBot(botSeed),
        BotKind.NormalVsEasy => playerId % 2 == 0 ? new NormalBot(botSeed) : new EasyBot(botSeed),
        BotKind.HardVsEasy => playerId % 2 == 0 ? new HardBot(botSeed) : new EasyBot(botSeed),
        BotKind.HardVsNormal => playerId % 2 == 0 ? new HardBot(botSeed) : new NormalBot(botSeed),
        _ => playerId % 2 == 0 ? new EasyBot(botSeed) : new RandomBot(botSeed),
    };

    /// <summary>One sealed auction that ended with bids: was the top bid tied, and did the Mogul win it on their edge or a tie?</summary>
    private sealed record SealedOutcome(bool Tied, bool MogulWonOnEdgeOrTie);

    private static List<SealedOutcome> SealedAuctionOutcomes(GameState state)
    {
        var outcomes = new List<SealedOutcome>();
        BidsRevealed? revealed = null;
        bool edge = false;
        foreach (var e in state.Log)
        {
            if (e is BidsRevealed b) { revealed = b; edge = false; }
            else if (e is BidEdgeWon) edge = true;
            else if (e is AuctionWon w && revealed != null && revealed.PropertyId == w.PropertyId)
            {
                int top = revealed.Bids.Max(x => x.Amount ?? 0);
                bool tied = revealed.Bids.Count(x => x.Amount == top) > 1;
                bool mogul = state.Player(w.PlayerId).CharacterId == "mogul";
                outcomes.Add(new SealedOutcome(tied, mogul && (edge || tied)));
                revealed = null;
            }
        }
        return outcomes;
    }

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
            Nudges = log.OfType<Nudged>().Count(),
            PrecisionRolls = log.OfType<PrecisionRolled>().Count(),
            CasinoBets = log.OfType<CasinoHighLow>().Count(),
            CasinoNet = log.OfType<CasinoHighLow>().Sum(c => c.Net),
            Duels = log.OfType<DuelWon>().Count(),
            InsuranceBought = log.OfType<InsuranceBought>().Count(),
            CardsPlayed = log.OfType<CardPlayed>().Count(),
            CardsCancelled = log.OfType<CardCancelled>().Count(),
            EventsDrawn = log.OfType<EventDrawn>().Count(),
            OpportunitiesDrawn = log.OfType<OpportunityDrawn>().Count(),
            MallBuys = log.OfType<MallCardBought>().Count(),
            Reshuffles = log.OfType<DeckReshuffled>().Count(),
            Characters = state.Players.Select(p => p.CharacterId).OfType<string>().ToList(),
            WinnerCharacters = winners.Select(id => state.Player(id).CharacterId).OfType<string>().ToList(),
            GroupHolders = GroupHoldings(state, engine.Content, state.Players.Select(p => p.Id)),
            GroupWinners = GroupHoldings(state, engine.Content, winners),
            ExpressLines = log.OfType<ExpressLineUsed>().Count(),
            SealedAuctions = log.OfType<AuctionStarted>().Count(a => !a.Open),
            OpenAuctions = log.OfType<AuctionStarted>().Count(a => a.Open),
            TiedSealedAuctions = SealedAuctionOutcomes(state).Count(o => o.Tied),
            MogulEdgeWins = SealedAuctionOutcomes(state).Count(o => o.MogulWonOnEdgeOrTie),
            HostileTakeovers = log.OfType<HostileTakeoverUsed>().Count(),
            CouncilSessions = log.OfType<CouncilResolved>().Count(),
            OrdinancesPassed = log.OfType<CouncilResolved>().Count(r => r.Passed),
            LobbyingSpent = log.OfType<CouncilResolved>().Sum(r => r.Votes.Sum(v => v.Lobby)),
            CouncilCancelled = log.OfType<CouncilCancelled>().Count(),
            OrdinanceCounts = log.OfType<CouncilResolved>().Where(r => r.Passed).GroupBy(r => r.OrdinanceId).ToDictionary(g => g.Key, g => g.Count()),
            PerkPayments = log.OfType<PerkPaid>().Count() + log.OfType<PerkIncome>().Count(),
        };
    }
}
