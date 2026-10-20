// CityTycoon.Sim: plays many bot-vs-bot games and writes a balance report.
//
// Example:  dotnet run --project src/CityTycoon.Sim -- --games 1000 --players 4 --bots easy
using System.Diagnostics;
using System.Globalization;
using CityTycoon.Engine;
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
            default: throw new ArgumentException($"Unknown option {args[i]}. Options: --games --players --rounds --seed --mode --bots --characters (none or random) --out");
        }
    }
    return new SimOptions(games, players, seed, mode, rounds, output, bots, characters);
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
        AddCharacterReport(lines, finished, options);
        AddPerkReport(lines, finished, options);
    }
    foreach (var e in errors.Take(10))
        lines.Add($"ERROR in game {e.Game} (seed {e.Seed}): {e.Error}");
    return string.Join(Environment.NewLine, lines);
}
