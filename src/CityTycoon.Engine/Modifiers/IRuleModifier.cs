using CityTycoon.Engine.Content;
using CityTycoon.Engine.State;
using CityTycoon.Engine.Tiles;

namespace CityTycoon.Engine.Modifiers;

/// <summary>
/// Something that changes a rule (Section 20.6): a revealed Government tile
/// now, and later characters, color perks and ordinances.
///
/// The engine asks every active modifier at fixed "hook points" (rent, build
/// cost, card targeting, Fire!) instead of checking for each tile or
/// character by name. So a new modifier is a new class, and the rent, build
/// and card code never has to change. The methods with bodies are the
/// "no change" answers: a modifier only overrides the hooks it cares about.
/// </summary>
public interface IRuleModifier
{
    /// <summary>What to call this modifier in a rent breakdown, like "Landfill next door".</summary>
    string Source { get; }

    /// <summary>A percentage added to this property's rent (−50 means 50% less). Section 13.1, step 2.</summary>
    int RentPercent(GameState state, string propertyId) => 0;

    /// <summary>A percentage taken off the cost of building on this property. Section 9.4.</summary>
    int BuildDiscountPercent(GameState state, string propertyId) => 0;

    /// <summary>False if Opportunity cards (and Hostile Takeover) may not target this property.</summary>
    bool CanBeTargetedByCards(GameState state, string propertyId) => true;

    /// <summary>True if the Fire! Event can't touch this property.</summary>
    bool ProtectsFromFire(GameState state, string propertyId) => false;
}

/// <summary>Gathers the active modifiers and adds up their answers.</summary>
public static class RuleModifiers
{
    /// <summary>
    /// Every modifier in play right now. Only revealed tiles count: a hidden
    /// tile has no effect at all (Section 12.1). Characters, perks and
    /// ordinances join this list in later phases.
    /// </summary>
    public static List<IRuleModifier> Active(GameState state, GameContent content) =>
        state.Board
            .Where(s => s.Kind == SpaceKind.GovernmentTile && s.IsRevealed)
            .Select(s => (IRuleModifier)new TileModifier(state, s, content.GovTile(s.TileId!)))
            .ToList();

    /// <summary>Each modifier that changes this property's rent, with its percentage, for the rent breakdown.</summary>
    /// <param name="active">The active modifiers, if the caller already gathered them (saves work when asking about many properties).</param>
    public static List<(string Source, int Percent)> RentPercents(GameState state, GameContent content, string propertyId,
        IReadOnlyList<IRuleModifier>? active = null) =>
        (active ?? Active(state, content))
            .Select(m => (m.Source, Percent: m.RentPercent(state, propertyId)))
            .Where(x => x.Percent != 0)
            .ToList();

    public static int BuildDiscountPercent(GameState state, GameContent content, string propertyId,
        IReadOnlyList<IRuleModifier>? active = null) =>
        (active ?? Active(state, content)).Sum(m => m.BuildDiscountPercent(state, propertyId));

    public static bool CanBeTargetedByCards(GameState state, GameContent content, string propertyId) =>
        Active(state, content).All(m => m.CanBeTargetedByCards(state, propertyId));

    public static bool IsFireSafe(GameState state, GameContent content, string propertyId) =>
        Active(state, content).Any(m => m.ProtectsFromFire(state, propertyId));
}
