using CityTycoon.Engine.Content;
using CityTycoon.Engine.State;

namespace CityTycoon.Engine.Modifiers;

// One small class per character ability (Section 5). Each overrides just the
// hook it changes, and only does anything for its owner. Characters are
// described in content/characters.json, which names these classes by "type".

/// <summary>The Mogul: bids count as higher when compared. They still pay the real bid.</summary>
public sealed class BidBonusModifier(ModifierContext c) : OwnedModifier(c), IRuleModifier
{
    public int BidBonus(GameState s, int playerId) => Mine(playerId) ? Value : 0;
}

/// <summary>The Mogul: wins ties when two players roll dice against each other.</summary>
public sealed class DiceTieBreakerModifier(ModifierContext c) : OwnedModifier(c), IRuleModifier
{
    public bool WinsDiceTies(GameState s, int playerId) => Mine(playerId);
}

/// <summary>The Hustler: one reroll per Casino visit.</summary>
public sealed class CasinoRerollModifier(ModifierContext c) : OwnedModifier(c), IRuleModifier
{
    public bool CasinoReroll(GameState s, int playerId) => Mine(playerId);
}

/// <summary>The Commuter (and Commuter Belt): room for more Momentum tokens.</summary>
public sealed class MomentumCapModifier(ModifierContext c) : OwnedModifier(c), IRuleModifier
{
    public int MomentumCapBonus(GameState s, int playerId) => Mine(playerId) ? Value : 0;
}

/// <summary>The Commuter: starts with more tokens.</summary>
public sealed class StartingMomentumModifier(ModifierContext c) : OwnedModifier(c), IRuleModifier
{
    public int StartingMomentum(GameState s, int playerId) => Mine(playerId) ? Value : 0;
}

/// <summary>The Lawyer: leaving Jail is free.</summary>
public sealed class FreeBailModifier(ModifierContext c) : OwnedModifier(c), IRuleModifier
{
    public bool FreeBail(GameState s, int playerId) => Mine(playerId);
}

/// <summary>The Lawyer: one named card (Audit) can't be played against them.</summary>
public sealed class ImmuneToCardModifier(ModifierContext c) : OwnedModifier(c), IRuleModifier
{
    public bool CanPlayerBeTargeted(GameState s, int playerId, CardDefinition card) =>
        !(Mine(playerId) && card.Id == Context.Spec.Card);
}

/// <summary>The Contractor: a percentage off every building level on their properties.</summary>
public sealed class BuildDiscountModifier(ModifierContext c) : OwnedModifier(c), IRuleModifier
{
    public int BuildDiscountPercent(GameState s, string propertyId) =>
        s.Properties[propertyId].OwnerId == Owner ? Value : 0;
}

/// <summary>The Politician: a bigger vote in the City Council (the Council arrives in Phase 8).</summary>
public sealed class VoteInfluenceModifier(ModifierContext c) : OwnedModifier(c), IRuleModifier
{
    public int VoteInfluence(GameState s, int playerId) => Mine(playerId) ? Value : 0;
}

/// <summary>The City Planner: peeks at hidden Government tiles when the game starts.</summary>
public sealed class PeekAtStartModifier(ModifierContext c) : OwnedModifier(c), IRuleModifier
{
    public int StartPeeks(GameState s, int playerId) => Mine(playerId) ? Value : 0;
}

/// <summary>The City Planner: peeks at more hidden tiles each time they pass GO.</summary>
public sealed class PeekOnGoModifier(ModifierContext c) : OwnedModifier(c), IRuleModifier
{
    public int GoPeeks(GameState s, int playerId) => Mine(playerId) ? Value : 0;
}

/// <summary>The Medic: the Hospital never bills or slows them down.</summary>
public sealed class HospitalImmunityModifier(ModifierContext c) : OwnedModifier(c), IRuleModifier
{
    public bool ShrugsOffHospital(GameState s, int playerId) => Mine(playerId);
}

/// <summary>The Medic: Insured markers cost less.</summary>
public sealed class InsuredMarkerCostModifier(ModifierContext c) : OwnedModifier(c), IRuleModifier
{
    public int InsuredMarkerCost(GameState s, int playerId) => Mine(playerId) ? Value : 0;
}
