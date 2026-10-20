using CityTycoon.Engine.Cards;
using CityTycoon.Engine.Content;
using CityTycoon.Engine.Events;
using CityTycoon.Engine.Rules;
using CityTycoon.Engine.State;
using CityTycoon.Engine.Steps;

namespace CityTycoon.Engine.Tiles;

/// <summary>
/// One kind of Government tile landing effect, like "PayBank". The same idea
/// as card effects: a tile names a type plus settings in govtiles.json, so a
/// new tile that reuses a type needs no new code.
/// </summary>
public interface ITileLanding
{
    string Type { get; }

    /// <summary>Checks the tile's JSON has the settings this effect needs. Throws a ContentException if not.</summary>
    void Validate(GovTileDefinition tile) { }

    void Apply(TileLandingContext c);
}

/// <summary>Everything a landing effect needs while it runs.</summary>
public sealed record TileLandingContext(EngineContext Ctx, PlayerState Player, GovTileDefinition Tile, BoardSpace Space)
{
    public GameState State => Ctx.State;
}

/// <summary>The list of every landing effect type. Written out by hand so Blazor's trimming can't remove one.</summary>
public static class TileLandings
{
    private static readonly Dictionary<string, ITileLanding> All = new ITileLanding[]
    {
        new PayBankLanding(),
        new GainMomentumLanding(),
        new DrawOpportunityLanding(),
        new PayPerLevelLanding(),
        new PayOwnerOfAllLanding(),
        new RideToRailroadLanding(),
    }.ToDictionary(e => e.Type);

    public static ITileLanding? Find(string type) => All.GetValueOrDefault(type);

    /// <summary>A tile fee: pays the bank (toId null) or a player, raising money if needed.</summary>
    public static void Charge(TileLandingContext c, int amount, int? toId = null)
    {
        if (amount > 0)
            c.Ctx.Push(new PaymentStep { FromId = c.Player.Id, ToId = toId, Amount = amount, Reason = PaymentReason.Tile, TileId = c.Tile.Id });
    }
}

/// <summary>Police Station and Landfill: pay a flat fee to the bank.</summary>
public sealed class PayBankLanding : ITileLanding
{
    public string Type => "PayBank";
    public void Validate(GovTileDefinition tile) => tile.Int("amount");
    public void Apply(TileLandingContext c) => TileLandings.Charge(c, c.Tile.Int("amount"));
}

/// <summary>Fire Station: gain Momentum tokens.</summary>
public sealed class GainMomentumLanding : ITileLanding
{
    public string Type => "GainMomentum";
    public void Validate(GovTileDefinition tile) => tile.Int("amount");
    public void Apply(TileLandingContext c) => MomentumRules.Gain(c.Ctx, c.Player, c.Tile.Int("amount"), c.Tile.Name);
}

/// <summary>School: draw an Opportunity card.</summary>
public sealed class DrawOpportunityLanding : ITileLanding
{
    public string Type => "DrawOpportunity";
    public void Apply(TileLandingContext c) => CardRules.DrawOpportunity(c.Ctx, c.Player, c.Tile.Name);
}

/// <summary>
/// City Hall: pay for every building level you own (a hotel is 5 levels),
/// then an Emergency Council session. The Council arrives in Phase 8.
/// </summary>
public sealed class PayPerLevelLanding : ITileLanding
{
    public string Type => "PayPerLevel";
    public void Validate(GovTileDefinition tile) => tile.Int("perLevel");

    public void Apply(TileLandingContext c)
    {
        int levels = c.State.Properties.Values.Where(p => p.OwnerId == c.Player.Id).Sum(p => p.Level);
        if (c.Tile.Flag("emergencySession"))
            c.Ctx.Emit(new TileNotReadyYet(c.Tile.Id));
        TileLandings.Charge(c, levels * c.Tile.Int("perLevel"));
    }
}

/// <summary>
/// Power Plant: pay the player who owns every property of a type (both
/// utilities), or the bank if nobody does. If that's you, you pay nothing.
/// </summary>
public sealed class PayOwnerOfAllLanding : ITileLanding
{
    public string Type => "PayOwnerOfAll";

    public void Validate(GovTileDefinition tile)
    {
        tile.Int("amount");
        _ = TypeOf(tile);
    }

    public void Apply(TileLandingContext c)
    {
        PropertyType type = TypeOf(c.Tile);
        var all = c.State.Properties.Values.Where(p => c.Ctx.Content.Property(p.PropertyId).Type == type).ToList();
        int? owner = all.Select(p => p.OwnerId).Distinct().Count() == 1 ? all[0].OwnerId : null;
        if (owner == c.Player.Id)
            return;
        TileLandings.Charge(c, c.Tile.Int("amount"), owner);
    }

    private static PropertyType TypeOf(GovTileDefinition tile) =>
        Enum.TryParse(tile.Setting("propertyType"), ignoreCase: true, out PropertyType type) ? type
        : throw new ContentException($"Government tile '{tile.Id}': \"propertyType\" must be color, railroad or utility.");
}

/// <summary>Transit Hub: you may pay a fare to move to any railroad.</summary>
public sealed class RideToRailroadLanding : ITileLanding
{
    public string Type => "RideToRailroad";
    public void Validate(GovTileDefinition tile) => tile.Int("cost");

    public void Apply(TileLandingContext c)
    {
        if (c.Player.Cash >= c.Tile.Int("cost"))
            c.Ctx.Push(new TransitRideStep { PlayerId = c.Player.Id, Cost = c.Tile.Int("cost") });
    }
}
