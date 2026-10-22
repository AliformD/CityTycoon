using CityTycoon.Engine.Content;
using CityTycoon.Engine.Modifiers;
using CityTycoon.Engine.State;

namespace CityTycoon.Engine.Rules;

/// <summary>
/// Works out rent (Section 13.1), step by step: base rent (with the full-set
/// bonus, railroads and utilities), percentage changes from modifiers like
/// Government tiles, multipliers (Stadium, Grand Opening), and half rent while
/// the owner is in Jail. Rent Strike is applied by the payment itself.
///
/// <see cref="Explain"/> keeps a line for every step, so the screen can show
/// the breakdown; <see cref="Calculate"/> just returns the total.
/// </summary>
public static class RentCalculator
{
    /// <param name="diceTotal">The dice total that brought the payer here (utilities use it).</param>
    public static int Calculate(GameState state, GameContent content, string propertyId, int diceTotal) =>
        Explain(state, content, propertyId, diceTotal).Total;

    /// <summary>The rent and how it was worked out, one line per step.</summary>
    public static RentBreakdown Explain(GameState state, GameContent content, string propertyId, int diceTotal)
    {
        var lines = new List<RentLine>();
        PropertyState owned = state.Properties[propertyId];
        if (owned.OwnerId is not int ownerId)
            return new RentBreakdown(0, [new RentLine("Nobody owns it", "")]);
        if (owned.IsMortgaged)
            return new RentBreakdown(0, [new RentLine("Mortgaged: no rent", "")]);
        if (owned.IsPending)
            return new RentBreakdown(0, [new RentLine("Pending: no rent until it's paid off", "")]);

        PropertyDefinition def = content.Property(propertyId);
        int rent = def.Type switch
        {
            PropertyType.Color => ColorRent(state, content, def, owned, ownerId),
            PropertyType.Railroad => content.Rules.RailroadRents[CountOwned(state, content, ownerId, PropertyType.Railroad) - 1],
            PropertyType.Utility => content.Rules.UtilityRentMultipliers[CountOwned(state, content, ownerId, PropertyType.Utility) - 1] * diceTotal,
            _ => 0,
        };
        if (rent <= 0)
            return new RentBreakdown(0, [new RentLine("No rent", "$0")]);
        lines.Add(new RentLine(BaseRentLabel(state, content, def, owned, ownerId, diceTotal), $"${rent}"));

        // Step 2: percentage changes (Government tiles so far) add up, within limits.
        // Dark Blue's Prime Real Estate ignores the negative ones (Section 13.1).
        double total = rent;
        var active = RuleModifiers.Active(state, content);
        var percents = RuleModifiers.RentPercents(state, content, propertyId, active);
        if (percents.Count > 0)
        {
            string? shield = active.FirstOrDefault(m => m.IgnoresNegativeRent(state, propertyId))?.Source;
            foreach (var (source, percent) in percents)
            {
                lines.Add(shield != null && percent < 0
                    ? new RentLine($"{source} (ignored: {shield})", "0%")
                    : new RentLine(source, $"{percent:+0;-0}%"));
            }
            total = total * (100 + RuleModifiers.NetRentPercent(state, content, propertyId, active)) / 100;
        }

        // Step 3: multipliers. A Stadium boost doubles a color group's rent (the Hustler's triples it).
        // Two boosts on the same group don't stack: the biggest one counts (docs/DECISIONS.md).
        var stadium = state.StadiumBoosts.Where(b => def.Group != null && b.GroupId == def.Group).ToList();
        if (stadium.Count > 0)
        {
            int times = stadium.Max(b => b.Multiplier > 0 ? b.Multiplier : content.Rules.StadiumRentMultiplier);
            total *= times;
            lines.Add(new RentLine("Stadium boost", $"×{times}"));
        }

        // A Grand Opening multiplies one property's rent. It adds to a Stadium boost.
        foreach (PropertyBoost boost in state.PropertyBoosts.Where(b => b.PropertyId == propertyId))
        {
            total *= boost.Multiplier;
            lines.Add(new RentLine("Grand Opening", $"×{boost.Multiplier}"));
        }

        // Step 4: frozen assets. The owner is in Jail, so rent is halved.
        if (state.Player(ownerId).InJail)
        {
            total *= content.Rules.JailRentMultiplier;
            lines.Add(new RentLine("Owner in Jail", $"×{content.Rules.JailRentMultiplier}"));
        }

        // Step 6: round down, but never below $1.
        return new RentBreakdown(Math.Max(1, (int)Math.Floor(total)), lines);
    }

    private static string BaseRentLabel(GameState state, GameContent content, PropertyDefinition def, PropertyState owned, int ownerId, int diceTotal) => def.Type switch
    {
        PropertyType.Railroad => $"Base rent ({CountOwned(state, content, ownerId, PropertyType.Railroad)} railroad(s) owned)",
        PropertyType.Utility => $"Base rent ({content.Rules.UtilityRentMultipliers[CountOwned(state, content, ownerId, PropertyType.Utility) - 1]} × dice total {diceTotal})",
        _ when owned.Level == 0 && OwnsFullGroup(state, content, ownerId, def.Group!) => "Base rent (full color set: doubled)",
        _ when owned.Level > 0 => $"Base rent with {Events.EventDescriber.LevelName(owned.Level)}",
        _ => "Base rent",
    };

    private static int ColorRent(GameState state, GameContent content, PropertyDefinition def, PropertyState owned, int ownerId)
    {
        int rent = def.Rents![owned.Level];
        // Full-set rent: base rent doubles when the owner holds the whole group (Section 8.7).
        if (owned.Level == 0 && OwnsFullGroup(state, content, ownerId, def.Group!))
            rent *= 2;
        return rent;
    }

    /// <summary>True when the player owns every property in the group and none of it is mortgaged or pending.</summary>
    public static bool OwnsFullGroup(GameState state, GameContent content, int playerId, string groupId) =>
        content.GroupMembers(groupId).All(id =>
            state.Properties[id].IsOwnedOutright(playerId) && !state.Properties[id].IsMortgaged);

    /// <summary>How many railroads or utilities a player owns. Pending ones don't count (Section 13.1).</summary>
    public static int CountOwned(GameState state, GameContent content, int playerId, PropertyType type) =>
        state.Properties.Values.Count(p => p.IsOwnedOutright(playerId) && content.Property(p.PropertyId).Type == type);
}

/// <summary>One line of a rent breakdown, like ("Landfill next door", "-50%").</summary>
public sealed record RentLine(string Label, string Change);

/// <summary>What the rent is, and every step that got it there (Section 13.1).</summary>
public sealed record RentBreakdown(int Total, IReadOnlyList<RentLine> Lines);
