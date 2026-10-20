using CityTycoon.Engine.Content;
using CityTycoon.Engine.State;

namespace CityTycoon.Engine.Modifiers;

// One small class per railroad or utility bonus (Sections 13.3 and 13.4).
// They are described in content/bonuses.json.

/// <summary>2 railroads, Express Line: pay to jump to any of your railroads, then roll 1 die.</summary>
public sealed class ExpressLineModifier(ModifierContext c) : OwnedModifier(c), IRuleModifier
{
    public int ExpressLineCost(GameState s, int playerId) => Mine(playerId) ? Value : 0;
}

/// <summary>3 railroads, Toll Collector: a Momentum token whenever someone pays you railroad rent.</summary>
public sealed class TollCollectorModifier(ModifierContext c) : OwnedModifier(c), IRuleModifier
{
    public int MomentumFromRent(GameState s, int ownerId, PropertyType type) =>
        Mine(ownerId) && type == PropertyType.Railroad ? Value : 0;
}

/// <summary>4 railroads, Rail Baron: Precision Rolls cost fewer tokens.</summary>
public sealed class PrecisionRollDiscountModifier(ModifierContext c) : OwnedModifier(c), IRuleModifier
{
    public int PrecisionRollDiscount(GameState s, int playerId) => Mine(playerId) ? Value : 0;
}

/// <summary>Both utilities, Utility Bill: opponents pay you when they pass GO.</summary>
public sealed class UtilityBillModifier(ModifierContext c) : OwnedModifier(c), IRuleModifier
{
    public int GoToll(GameState s, int passerId) => Mine(passerId) ? 0 : Value;
}

/// <summary>Both utilities, Power Grid: opponents pay you each time they build a level.</summary>
public sealed class PowerGridModifier(ModifierContext c) : OwnedModifier(c), IRuleModifier
{
    public int BuildFee(GameState s, int builderId) => Mine(builderId) ? 0 : Value;
}
