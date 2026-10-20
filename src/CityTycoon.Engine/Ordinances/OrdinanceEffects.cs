using CityTycoon.Engine.Content;
using CityTycoon.Engine.Events;
using CityTycoon.Engine.Modifiers;
using CityTycoon.Engine.State;
using CityTycoon.Engine.Steps;

namespace CityTycoon.Engine.Ordinances;

/// <summary>
/// Every ordinance effect type, in one explicit list (the same approach as the card
/// effects: a hand-written list is safe for the browser's code trimming).
/// </summary>
public static class OrdinanceEffects
{
    private static readonly IOrdinanceEffect[] All =
    [
        new RezoneOrdinance(),
        new AddPermitsOrdinance(),
        new BuildingMoratoriumOrdinance(),
        new DistrictRentOrdinance(),
        new RailroadRentOrdinance(),
        new TaxPerLevelOrdinance(),
        new InfrastructureBondOrdinance(),
        new CollectFromBankOrdinance(),
        new PreserveOrdinance(),
        new CasinoCapOrdinance(),
        new RevealAllTilesOrdinance(),
    ];

    public static IOrdinanceEffect? Find(string type) => All.FirstOrDefault(e => e.Type == type);

    /// <summary>Every active player in turn order, starting from the seat after the agenda setter would also work, but order doesn't matter here.</summary>
    public static IEnumerable<PlayerState> Payers(GameState state) => state.ActivePlayers;

    /// <summary>All districts, for ordinances that can target any of them.</summary>
    public static IReadOnlyList<string> AllDistricts(EngineContext ctx) => ctx.Content.Board.Districts.Select(d => d.Id).ToList();
}

/// <summary>Rezone Up (direction 1) and Downzone (direction -1).</summary>
public sealed class RezoneOrdinance : IOrdinanceEffect
{
    public string Type => "Rezone";

    public void Validate(OrdinanceDefinition o)
    {
        if (o.Int("direction") is not (1 or -1))
            throw new ContentException($"Ordinance '{o.Id}': \"direction\" must be 1 or -1.");
    }

    public IReadOnlyList<string> Districts(EngineContext ctx, OrdinanceDefinition o) =>
        OrdinanceRules.ChangeableDistricts(ctx.State, ctx.Content)
            .Where(id => (int)ctx.State.Zoning[id] + o.Int("direction") is >= 0 and <= (int)ZoningLevel.HighRise)
            .ToList();

    public void Apply(OrdinanceContext c) => OrdinanceRules.ChangeZoning(c.Ctx, c.DistrictId!, c.Ordinance.Int("direction"), c.Ordinance.Id);
}

/// <summary>Hotel and Skyscraper Permit Expansion: more permits in the citywide supply.</summary>
public sealed class AddPermitsOrdinance : IOrdinanceEffect
{
    public string Type => "AddPermits";

    public void Validate(OrdinanceDefinition o)
    {
        o.Int("hotel");
        o.Int("skyscraper");
    }

    public void Apply(OrdinanceContext c)
    {
        int hotels = c.Ordinance.Int("hotel"), skyscrapers = c.Ordinance.Int("skyscraper");
        c.Ctx.State.HotelPermits += hotels;
        c.Ctx.State.SkyscraperPermits += skyscrapers;
        c.Ctx.Emit(new PermitsAdded(hotels, skyscrapers));
    }
}

/// <summary>Building Moratorium: nobody can build anywhere while it lasts.</summary>
public sealed class BuildingMoratoriumOrdinance : IOrdinanceEffect
{
    public string Type => "BuildingMoratorium";

    public void Validate(OrdinanceDefinition o) => RequireDuration(o);

    public static void RequireDuration(OrdinanceDefinition o)
    {
        if (o.Duration is null or < 1)
            throw new ContentException($"Ordinance '{o.Id}' needs a \"duration\" of at least 1 round.");
    }

    public IRuleModifier Modifier(ActiveOrdinance a, OrdinanceDefinition o, GameContent content) => new MoratoriumModifier(o.Name);
}

/// <summary>Rent Control and Tourism Boost: a rent change in one district while it lasts.</summary>
public sealed class DistrictRentOrdinance : IOrdinanceEffect
{
    public string Type => "DistrictRent";

    public void Validate(OrdinanceDefinition o)
    {
        BuildingMoratoriumOrdinance.RequireDuration(o);
        o.Int("percent");
        if (o.Target != OrdinanceTarget.District)
            throw new ContentException($"Ordinance '{o.Id}' changes one district's rent, so it needs \"target\": \"district\".");
    }

    public IReadOnlyList<string> Districts(EngineContext ctx, OrdinanceDefinition o) => OrdinanceEffects.AllDistricts(ctx);

    public IRuleModifier Modifier(ActiveOrdinance a, OrdinanceDefinition o, GameContent content) =>
        new RentOrdinanceModifier(o.Name, o.Int("percent"), a.DistrictId, null);
}

/// <summary>Transit Investment: railroad rents change while it lasts.</summary>
public sealed class RailroadRentOrdinance : IOrdinanceEffect
{
    public string Type => "RailroadRent";

    public void Validate(OrdinanceDefinition o)
    {
        BuildingMoratoriumOrdinance.RequireDuration(o);
        o.Int("percent");
    }

    public IRuleModifier Modifier(ActiveOrdinance a, OrdinanceDefinition o, GameContent content) =>
        new RentOrdinanceModifier(o.Name, o.Int("percent"), null, content);
}

/// <summary>Property Tax Hike: every player pays per building level they own, right away.</summary>
public sealed class TaxPerLevelOrdinance : IOrdinanceEffect
{
    public string Type => "TaxPerLevel";

    public void Validate(OrdinanceDefinition o) => o.Int("perLevel");

    // A tax on nobody's buildings would do nothing, so it can't go to a vote until someone has built.
    public bool CanPick(EngineContext ctx, OrdinanceDefinition o) => ctx.State.Properties.Values.Any(p => p.OwnerId != null && p.Level > 0);

    public void Apply(OrdinanceContext c)
    {
        foreach (PlayerState player in OrdinanceEffects.Payers(c.Ctx.State))
        {
            int levels = c.Ctx.State.Properties.Values.Where(p => p.OwnerId == player.Id).Sum(p => p.Level);
            if (levels > 0)
                CouncilPayments.Charge(c.Ctx, player.Id, levels * c.Ordinance.Int("perLevel"), c.Ordinance.Id);
        }
    }
}

/// <summary>Infrastructure Bond: everyone pays, and the lowest-zoned district goes up a level.</summary>
public sealed class InfrastructureBondOrdinance : IOrdinanceEffect
{
    public string Type => "InfrastructureBond";

    public void Validate(OrdinanceDefinition o) => o.Int("amount");

    // The districts tied for lowest zoning that can still go up.
    public static IReadOnlyList<string> Candidates(EngineContext ctx)
    {
        var open = OrdinanceRules.ChangeableDistricts(ctx.State, ctx.Content)
            .Where(id => ctx.State.Zoning[id] < ZoningLevel.HighRise).ToList();
        if (open.Count == 0)
            return [];
        ZoningLevel lowest = open.Min(id => ctx.State.Zoning[id]);
        return open.Where(id => ctx.State.Zoning[id] == lowest).ToList();
    }

    public bool CanPick(EngineContext ctx, OrdinanceDefinition o) => Candidates(ctx).Count > 0;

    public void Apply(OrdinanceContext c)
    {
        IReadOnlyList<string> candidates = Candidates(c.Ctx);
        if (candidates.Count == 1)
            OrdinanceRules.ChangeZoning(c.Ctx, candidates[0], 1, c.Ordinance.Id);
        else if (candidates.Count > 1)
            c.Ctx.Push(new PickDistrictStep { PlayerId = c.SetterId, OrdinanceId = c.Ordinance.Id, Districts = candidates.ToList() });
        foreach (PlayerState player in OrdinanceEffects.Payers(c.Ctx.State))
            CouncilPayments.Charge(c.Ctx, player.Id, c.Ordinance.Int("amount"), c.Ordinance.Id);
    }
}

/// <summary>Stimulus Check: every player collects money from the bank.</summary>
public sealed class CollectFromBankOrdinance : IOrdinanceEffect
{
    public string Type => "CollectFromBank";

    public void Validate(OrdinanceDefinition o) => o.Int("amount");

    public void Apply(OrdinanceContext c)
    {
        int amount = c.Ordinance.Int("amount");
        foreach (PlayerState player in OrdinanceEffects.Payers(c.Ctx.State))
        {
            player.Cash += amount;
            c.Ctx.Emit(new CouncilPayout(player.Id, amount, c.Ordinance.Id));
        }
    }
}

/// <summary>Historic Preservation: a district's zoning can never change again. (The ordinance staying on the table is what freezes it.)</summary>
public sealed class PreserveOrdinance : IOrdinanceEffect
{
    public string Type => "Preserve";

    public void Validate(OrdinanceDefinition o)
    {
        if (!o.Lasting || o.Target != OrdinanceTarget.District)
            throw new ContentException($"Ordinance '{o.Id}' freezes a district, so it needs \"lasting\": true and \"target\": \"district\".");
    }

    public IReadOnlyList<string> Districts(EngineContext ctx, OrdinanceDefinition o) =>
        OrdinanceRules.ChangeableDistricts(ctx.State, ctx.Content).ToList();
}

/// <summary>Casino Regulation: Casino bets and Dice Duel stakes are capped while it lasts.</summary>
public sealed class CasinoCapOrdinance : IOrdinanceEffect
{
    public string Type => "CasinoCap";

    public void Validate(OrdinanceDefinition o)
    {
        BuildingMoratoriumOrdinance.RequireDuration(o);
        o.Int("amount");
    }

    public IRuleModifier Modifier(ActiveOrdinance a, OrdinanceDefinition o, GameContent content) => new CasinoCapModifier(o.Name, o.Int("amount"));
}

/// <summary>Public Works: every hidden Government tile is revealed.</summary>
public sealed class RevealAllTilesOrdinance : IOrdinanceEffect
{
    public string Type => "RevealAllTiles";

    public bool CanPick(EngineContext ctx, OrdinanceDefinition o) =>
        ctx.State.Board.Any(s => s.Kind == SpaceKind.GovernmentTile && !s.IsRevealed);

    public void Apply(OrdinanceContext c)
    {
        foreach (BoardSpace space in c.Ctx.State.Board.Where(s => s.Kind == SpaceKind.GovernmentTile && !s.IsRevealed))
        {
            space.IsRevealed = true;
            c.Ctx.Emit(new TileRevealedByCouncil(space.Id, space.TileId!));
        }
    }
}

/// <summary>Small helper so ordinance payments look the same everywhere.</summary>
public static class CouncilPayments
{
    public static void Charge(EngineContext ctx, int playerId, int amount, string ordinanceId) =>
        ctx.Push(new PaymentStep { FromId = playerId, Amount = amount, Reason = PaymentReason.Council, OrdinanceId = ordinanceId });
}

// ---------------------------------------------------------- rule modifiers made by active ordinances

/// <summary>Building Moratorium.</summary>
public sealed class MoratoriumModifier(string source) : IRuleModifier
{
    public string Source => source;
    public string? BuildingBlockedReason(GameState state, string propertyId) => $"The {source} is in force: nobody can build.";
}

/// <summary>Rent Control, Tourism Boost and Transit Investment: a rent change for one district, or for all railroads.</summary>
public sealed class RentOrdinanceModifier(string source, int percent, string? districtId, GameContent? railroadsOnly) : IRuleModifier
{
    public string Source => source;

    public int RentPercent(GameState state, string propertyId)
    {
        if (railroadsOnly != null)
            return railroadsOnly.Property(propertyId).Type == PropertyType.Railroad ? percent : 0;
        return state.Space(propertyId).DistrictId == districtId ? percent : 0;
    }
}

/// <summary>Casino Regulation.</summary>
public sealed class CasinoCapModifier(string source, int cap) : IRuleModifier
{
    public string Source => source;
    public int? MaxCasinoStake(GameState state) => cap;
}
