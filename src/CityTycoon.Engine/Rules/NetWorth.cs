using CityTycoon.Engine.Content;
using CityTycoon.Engine.Modifiers;
using CityTycoon.Engine.State;

namespace CityTycoon.Engine.Rules;

/// <summary>The score (Section 16.3). Public, and shown live for every player.</summary>
public static class NetWorth
{
    /// <param name="active">The active modifiers, if the caller already gathered them (saves work when scoring many players).</param>
    public static int Calculate(GameState state, GameContent content, int playerId, IReadOnlyList<IRuleModifier>? active = null)
    {
        PlayerState player = state.Player(playerId);
        if (player.IsBankrupt)
            return 0;

        int total = player.Cash;
        foreach (PropertyState owned in state.Properties.Values.Where(p => p.OwnerId == playerId))
        {
            PropertyDefinition def = content.Property(owned.PropertyId);
            total += PropertyValue(content, def, owned);
            total += BuildingValue(content, def, owned.Level);
        }
        // Each unpaid loan counts as what it costs this player to repay (less with the Yellow perk).
        if (player.Loans > 0)
            total -= player.Loans * RuleModifiers.LoanRepayCost(state, content, playerId, active);
        return total;
    }

    /// <summary>
    /// What a property adds to net worth: its assessed value, minus the mortgage
    /// if mortgaged, minus the unpaid balance if pending (Section 16.3).
    /// </summary>
    public static int PropertyValue(GameContent content, PropertyDefinition def, PropertyState owned)
    {
        if (owned.IsMortgaged)
            return def.AssessedValue - MortgageValue(content, def);
        if (owned.IsPending)
            return def.AssessedValue - owned.PendingBalance;
        return def.AssessedValue;
    }

    public static int MortgageValue(GameContent content, PropertyDefinition def) =>
        def.AssessedValue * content.Rules.MortgagePercent / 100;

    /// <summary>The full cost of every building level on a property (Section 9.1).</summary>
    public static int BuildingValue(GameContent content, PropertyDefinition def, int level)
    {
        int value = 0;
        for (int l = 1; l <= level; l++)
            value += BuildingRules.LevelCost(content, def, l);
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
