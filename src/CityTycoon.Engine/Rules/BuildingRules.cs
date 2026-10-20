using CityTycoon.Engine.Content;
using CityTycoon.Engine.Events;
using CityTycoon.Engine.State;
using CityTycoon.Engine.Steps;

namespace CityTycoon.Engine.Rules;

/// <summary>
/// Building and selling houses, hotels and skyscrapers (Section 9), with
/// zoning (Section 10.1) and permits (Section 10.3).
/// </summary>
public static class BuildingRules
{
    /// <summary>The full price of one building level, before discounts (Section 9.1).</summary>
    public static int LevelCost(GameContent content, PropertyDefinition def, int level) =>
        level == content.Rules.SkyscraperLevel ? def.HouseCost * content.Rules.SkyscraperCostMultiplier : def.HouseCost;

    /// <summary>
    /// Applies a building discount (Section 9.4): discounts add up to at most
    /// 75% off, and the result is rounded down to the nearest $5.
    /// Characters, tiles and cards that give discounts arrive in later phases.
    /// </summary>
    public static int ApplyDiscount(RulesConfig rules, int cost, int discountPercent)
    {
        int percent = Math.Clamp(discountPercent, 0, rules.MaxBuildDiscountPercent);
        int discounted = cost * (100 - percent) / 100;
        return discounted / rules.BuildDiscountRoundTo * rules.BuildDiscountRoundTo;
    }

    /// <summary>What the next building level on this property would cost.</summary>
    public static int NextLevelCost(EngineContext ctx, string propertyId)
    {
        PropertyDefinition def = ctx.Content.Property(propertyId);
        int newLevel = ctx.State.Properties[propertyId].Level + 1;
        return ApplyDiscount(ctx.Rules, LevelCost(ctx.Content, def, newLevel), discountPercent: 0);
    }

    /// <summary>The tallest building the property's district allows right now.</summary>
    public static int ZoningCap(EngineContext ctx, string propertyId)
    {
        string district = ctx.State.Space(propertyId).DistrictId!;
        return ctx.Rules.MaxBuildingLevel(ctx.State.Zoning[district]);
    }

    /// <summary>
    /// Checks every building requirement from Section 9.2 except "is it your
    /// turn" (the decision decides that). Returns null if building is allowed,
    /// or a plain-English reason if not, so the screen can explain it.
    /// </summary>
    public static string? WhyCantBuild(EngineContext ctx, int playerId, string propertyId)
    {
        GameState state = ctx.State;
        PropertyDefinition def = ctx.Content.Property(propertyId);
        PropertyState owned = state.Properties[propertyId];
        PlayerState player = state.Player(playerId);

        if (def.Type != PropertyType.Color)
            return "You can only build on color properties.";
        if (player.InJail)
            return "You can't build while you're in Jail.";

        // Rule 1: own the whole group, with none of it mortgaged or pending.
        var group = ctx.Content.GroupMembers(def.Group!).Select(id => state.Properties[id]).ToList();
        if (!group.All(p => p.IsOwnedOutright(playerId)))
            return "You need to own the whole color group, with nothing pending.";
        if (group.Any(p => p.IsMortgaged))
            return "Part of this group is mortgaged.";

        int newLevel = owned.Level + 1;
        if (newLevel > ctx.Rules.SkyscraperLevel)
            return "This property is already as tall as it can go.";

        // Rule 3: the even-build rule. You build on the lowest property in the group first.
        if (owned.Level > group.Min(p => p.Level))
            return "Even-build rule: build on the other properties in this group first.";

        // Rule 4: zoning.
        if (newLevel > ZoningCap(ctx, propertyId))
            return "This district's zoning doesn't allow a building that tall.";

        // Rule 5: permits.
        if (newLevel == ctx.Rules.HotelLevel && state.HotelPermits <= 0)
            return "No hotel permits are left.";
        if (newLevel == ctx.Rules.SkyscraperLevel && state.SkyscraperPermits <= 0)
            return "No skyscraper permits are left.";

        // Rule 6: the build limit.
        if (player.BuildsThisTurn >= ctx.Rules.BuildLimitPerTurn)
            return $"You've already built {ctx.Rules.BuildLimitPerTurn} levels this turn.";

        // Rule 8: skyscrapers need a hotel on every property in the group.
        if (newLevel == ctx.Rules.SkyscraperLevel && group.Any(p => p.Level < ctx.Rules.HotelLevel))
            return "Every property in the group needs a hotel before you can build a skyscraper.";

        if (player.Cash < NextLevelCost(ctx, propertyId))
            return "You don't have enough cash.";

        return null;
    }

    public static void Build(EngineContext ctx, int playerId, string propertyId)
    {
        PropertyState owned = ctx.State.Properties[propertyId];
        PlayerState player = ctx.Player(playerId);
        int cost = NextLevelCost(ctx, propertyId);

        player.Cash -= cost;
        player.BuildsThisTurn++;
        owned.Level++;

        // A hotel takes a hotel permit. A skyscraper takes a skyscraper permit
        // and gives the hotel permit back, because the hotel is replaced.
        if (owned.Level == ctx.Rules.HotelLevel)
        {
            ctx.State.HotelPermits--;
        }
        else if (owned.Level == ctx.Rules.SkyscraperLevel)
        {
            ctx.State.SkyscraperPermits--;
            ctx.State.HotelPermits++;
        }

        ctx.Emit(new BuildingBuilt(playerId, propertyId, owned.Level, cost));
    }

    /// <summary>
    /// Selling follows the even-build rule too: sell from the tallest
    /// property in the group first (Section 9.3).
    /// </summary>
    public static string? WhyCantSell(EngineContext ctx, int playerId, string propertyId)
    {
        PropertyState owned = ctx.State.Properties[propertyId];
        if (owned.OwnerId != playerId)
            return "You don't own this property.";
        if (owned.Level == 0)
            return "There are no buildings to sell.";
        string group = ctx.Content.Property(propertyId).Group!;
        int tallest = ctx.Content.GroupMembers(group).Max(id => ctx.State.Properties[id].Level);
        if (owned.Level < tallest)
            return "Even-build rule: sell from the other properties in this group first.";
        return null;
    }

    /// <summary>What selling one level gives back: half of what the level cost.</summary>
    public static int SellValue(GameContent content, PropertyDefinition def, int level) =>
        LevelCost(content, def, level) * content.Rules.BuildingSellPercent / 100;

    public static void Sell(EngineContext ctx, int playerId, string propertyId)
    {
        PropertyDefinition def = ctx.Content.Property(propertyId);
        PropertyState owned = ctx.State.Properties[propertyId];
        int refund = SellValue(ctx.Content, def, owned.Level);
        RulesConfig rules = ctx.Rules;

        if (owned.Level == rules.SkyscraperLevel)
        {
            // Back down to a hotel: the skyscraper permit returns, and a hotel permit
            // is needed again. If none is free, drop straight to 4 houses (Section 9.3).
            ctx.State.SkyscraperPermits++;
            if (ctx.State.HotelPermits > 0)
            {
                ctx.State.HotelPermits--;
                owned.Level--;
            }
            else
            {
                refund += SellValue(ctx.Content, def, rules.HotelLevel);
                owned.Level -= 2;
            }
        }
        else
        {
            if (owned.Level == rules.HotelLevel)
                ctx.State.HotelPermits++;
            owned.Level--;
        }

        ctx.Player(playerId).Cash += refund;
        ctx.Emit(new BuildingSold(playerId, propertyId, owned.Level, refund));
    }

    /// <summary>Sells every level on a property at once (bankruptcy), returning permits. Gives back the refund.</summary>
    public static int RemoveAllBuildings(EngineContext ctx, string propertyId)
    {
        PropertyDefinition def = ctx.Content.Property(propertyId);
        PropertyState owned = ctx.State.Properties[propertyId];
        int refund = 0;
        for (int level = owned.Level; level > 0; level--)
            refund += SellValue(ctx.Content, def, level);
        if (owned.Level == ctx.Rules.SkyscraperLevel)
            ctx.State.SkyscraperPermits++;
        else if (owned.Level == ctx.Rules.HotelLevel)
            ctx.State.HotelPermits++;
        owned.Level = 0;
        return refund;
    }

    /// <summary>The cash every building on a property would bring if all of it were sold.</summary>
    public static int SellAllValue(GameContent content, PropertyDefinition def, int level)
    {
        int total = 0;
        for (int l = level; l > 0; l--)
            total += SellValue(content, def, l);
        return total;
    }
}
