using System.Text.Json;
using System.Text.Json.Nodes;
using CityTycoon.Engine.Content;

namespace CityTycoon.Engine.Tests.Helpers;

/// <summary>
/// The game's content, but with the money numbers most tests were written with:
/// $1,500 to start, $200 at GO and a $100 catch-up bonus, and no Casino coin flip. Tests check how a rule works,
/// so they shouldn't break each time the money is rebalanced (decision #143).
/// <c>RulesConfigTests</c> checks the real numbers in rules.json.
/// </summary>
public static class TestContent
{
    public static readonly GameContent ClassicMoney = With(new()
    {
        ["startingCash"] = 1500,
        ["goSalary"] = 200,
        ["catchUpBonus"] = 100,
        // The Casino coin flip (decision #153) rolls a die on landing, which would shift every scripted die after it.
        // CasinoTests turns it back on to test it.
        ["casinoLandingFlip"] = 0,
    });

    /// <summary>A copy of the default content with some rules.json numbers changed.</summary>
    public static GameContent With(Dictionary<string, JsonNode?> changes)
    {
        var rules = JsonSerializer.SerializeToNode(GameContent.Default.Rules, ContentLoader.JsonOptions)!.AsObject();
        foreach (var (name, value) in changes)
            rules[name] = value;
        return GameContent.Default.WithRules(rules.Deserialize<RulesConfig>(ContentLoader.JsonOptions)!);
    }
}
