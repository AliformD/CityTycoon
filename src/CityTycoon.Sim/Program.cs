// CityTycoon.Sim: plays many bot-vs-bot games and writes a balance report.
//
// Example:  dotnet run --project src/CityTycoon.Sim -- --games 1000 --players 4 --bots easy
using System.Diagnostics;
using System.Globalization;
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
            default: throw new ArgumentException($"Unknown option {args[i]}. Options: --games --players --rounds --seed --mode --bots --out");
        }
    }
    return new SimOptions(games, players, seed, mode, rounds, output, bots);
}

static IEnumerable<string> ToCsv(List<GameStats> results)
{
    yield return "game,seed,rounds,finished,winner_seats,winner_turn_positions,bankruptcies,auctions_won,avg_price_to_value,jail_visits,error";
    foreach (var r in results)
    {
        yield return string.Join(",",
            r.Game, r.Seed, r.Rounds, r.Finished,
            string.Join(" ", r.WinnerSeats), string.Join(" ", r.WinnerTurnPositions),
            r.Bankruptcies, r.AuctionsWon, r.AveragePriceToValue.ToString("0.000", CultureInfo.InvariantCulture),
            r.JailVisits, "\"" + (r.Error ?? "").Replace("\"", "'") + "\"");
    }
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
        lines.Add("Win rate by turn position (1 = goes first). Even share would be " + (1.0 / options.Players).ToString("P0") + ":");
        for (int pos = 0; pos < options.Players; pos++)
        {
            double wins = finished.Sum(r => r.WinnerTurnPositions.Contains(pos) ? 1.0 / r.WinnerTurnPositions.Count : 0);
            lines.Add($"  Position {pos + 1}: {wins / finished.Count:P1}");
        }
    }
    foreach (var e in errors.Take(10))
        lines.Add($"ERROR in game {e.Game} (seed {e.Seed}): {e.Error}");
    return string.Join(Environment.NewLine, lines);
}
