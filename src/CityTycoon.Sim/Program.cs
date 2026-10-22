// CityTycoon.Sim: plays many bot-vs-bot games and writes a balance report.
//
// Example:  dotnet run --project src/CityTycoon.Sim -- --games 1000 --players 4 --bots easy
// Try other numbers without editing rules.json: --set goSalary=300 --set startingCash=2000
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using CityTycoon.Engine;
using CityTycoon.Engine.Content;
using CityTycoon.Engine.Events;
using CityTycoon.Engine.State;
using CityTycoon.Sim;

var options = ParseArgs(args);
Console.WriteLine($"Playing {options.Games} {options.Mode} games with {options.Players} {options.Bots.ToString().ToLowerInvariant()} bots (first seed {options.Seed})...");

var timer = Stopwatch.StartNew();
List<GameStats> results = await SimulationRunner.RunAsync(options);
timer.Stop();

Directory.CreateDirectory(options.OutputFolder);
string csvPath = Path.Combine(options.OutputFolder, "games.csv");
File.WriteAllLines(csvPath, ToCsv(results));
File.WriteAllLines(Path.Combine(options.OutputFolder, "trades.csv"), TradesCsv(results));
File.WriteAllLines(Path.Combine(options.OutputFolder, "bot-notes.csv"), NotesCsv(results));
File.WriteAllLines(Path.Combine(options.OutputFolder, "landing.csv"), LandingCsv(results));
string summary = Summarize(results, options, timer.Elapsed);
File.WriteAllText(Path.Combine(options.OutputFolder, "summary.txt"), summary);
Console.WriteLine(summary);
Console.WriteLine($"Per-game results: {Path.GetFullPath(csvPath)}");

// A non-zero exit code tells CI (or a script) that something went wrong.
return results.Any(r => r.Error != null) ? 1 : 0;

static SimOptions ParseArgs(string[] args)
{
    int games = 100, players = 4, rounds = 30;
    ulong seed = 1;
    GameMode mode = GameMode.Timed;
    string output = "sim-results";
    BotKind bots = BotKind.Mixed;
    CharacterMode characters = CharacterMode.Random;
    var overrides = new Dictionary<string, string>();
    for (int i = 0; i < args.Length - 1; i += 2)
    {
        string value = args[i + 1];
        switch (args[i])
        {
            case "--games": games = int.Parse(value); break;
            case "--players": players = int.Parse(value); break;
            case "--rounds": rounds = int.Parse(value); break;
            case "--seed": seed = ulong.Parse(value); break;
            case "--mode": mode = Enum.Parse<GameMode>(value, ignoreCase: true); break;
            case "--out": output = value; break;
            case "--bots": bots = Enum.Parse<BotKind>(value, ignoreCase: true); break;
            case "--characters": characters = Enum.Parse<CharacterMode>(value, ignoreCase: true); break;
            case "--set":
                string[] parts = value.Split('=', 2);
                if (parts.Length != 2)
                    throw new ArgumentException($"--set needs name=value, like --set goSalary=300 (got '{value}').");
                overrides[parts[0]] = parts[1];
                break;
            default: throw new ArgumentException($"Unknown option {args[i]}. Options: --games --players --rounds --seed --mode --bots --characters (none or random) --set name=value --out");
        }
    }
    return new SimOptions(games, players, seed, mode, rounds, output, bots, characters, WithOverrides(overrides));
}

// A copy of the game's rules with some numbers changed, or null when nothing was changed.
// The names are the ones in rules.json. The rules go to JSON and back, so a misspelt name is caught.
static GameContent? WithOverrides(Dictionary<string, string> overrides)
{
    if (overrides.Count == 0)
        return null;
    var rules = JsonSerializer.SerializeToNode(GameContent.Default.Rules, ContentLoader.JsonOptions)!.AsObject();
    foreach (var (name, value) in overrides)
    {
        if (!rules.ContainsKey(name))
            throw new ArgumentException($"rules.json has no number called '{name}'.");
        rules[name] = JsonNode.Parse(value);
        Console.WriteLine($"Rule changed for this run: {name} = {value}");
    }
    return GameContent.Default.WithRules(rules.Deserialize<RulesConfig>(ContentLoader.JsonOptions)!);
}

static IEnumerable<string> ToCsv(List<GameStats> results)
{
    yield return "game,seed,rounds,finished,winner_seats,winner_turn_positions,winner_characters,bankruptcies,auctions_won,avg_price_to_value,jail_visits,error";
    foreach (var r in results)
    {
        yield return string.Join(",",
            r.Game, r.Seed, r.Rounds, r.Finished,
            string.Join(" ", r.WinnerSeats), string.Join(" ", r.WinnerTurnPositions), string.Join(" ", r.WinnerCharacters),
            r.Bankruptcies, r.AuctionsWon, r.AveragePriceToValue.ToString("0.000", CultureInfo.InvariantCulture),
            r.JailVisits, "\"" + (r.Error ?? "").Replace("\"", "'") + "\"");
    }
}

// One row per finished trade (decision #150): who made the final terms ("from"), what changed hands,
// the rent each side collected afterwards on what it got (whole color groups), and who won the game.
static IEnumerable<string> TradesCsv(List<GameStats> results)
{
    yield return "game,round,from_seat,from_level,to_seat,to_level,from_gives_cash,from_gets_cash,from_gives,from_gets,counters,from_rent_after,to_rent_after,from_won,to_won";
    foreach (var r in results)
    {
        foreach (var t in r.Trades)
        {
            string Level(int id) => id < r.Levels.Count ? r.Levels[id] : "";
            yield return string.Join(",", r.Game, t.Round, t.Offer.FromId, Level(t.Offer.FromId), t.Offer.ToId, Level(t.Offer.ToId),
                t.Offer.GiveCash, t.Offer.GetCash, string.Join(" ", t.Offer.GiveProperties), string.Join(" ", t.Offer.GetProperties),
                t.Counters, t.FromRent, t.ToRent, r.WinnerSeats.Contains(t.Offer.FromId), r.WinnerSeats.Contains(t.Offer.ToId));
        }
    }
}

// Every bot's private trade notes (see BotNote): one row per choice, with the sums behind it.
static IEnumerable<string> NotesCsv(List<GameStats> results)
{
    yield return "game,round,bot_seat,level,personality,kind,choice,from_seat,to_seat,give_cash,get_cash,gives,gets,cash,basic,mine,theirs,gain,limit,counter_give_cash,counter_get_cash,won,why";
    static string N(double x) => x.ToString("0", CultureInfo.InvariantCulture);
    foreach (var r in results)
    {
        foreach (var n in r.Notes)
        {
            yield return string.Join(",", r.Game, n.Round, n.BotId, n.Level, n.Personality, n.Kind, n.Choice, n.Offer.FromId, n.Offer.ToId,
                n.Offer.GiveCash, n.Offer.GetCash, string.Join(" ", n.Offer.GiveProperties), string.Join(" ", n.Offer.GetProperties),
                n.Cash, N(n.Basic), N(n.Mine), N(n.Theirs), N(n.Gain), n.Limit, n.Counter?.GiveCash.ToString() ?? "", n.Counter?.GetCash.ToString() ?? "",
                r.WinnerSeats.Contains(n.BotId), "\"" + (n.Why ?? "").Replace("\"", "'") + "\"");
        }
    }
}

// How often a turn ended on each space, as a share of all turns (decision #150). Bots spend Nudges to dodge
// big rents, so these are the odds with bots playing, not the pure dice odds.
static IEnumerable<string> LandingCsv(List<GameStats> results)
{
    var content = CityTycoon.Engine.Content.GameContent.Default;
    double turns = Math.Max(1, results.Sum(r => r.Turns));
    yield return "space,group,landing_percent_of_turns";
    foreach (string id in results.SelectMany(r => r.Landings.Keys).Distinct().OrderByDescending(id => results.Sum(r => r.Landings.GetValueOrDefault(id))))
    {
        string group = content.Properties.TryGetValue(id, out var p) ? p.Group ?? p.Type.ToString() : "";
        yield return string.Join(",", id, group, (results.Sum(r => r.Landings.GetValueOrDefault(id)) / turns * 100).ToString("0.000", CultureInfo.InvariantCulture));
    }
}

// The trade report card in the summary: how many trades, and whether they paid off for each side.
static void AddTradeReport(List<string> lines, List<GameStats> finished)
{
    var trades = finished.SelectMany(r => r.Trades.Select(t => (Game: r, Trade: t))).ToList();
    int offers = finished.Sum(r => r.TradeOffers);
    lines.Add($"Trade report card: {offers} offers, {trades.Count} finished ({(offers == 0 ? 0 : (double)trades.Count / offers):P0}), "
        + $"{(trades.Count == 0 ? 0 : trades.Average(x => x.Trade.Counters)):0.0} counter-offers per finished trade.");
    if (trades.Count == 0)
        return;
    lines.Add($"  Rent collected afterwards on what each side got (whole groups): final-terms side ${trades.Average(x => x.Trade.FromRent):0}, "
        + $"other side ${trades.Average(x => x.Trade.ToRent):0} per trade.");
    lines.Add($"  Who won the game: final-terms side {trades.Count(x => x.Game.WinnerSeats.Contains(x.Trade.Offer.FromId)) / (double)trades.Count:P1}, "
        + $"other side {trades.Count(x => x.Game.WinnerSeats.Contains(x.Trade.Offer.ToId)) / (double)trades.Count:P1}. Details in trades.csv and bot-notes.csv.");
}

// How often each color group was landed on, as a share of all turns.
static void AddLandingReport(List<string> lines, List<GameStats> finished)
{
    var content = CityTycoon.Engine.Content.GameContent.Default;
    double turns = Math.Max(1, finished.Sum(r => r.Turns));
    lines.Add("Landing odds by color group (share of turns that end on one of its spaces; per space in landing.csv):");
    foreach (var group in content.Groups.Values)
    {
        int landings = content.GroupMembers(group.Id).Sum(id => finished.Sum(r => r.Landings.GetValueOrDefault(id)));
        lines.Add($"  {group.Name,-10} {landings / turns,6:P1}");
    }
}

// For games with two kinds of bot in alternating seats: how often the first-listed kind (even seats) won.
static void AddMatchupReport(List<string> lines, List<GameStats> finished, SimOptions options)
{
    if (options.Bots is not (BotKind.Mixed or BotKind.NormalVsEasy or BotKind.HardVsEasy or BotKind.HardVsNormal))
        return;
    int strongSeats = (options.Players + 1) / 2;
    double even = (double)strongSeats / options.Players;
    double wins = finished.Sum(r => r.WinnerSeats.Count(seat => seat % 2 == 0) / (double)r.WinnerSeats.Count);
    lines.Add($"Matchup {options.Bots}: the even seats (first kind listed) won {wins / finished.Count:P1} of games. An even share would be {even:P1}.");
}

// Win rate by character (Section 26.3). A character's win rate is its wins divided by the games it played in
// (a shared win counts as a fraction). The balance target: no more than 5 points above an even share.
static void AddCharacterReport(List<string> lines, List<GameStats> finished, SimOptions options)
{
    var withCharacters = finished.Where(r => r.Characters.Count > 0).ToList();
    if (withCharacters.Count == 0)
        return;
    double even = 1.0 / options.Players;
    lines.Add($"Win rate by character. Even share would be {even:P1}; the target is no more than 5 points above it:");
    var content = CityTycoon.Engine.Content.GameContent.Default;
    foreach (var character in content.Characters)
    {
        int played = withCharacters.Count(r => r.Characters.Contains(character.Id));
        if (played == 0)
            continue;
        double wins = withCharacters.Sum(r => r.WinnerCharacters.Contains(character.Id) ? 1.0 / r.WinnerCharacters.Count : 0);
        double rate = wins / played;
        string flag = rate > even + 0.05 ? "  <-- over the target" : "";
        lines.Add($"  {character.Name,-18} {rate,6:P1}  ({wins:0.#} wins in {played} games){flag}");
    }
}

// Win rate by secret bot personality, counted the same way as characters.
static void AddPersonalityReport(List<string> lines, List<GameStats> finished, SimOptions options)
{
    var seats = finished.SelectMany(r => r.Personalities).Where(p => p != "").ToList();
    if (seats.Count == 0)
        return;
    lines.Add($"Win rate by bot personality. Even share would be {1.0 / options.Players:P1}:");
    foreach (string personality in seats.Distinct().Order())
    {
        int played = seats.Count(p => p == personality);
        double wins = finished.Sum(r => r.WinnerPersonalities.Count(p => p == personality) / (double)Math.Max(1, r.WinnerPersonalities.Count));
        lines.Add($"  {personality,-10} {wins / played,6:P1}  (in {played} seats)");
    }
}

// Which color perks the winners owned at the end, compared with everyone who owned them.
static void AddPerkReport(List<string> lines, List<GameStats> finished, SimOptions options)
{
    var content = CityTycoon.Engine.Content.GameContent.Default;
    lines.Add("Color groups owned whole at the end of the game (holders, and how often a holder was a winner):");
    foreach (var group in content.Groups.Values)
    {
        int holders = finished.Sum(r => r.GroupHolders.GetValueOrDefault(group.Id));
        int winners = finished.Sum(r => r.GroupWinners.GetValueOrDefault(group.Id));
        lines.Add($"  {group.Name,-10} {group.Perk.Name,-20} held by {holders,5} players, {(holders == 0 ? 0 : (double)winners / holders):P1} of them won");
    }
    lines.Add($"Perks and bonuses per game: {finished.Average(r => r.PerkPayments):0.0} perk payments, "
        + $"{finished.Average(r => r.ExpressLines):0.00} Express Line jumps, {finished.Average(r => r.HostileTakeovers):0.00} Hostile Takeovers");
    int sealedAuctions = finished.Sum(r => r.SealedAuctions);
    lines.Add($"Auctions per game: {finished.Average(r => r.SealedAuctions):0.0} sealed, {finished.Average(r => r.OpenAuctions):0.0} open. "
        + $"Sealed auctions with a tied top bid: {(sealedAuctions == 0 ? 0 : (double)finished.Sum(r => r.TiedSealedAuctions) / sealedAuctions):P1}. "
        + $"Sealed auctions the Mogul won on their edge or a tie: {finished.Average(r => r.MogulEdgeWins):0.00} per game.");
    int sessions = finished.Sum(r => r.CouncilSessions);
    lines.Add($"City Council per game: {finished.Average(r => r.CouncilSessions):0.0} votes, "
        + $"{(sessions == 0 ? 0 : (double)finished.Sum(r => r.OrdinancesPassed) / sessions):P0} passed, "
        + $"${finished.Average(r => r.LobbyingSpent):0} spent lobbying (Politician ${finished.Average(r => r.PoliticianLobbying):0}), "
        + $"{finished.Average(r => r.CouncilCancelled):0.00} sessions cancelled, "
        + $"{finished.Average(r => r.VoteDeals):0.0} vote deals worth ${finished.Average(r => r.VoteDealMoney):0}.");
    lines.Add("Table talk per game: " + string.Join(", ", Enum.GetValues<TalkTone>()
        .Select(t => $"{finished.Average(r => r.Remarks.GetValueOrDefault(t)):0.0} {t.ToString().ToLowerInvariant()}")) + ".");
    var passedByOrdinance = finished.SelectMany(r => r.OrdinanceCounts).GroupBy(kv => kv.Key).ToDictionary(g => g.Key, g => g.Sum(kv => kv.Value));
    foreach (var ordinance in content.Ordinances)
        lines.Add($"  {ordinance.Name,-28} passed {(double)passedByOrdinance.GetValueOrDefault(ordinance.Id) / finished.Count:0.00} times per game");
    lines.Add($"Dice Duels in all {finished.Count} games: {finished.Sum(r => r.Duels)}.");
}

static string Summarize(List<GameStats> results, SimOptions options, TimeSpan elapsed)
{
    var finished = results.Where(r => r.Finished).ToList();
    var errors = results.Where(r => r.Error != null).ToList();
    var lines = new List<string>
    {
        "=== City Tycoon simulation ===",
        $"Games: {results.Count}  Finished: {finished.Count}  Stalled (Classic, over {SimulationRunner.ClassicRoundCap} rounds): {results.Count(r => r.Stalled)}  Errors: {errors.Count}  Time: {elapsed.TotalSeconds:0.0}s",
    };
    if (finished.Count > 0)
    {
        var rounds = finished.Select(r => (double)r.Rounds).ToList();
        double avg = rounds.Average();
        double spread = Math.Sqrt(rounds.Average(x => (x - avg) * (x - avg)));
        lines.Add($"Game length: average {avg:0.0} rounds (spread +/- {spread:0.0}), shortest {rounds.Min()}, longest {rounds.Max()}");
        lines.Add($"Bankruptcies per game: {finished.Average(r => r.Bankruptcies):0.00}");
        lines.Add($"Auction price as a share of assessed value: {finished.Where(r => r.AuctionsWon > 0).Average(r => r.AveragePriceToValue):P0}");
        lines.Add($"Jail visits per game: {finished.Average(r => r.JailVisits):0.0}");
        var built = finished.Where(r => r.FirstBuildingRound != null).Select(r => (double)r.FirstBuildingRound!.Value).OrderBy(x => x).ToList();
        lines.Add($"Pacing: someone built in {(double)built.Count / finished.Count:P0} of games; first building in round "
            + (built.Count == 0 ? "-" : $"{built[built.Count / 2]:0} (median), {built.Average():0.0} (average)")
            + $". Players who built: {finished.Average(r => r.Builders):0.0} per game. Building levels standing at the end: {finished.Average(r => r.EndBuildingLevels):0.0}"
            + $". Games with a bankruptcy: {(double)finished.Count(r => r.Bankruptcies > 0) / finished.Count:P0}. "
            + $"Close calls per game (cash under ${SimulationRunner.LowCash}, back to ${SimulationRunner.RecoveredCash}): {finished.Average(r => r.CloseCalls):0.0}");
        var firsts = finished.Where(r => r.Monopolies.Count > 0).Select(r => r.Monopolies.MinBy(m => m.Round)!).ToList();
        var firstRounds = firsts.Select(m => m.Round).OrderBy(x => x).ToList();
        lines.Add($"First color group held by one player: in {(double)firsts.Count / finished.Count:P0} of games; round "
            + (firsts.Count == 0 ? "-" : $"{firstRounds[firstRounds.Count / 2]} (median), {firstRounds.Average():0.0} (average)")
            + $". Games with one by round 3: {(double)finished.Count(r => r.Monopolies.Any(m => m.Round <= 3)) / finished.Count:P0}, by round 5: {(double)finished.Count(r => r.Monopolies.Any(m => m.Round <= 5)) / finished.Count:P0}"
            + $". Groups held by round 5: {finished.Average(r => r.Monopolies.Count(m => m.Round <= 5)):0.0}, by round 10: {finished.Average(r => r.Monopolies.Count(m => m.Round <= 10)):0.0} per game"
            + ". The first one came from: " + string.Join(", ", firsts.GroupBy(m => m.How).OrderByDescending(g => g.Count()).Select(g => $"{g.Key} {(double)g.Count() / firsts.Count:P0}")));
        lines.Add($"Money at the end (players still in): ${finished.Average(r => r.EndCash):0} cash, ${finished.Average(r => r.EndNetWorth):0} net worth. Average rent payment: ${finished.Average(r => r.AverageRent):0}");
        lines.Add($"Per game: {finished.Average(r => r.BuildingsBuilt):0.0} buildings built, {finished.Average(r => r.Mortgages):0.0} mortgages, "
            + $"{finished.Average(r => r.LoansTaken):0.0} loans, {finished.Average(r => r.TradesCompleted):0.0} trades, "
            + $"{finished.Average(r => r.DownPaymentWins):0.0} down-payment wins, {finished.Average(r => r.Forfeits):0.00} forfeits");
        lines.Add($"Per game: {finished.Average(r => r.Nudges):0.0} Nudges, {finished.Average(r => r.PrecisionRolls):0.0} Precision Rolls, "
            + $"{finished.Average(r => r.InsuranceBought):0.0} Insured markers bought, {finished.Average(r => r.Duels):0.0} Dice Duels");
        lines.Add($"Cards per game: {finished.Average(r => r.EventsDrawn):0.0} Events drawn, {finished.Average(r => r.OpportunitiesDrawn):0.0} Opportunity cards drawn, "
            + $"{finished.Average(r => r.MallBuys):0.0} bought at the Mall, {finished.Average(r => r.CardsPlayed):0.0} played, "
            + $"{finished.Average(r => r.CardsCancelled):0.00} cancelled by Insurance, {finished.Average(r => r.Reshuffles):0.0} reshuffles");
        int bets = finished.Sum(r => r.CasinoBets);
        lines.Add($"Casino: {(double)bets / finished.Count:0.0} High/Low bets per game, players' total result {finished.Sum(r => r.CasinoNet):N0} dollars");
        lines.Add("Win rate by turn position (1 = goes first). Even share would be " + (1.0 / options.Players).ToString("P0") + ":");
        for (int pos = 0; pos < options.Players; pos++)
        {
            double wins = finished.Sum(r => r.WinnerTurnPositions.Contains(pos) ? 1.0 / r.WinnerTurnPositions.Count : 0);
            lines.Add($"  Position {pos + 1}: {wins / finished.Count:P1}");
        }
        AddMatchupReport(lines, finished, options);
        AddCharacterReport(lines, finished, options);
        AddPersonalityReport(lines, finished, options);
        AddPerkReport(lines, finished, options);
        AddTradeReport(lines, finished);
        AddLandingReport(lines, finished);
    }
    foreach (var e in errors.Take(10))
        lines.Add($"ERROR in game {e.Game} (seed {e.Seed}): {e.Error}");
    return string.Join(Environment.NewLine, lines);
}
