using CityTycoon.Engine.Content;
using CityTycoon.Engine.State;

namespace CityTycoon.Engine.Rules;

/// <summary>
/// Works out rent (Section 13.1). So far: base rent (with the full-set bonus,
/// railroads and utilities), the Stadium's ×2, and half rent while the owner
/// is in Jail. Percentage modifiers and cards arrive in later phases.
/// </summary>
public static class RentCalculator
{
    /// <param name="diceTotal">The dice total that brought the payer here (utilities use it).</param>
    public static int Calculate(GameState state, GameContent content, string propertyId, int diceTotal)
    {
        PropertyState owned = state.Properties[propertyId];
        if (owned.OwnerId is not int ownerId || owned.IsMortgaged || owned.IsPending)
            return 0; // Unowned, mortgaged and pending properties collect no rent.

        PropertyDefinition def = content.Property(propertyId);
        int rent = def.Type switch
        {
            PropertyType.Color => ColorRent(state, content, def, owned, ownerId),
            PropertyType.Railroad => content.Rules.RailroadRents[CountOwned(state, content, ownerId, PropertyType.Railroad) - 1],
            PropertyType.Utility => content.Rules.UtilityRentMultipliers[CountOwned(state, content, ownerId, PropertyType.Utility) - 1] * diceTotal,
            _ => 0,
        };
        if (rent <= 0)
            return 0;

        // Step 3: multipliers. A Stadium boost doubles a color group's rent.
        // Two boosts on the same group still only double it (docs/DECISIONS.md).
        double total = rent;
        if (def.Group != null && state.StadiumBoosts.Any(b => b.GroupId == def.Group))
            total *= content.Rules.StadiumRentMultiplier;

        // Step 4: frozen assets. The owner is in Jail, so rent is halved.
        if (state.Player(ownerId).InJail)
            total *= content.Rules.JailRentMultiplier;

        // Step 6: round down, but never below $1.
        return Math.Max(1, (int)Math.Floor(total));
    }

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
