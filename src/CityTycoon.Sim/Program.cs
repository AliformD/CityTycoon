// CityTycoon.Sim: runs bot-vs-bot games and writes balance reports.
// Phase 0 only proves the project runs; games arrive in Phase 1.
using CityTycoon.Engine.Content;

RulesConfig rules = ContentLoader.LoadRules();
Console.WriteLine($"City Tycoon Sim. Rules loaded: starting cash ${rules.StartingCash}, GO salary ${rules.GoSalary}.");
