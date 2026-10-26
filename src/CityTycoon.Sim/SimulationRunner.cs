using CityTycoon.AI;
using CityTycoon.Engine;
using CityTycoon.Engine.Events;
using CityTycoon.Engine.Hosting;
using CityTycoon.Engine.Content;
using CityTycoon.Engine.Rules;
using CityTycoon.Engine.State;

namespace CityTycoon.Sim;

/// <param name="Characters">None plays the plain rules; Random deals every bot a different character.</param>
/// <param name="Content">The rules to play by. Null uses the game's own content files; <c>--set</c> changes numbers in a copy.</param>
public sealed record SimOptions(int Games, int Players, ulong Seed, GameMode Mode, int Rounds, string OutputFolder,
    BotKind Bots = BotKind.Mixed, CharacterMode Characters = CharacterMode.Random, GameContent? Content = null);

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
    /// <summary>Dice Poker hands played at the Casino, and how many had the player's own money on them too (decision #164).</summary>
    public int CasinoHands { get; init; }
    public int CasinoBets { get; init; }
    /// <summary>What players won (positive) or lost (negative) at the Casino in total, free chips included.</summary>
    public int CasinoNet { get; init; }
    /// <summary>What players' own money on Casino hands won or lost, leaving out what the free chip alone would have paid.</summary>
    public int CasinoStakeNet { get; init; }
    public int Showdowns { get; init; }
    public int ShowdownFolds { get; init; }
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
    /// <summary>What happened after each Hostile Takeover: nobody bid, the owner bought it back (and the money they paid), or someone else bought it.</summary>
    public int TakeoverNoBids { get; init; }
    public int TakeoverBuybacks { get; init; }
    public int TakeoverBuybackMoney { get; init; }
    public int TakeoverSales { get; init; }
    public int TakeoverSaleMoney { get; init; }

    /// <summary>City Council sessions held, how many ordinances passed, the total lobbying money spent, and the most any one vote swung (Yes percent).</summary>
    public int CouncilSessions { get; init; }
    public int OrdinancesPassed { get; init; }
    public int LobbyingSpent { get; init; }
    public int CouncilCancelled { get; init; }
    /// <summary>Lobbying money the Politician spent (decision #159; only the Politician lobbies since #162).</summary>
    public int PoliticianLobbying { get; init; }
    /// <summary>Offers made in the bidding for the Politician's vote (decision #162).</summary>
    public int ContractBids { get; init; }
    /// <summary>Vote contracts the Politician took and the money they moved (decision #162).</summary>
    public int VoteDeals { get; init; }
    public int VoteDealMoney { get; init; }
    /// <summary>Remarks bots made at the table, by tone (decision #160).</summary>
    public Dictionary<TalkTone, int> Remarks { get; init; } = [];
    public Dictionary<string, int> OrdinanceCounts { get; init; } = [];
    public int PerkPayments { get; init; }
    // Pacing numbers (how soon building starts, close calls, how much money is around).
    /// <summary>The round the first building went up, or null if nobody built.</summary>
    public int? FirstBuildingRound { get; init; }
    /// <summary>For each color group someone came to hold all of (paid off or not): the round it first happened, and how (an auction with or without a down payment, a trade, or other).</summary>
    public IReadOnlyList<MonopolyFormed> Monopolies { get; init; } = [];
    /// <summary>Times a player fell below <see cref="SimulationRunner.LowCash"/> and later climbed back to <see cref="SimulationRunner.RecoveredCash"/> without going bankrupt.</summary>
    public int CloseCalls { get; init; }
    /// <summary>How many players built at least once, and the building levels standing at the end.</summary>
    public int Builders { get; init; }
    public int EndBuildingLevels { get; init; }
    /// <summary>Average cash and net worth of the players still in at the end.</summary>
    public double EndCash { get; init; }
    public double EndNetWorth { get; init; }
    /// <summary>Each seat's secret bot personality (empty for Random bots), and the winners' personalities.</summary>
    public IReadOnlyList<string> Personalities { get; init; } = [];
    public IReadOnlyList<string> WinnerPersonalities { get; init; } = [];
    /// <summary>The average rent payment, so it can be compared with how much cash players hold.</summary>
    public double AverageRent { get; init; }

    /// <summary>Every finished trade and how it worked out afterwards (decision #150).</summary>
    public IReadOnlyList<TradeOutcome> Trades { get; init; } = [];
    /// <summary>Trade offers made, including ones that were declined.</summary>
    public int TradeOffers { get; init; }
    /// <summary>Each seat's bot level ("Easy", "Normal", "Hard", or "Random").</summary>
    public IReadOnlyList<string> Levels { get; init; } = [];
    /// <summary>Every bot's private trade notes from this game (see <see cref="BotNote"/>).</summary>
    public IReadOnlyList<BotNote> Notes { get; init; } = [];
    /// <summary>How many times a turn ended on each space, and how many turns were played, for the landing odds.</summary>
    public IReadOnlyDictionary<string, int> Landings { get; init; } = new Dictionary<string, int>();
    public int Turns { get; init; }

    /// <summary>How spread out the deeds were after round 6 and after round 10 (null if the game ended first).</summary>
    public Spread? SpreadAt6 { get; init; }
    public Spread? SpreadAt10 { get; init; }
    /// <summary>The average round in which players first passed GO (the end of their first lap).</summary>
    public double FirstLapEndRound { get; init; }
    /// <summary>First-lap house rule: offers to buy at the listed price, and how many were taken.</summary>
    public int FixedPriceOffers { get; init; }
    public int FixedPriceBuys { get; init; }
}

/// <summary>A snapshot of who owns what: players holding at least one deed, the biggest player's share of the owned deeds, color groups split between two or more owners, and deeds still unsold.</summary>
public sealed record Spread(int PlayersWithDeeds, double TopShare, int SplitGroups, int Unsold);

/// <summary>The first time one player held every property of a color group: the round, and the move that did it.</summary>
public sealed record MonopolyFormed(string Group, int Round, string How);

/// <summary>Plays many bot-vs-bot games and gathers statistics (Section 26.3).</summary>
public static class SimulationRunner
{
    /// <summary>Classic games can, in theory, go on forever. Stop them here and count them as "stalled".</summary>
    public const int ClassicRoundCap = 500;

    /// <summary>A close call: cash dropped under this...</summary>
    public const int LowCash = 150;

    /// <summary>...and later climbed back to this, without going bankrupt.</summary>
    public const int RecoveredCash = 400;

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
            GameContent content = options.Content ?? GameContent.Default;
            engine = GameEngine.NewGame(setup, content);
            var controllers = engine.State.Players.ToDictionary(
                p => p.Id,
                p => CreateBot(options.Bots, p.Id, seed * 31 + (ulong)p.Id, content));
            var host = new GameHost(engine, controllers);
            var low = new HashSet<int>();
            int closeCalls = 0;
            var monopolies = new List<MonopolyFormed>();
            Spread? at6 = null, at10 = null;
            while (!engine.State.IsOver)
            {
                if (at6 == null && engine.State.Round > 6)
                    at6 = SpreadNow(engine);
                if (at10 == null && engine.State.Round > 10)
                    at10 = SpreadNow(engine);
                if (engine.State.Round > ClassicRoundCap)
                    return Summarize(game, seed, engine, error: null) with { Stalled = true, CloseCalls = closeCalls, Monopolies = monopolies };
                await host.StepAsync();
                WatchMonopolies(engine, monopolies);
                foreach (PlayerState p in engine.State.Players)
                {
                    if (p.IsBankrupt)
                        low.Remove(p.Id);
                    else if (p.Cash < LowCash)
                        low.Add(p.Id);
                    else if (p.Cash >= RecoveredCash && low.Remove(p.Id))
                        closeCalls++;
                }
            }
            string PersonalityOf(int id) => controllers[id] is EasyBot bot ? bot.Personality.ToString() : "";
            var winners = engine.State.Result?.WinnerIds ?? [];
            return Summarize(game, seed, engine, error: null) with
            {
                SpreadAt6 = at6,
                SpreadAt10 = at10,
                CloseCalls = closeCalls,
                Monopolies = monopolies,
                Levels = engine.State.Players.Select(p => controllers[p.Id].GetType().Name.Replace("Bot", "")).ToList(),
                Notes = controllers.Values.OfType<EasyBot>().SelectMany(b => b.Notebook).OrderBy(n => n.LogIndex).ToList(),
                Personalities = engine.State.Players.Select(p => PersonalityOf(p.Id)).ToList(),
                WinnerPersonalities = winners.Select(PersonalityOf).ToList(),
            };
        }
        catch (Exception ex)
        {
            return engine == null
                ? new GameStats(game, seed, 0, false, ex.Message, [], [], 0, 0, 0, 0)
                : Summarize(game, seed, engine, ex.GetType().Name + ": " + ex.Message);
        }
    }

    /// <summary>Adds any color group that one player now holds every property of, the first time it happens.</summary>
    private static void WatchMonopolies(GameEngine engine, List<MonopolyFormed> found)
    {
        foreach (string group in engine.Content.Groups.Keys)
        {
            if (found.Any(m => m.Group == group))
                continue;
            var owners = engine.Content.GroupMembers(group).Select(id => engine.State.Properties[id].OwnerId).Distinct().ToList();
            if (owners is not [int])
                continue;
            // The most recent auction win or trade is what finished the group.
            GameEvent? last = engine.State.Log.LastOrDefault(e => e is AuctionWon or TradeCompleted);
            string how = last switch { AuctionWon { DownPayment: true } => "auction with a down payment", AuctionWon => "auction paid in full", TradeCompleted => "trade", _ => "other" };
            found.Add(new MonopolyFormed(group, engine.State.Round, how));
        }
    }

    private static Spread SpreadNow(GameEngine engine)
    {
        var owners = engine.State.Properties.Values.Select(p => p.OwnerId).ToList();
        var owned = owners.OfType<int>().ToList();
        int split = engine.Content.Groups.Keys.Count(g =>
            engine.Content.GroupMembers(g).Select(id => engine.State.Properties[id].OwnerId).OfType<int>().Distinct().Count() >= 2);
        double top = owned.Count == 0 ? 0 : owned.GroupBy(id => id).Max(x => x.Count()) / (double)owned.Count;
        return new Spread(owned.Distinct().Count(), top, split, owners.Count(o => o == null));
    }

    private static double FirstLapEnd(GameState state)
    {
        int round = 0;
        var firsts = new Dictionary<int, int>();
        foreach (var e in state.Log)
        {
            if (e is TurnStarted t)
                round = t.Round;
            else if (e is PassedGo g)
                firsts.TryAdd(g.PlayerId, round);
        }
        return firsts.Count == 0 ? 0 : firsts.Values.Average();
    }

    private static int? FirstBuildingRound(IEnumerable<GameEvent> log)
    {
        int round = 0;
        foreach (var e in log)
        {
            if (e is TurnStarted t)
                round = t.Round;
            else if (e is BuildingBuilt)
                return round;
        }
        return null;
    }

    /// <summary>For each color group, how many of these players own the whole group (none of it mortgaged or pending).</summary>
    private static Dictionary<string, int> GroupHoldings(GameState state, GameContent content, IEnumerable<int> playerIds)
    {
        var ids = playerIds.Where(id => !state.Player(id).IsBankrupt).ToList();
        return content.Groups.Keys.ToDictionary(
            group => group,
            group => ids.Count(id => RentCalculator.OwnsFullGroup(state, content, id, group)));
    }

    private static IPlayerController CreateBot(BotKind kind, int playerId, ulong botSeed, GameContent content) => kind switch
    {
        BotKind.Random => new RandomBot(botSeed),
        BotKind.Easy => new EasyBot(botSeed, content),
        BotKind.Normal => new NormalBot(botSeed, content),
        BotKind.Hard => new HardBot(botSeed, content),
        BotKind.NormalVsEasy => playerId % 2 == 0 ? new NormalBot(botSeed, content) : new EasyBot(botSeed, content),
        BotKind.HardVsEasy => playerId % 2 == 0 ? new HardBot(botSeed, content) : new EasyBot(botSeed, content),
        BotKind.HardVsNormal => playerId % 2 == 0 ? new HardBot(botSeed, content) : new NormalBot(botSeed, content),
        _ => playerId % 2 == 0 ? new EasyBot(botSeed, content) : new RandomBot(botSeed),
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

    private sealed record TakeoverOutcome(int Kind, int Price);

    /// <summary>Follows each Hostile Takeover to its auction result. Kind: 0 nobody bid, 1 owner bought back, 2 sold.</summary>
    private static List<TakeoverOutcome> TakeoverOutcomes(IEnumerable<GameEvent> log)
    {
        var outcomes = new List<TakeoverOutcome>();
        HostileTakeoverUsed? open = null;
        foreach (var e in log)
        {
            if (e is HostileTakeoverUsed h)
                open = h;
            else if (e is OwnerKeptProperty k && k.PropertyId == open?.PropertyId)
            {
                outcomes.Add(new(0, 0));
                open = null;
            }
            else if (e is AuctionWon w && w.PropertyId == open?.PropertyId)
            {
                outcomes.Add(new(w.PaidToId == null ? 1 : 2, w.Price));
                open = null;
            }
        }
        return outcomes;
    }

    private static int FixedPriceBuys(IReadOnlyList<GameEvent> log)
    {
        int buys = 0;
        string? offered = null;
        foreach (var e in log)
        {
            if (e is AuctionStarted a)
                offered = a.FixedPrice ? a.PropertyId : null;
            else if (e is AuctionWon w && w.PropertyId == offered)
            {
                buys++;
                offered = null;
            }
        }
        return buys;
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
            CasinoHands = log.OfType<CasinoHandPaid>().Count(),
            CasinoBets = log.OfType<CasinoHandPaid>().Count(c => c.Stake > 0),
            CasinoNet = log.OfType<CasinoHandPaid>().Sum(c => c.Net),
            CasinoStakeNet = log.OfType<CasinoHandPaid>().Sum(c => c.Net - c.Chip * (c.Net + c.Stake) / Math.Max(1, c.Chip + c.Stake)),
            Showdowns = log.OfType<ShowdownCalled>().Count(),
            ShowdownFolds = log.OfType<ShowdownFolded>().Count(),
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
            SealedAuctions = log.OfType<AuctionStarted>().Count(a => !a.Open && !a.FixedPrice),
            OpenAuctions = log.OfType<AuctionStarted>().Count(a => a.Open),
            TiedSealedAuctions = SealedAuctionOutcomes(state).Count(o => o.Tied),
            MogulEdgeWins = SealedAuctionOutcomes(state).Count(o => o.MogulWonOnEdgeOrTie),
            HostileTakeovers = log.OfType<HostileTakeoverUsed>().Count(),
            TakeoverNoBids = TakeoverOutcomes(log).Count(o => o.Kind == 0),
            TakeoverBuybacks = TakeoverOutcomes(log).Count(o => o.Kind == 1),
            TakeoverBuybackMoney = TakeoverOutcomes(log).Where(o => o.Kind == 1).Sum(o => o.Price),
            TakeoverSales = TakeoverOutcomes(log).Count(o => o.Kind == 2),
            TakeoverSaleMoney = TakeoverOutcomes(log).Where(o => o.Kind == 2).Sum(o => o.Price),
            CouncilSessions = log.OfType<CouncilResolved>().Count(),
            // The City Planner's reversal (decision #152) can turn a result around after the roll.
            OrdinancesPassed = log.OfType<CouncilResolved>().Count(r => r.Passed)
                + log.OfType<CouncilReversed>().Sum(r => r.NowPassed ? 1 : -1),
            LobbyingSpent = log.OfType<CouncilResolved>().Sum(r => r.Votes.Sum(v => v.Lobby)),
            CouncilCancelled = log.OfType<CouncilCancelled>().Count(),
            PoliticianLobbying = log.OfType<CouncilResolved>().SelectMany(r => r.Votes)
                .Where(v => state.Player(v.PlayerId).CharacterId == "politician").Sum(v => v.Lobby),
            ContractBids = log.OfType<VoteContractBid>().Count(),
            VoteDeals = log.OfType<VoteDealMade>().Count(),
            VoteDealMoney = log.OfType<VoteDealMade>().Sum(d => d.Deal.Amount),
            Remarks = log.OfType<TableTalk>().GroupBy(t => t.Tone).ToDictionary(g => g.Key, g => g.Count()),
            OrdinanceCounts = log.OfType<CouncilResolved>().Where(r => r.Passed).GroupBy(r => r.OrdinanceId).ToDictionary(g => g.Key, g => g.Count()),
            PerkPayments = log.OfType<PerkPaid>().Count() + log.OfType<PerkIncome>().Count(),
            FirstBuildingRound = FirstBuildingRound(log),
            Builders = log.OfType<BuildingBuilt>().Select(b => b.PlayerId).Distinct().Count(),
            EndBuildingLevels = state.Properties.Values.Sum(p => p.Level),
            EndCash = state.Players.Where(p => !p.IsBankrupt).Select(p => (double)p.Cash).DefaultIfEmpty(0).Average(),
            EndNetWorth = state.Players.Where(p => !p.IsBankrupt).Select(p => (double)NetWorth.Calculate(state, engine.Content, p.Id)).DefaultIfEmpty(0).Average(),
            AverageRent = log.OfType<RentPaid>().Select(r => (double)r.Amount).DefaultIfEmpty(0).Average(),
            Trades = TradeReport.Trades(log, engine.Content),
            TradeOffers = log.OfType<TradeProposed>().Count(),
            Landings = log.OfType<LandedOn>().GroupBy(l => l.SpaceId).ToDictionary(g => g.Key, g => g.Count()),
            Turns = log.OfType<TurnStarted>().Count(),
            FirstLapEndRound = FirstLapEnd(state),
            FixedPriceOffers = log.OfType<AuctionStarted>().Count(a => a.FixedPrice),
            FixedPriceBuys = FixedPriceBuys(log),
        };
    }
}
