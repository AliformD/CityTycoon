using CityTycoon.Engine.Content;
using CityTycoon.Engine.State;

namespace CityTycoon.Engine.Rules;

/// <summary>The score (Section 16.3). Public, and shown live for every player.</summary>
public static class NetWorth
{
    public static int Calculate(GameState state, GameContent content, int playerId)
    {
        PlayerState player = state.Player(playerId);
        if (player.IsBankrupt)
            return 0;

        int total = player.Cash;
        foreach (PropertyState owned in state.Properties.Values.Where(p => p.OwnerId == playerId))
        {
            PropertyDefinition def = content.Property(owned.PropertyId);
            total += owned.IsMortgaged ? def.AssessedValue - MortgageValue(content, def) : def.AssessedValue;
            total += BuildingValue(content, def, owned.Level);
        }
        return total;
    }

    public static int MortgageValue(GameContent content, PropertyDefinition def) =>
        def.AssessedValue * content.Rules.MortgagePercent / 100;

    /// <summary>The full cost of every building level on a property (Section 9.1).</summary>
    public static int BuildingValue(GameContent content, PropertyDefinition def, int level)
    {
        int value = 0;
        for (int l = 1; l <= level; l++)
            value += l == 6 ? def.HouseCost * content.Rules.SkyscraperCostMultiplier : def.HouseCost;
        return value;
    }

    /// <summary>
    /// The final scoreboard: highest net worth first, then most cash (Section 16.3).
    /// Bankrupt players go last.
    /// </summary>
    public static List<Standing> Standings(GameState state, GameContent content)
    {
        var rows = state.Players
            .Select(p => (Player: p, Worth: Calculate(state, content, p.Id)))
            .OrderBy(r => r.Player.IsBankrupt)
            .ThenByDescending(r => r.Worth)
            .ThenByDescending(r => r.Player.Cash)
            .ToList();

        var standings = new List<Standing>();
        for (int i = 0; i < rows.Count; i++)
        {
            // Players tied on both net worth and cash share a rank.
            bool tiedWithPrevious = i > 0
                && rows[i].Player.IsBankrupt == rows[i - 1].Player.IsBankrupt
                && rows[i].Worth == rows[i - 1].Worth
                && rows[i].Player.Cash == rows[i - 1].Player.Cash;
            int rank = tiedWithPrevious ? standings[i - 1].Rank : i + 1;
            standings.Add(new Standing(rows[i].Player.Id, rank, rows[i].Worth, rows[i].Player.Cash, rows[i].Player.IsBankrupt));
        }
        return standings;
    }
}
