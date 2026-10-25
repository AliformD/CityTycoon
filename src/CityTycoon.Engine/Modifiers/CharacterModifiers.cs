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

/// <summary>The Hustler: one reroll of their movement dice per lap (decision #152).</summary>
public sealed class MoveRerollModifier(ModifierContext c) : OwnedModifier(c), IRuleModifier
{
    public bool MoveReroll(GameState s, int playerId) => Mine(playerId);
}

/// <summary>The Hustler: a bigger Stadium multiplier on the group they pick.</summary>
public sealed class StadiumMultiplierModifier(ModifierContext c) : OwnedModifier(c), IRuleModifier
{
    public int StadiumMultiplier(GameState s, int playerId) => Mine(playerId) ? Value : 0;
}

/// <summary>The Hustler: a bigger free chip at the Casino (decisions #161 and #164).</summary>
public sealed class CasinoChipBonusModifier(ModifierContext c) : OwnedModifier(c), IRuleModifier
{
    public int CasinoChipBonus(GameState s, int playerId) => Mine(playerId) ? Value : 0;
}

/// <summary>The Commuter: a Nudge can reach further.</summary>
public sealed class NudgeRangeModifier(ModifierContext c) : OwnedModifier(c), IRuleModifier
{
    public int NudgeRangeBonus(GameState s, int playerId) => Mine(playerId) ? Value : 0;
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

/// <summary>The Lawyer: collects part of the GO salary when sent to Jail ("value" is the percent).</summary>
public sealed class JailGoShareModifier(ModifierContext c) : OwnedModifier(c), IRuleModifier
{
    public int JailGoSharePercent(GameState s, int playerId) => Mine(playerId) ? Value : 0;
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

/// <summary>A bigger vote in the City Council. No character uses it since decision #162 (every vote is equal), but content may.</summary>
public sealed class VoteInfluenceModifier(ModifierContext c) : OwnedModifier(c), IRuleModifier
{
    public int VoteInfluence(GameState s, int playerId) => Mine(playerId) ? Value : 0;
}

/// <summary>
/// The Politician: the only player who may lobby, and the one other players pay to vote their way
/// ("value" is the influence each dollar buys, in percent; decision #162).
/// </summary>
public sealed class VoteContractsModifier(ModifierContext c) : OwnedModifier(c), IRuleModifier
{
    public int ContractPercent(GameState s, int playerId) => Mine(playerId) ? Value : 0;
}

/// <summary>The Politician: landing on City Hall fills their Momentum tokens (decision #162).</summary>
public sealed class CityHallMomentumModifier(ModifierContext c) : OwnedModifier(c), IRuleModifier
{
    public bool RefillsMomentumAtCityHall(GameState s, int playerId) => Mine(playerId);
}

/// <summary>The City Planner: may reverse the result of one City Council vote per game (decision #152).</summary>
public sealed class ReverseVoteModifier(ModifierContext c) : OwnedModifier(c), IRuleModifier
{
    public bool CanReverseVote(GameState s, int playerId) => Mine(playerId);
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

/// <summary>The City Planner: pays no Government tile fees (decision #158).</summary>
public sealed class TileFeeImmunityModifier(ModifierContext c) : OwnedModifier(c), IRuleModifier
{
    public bool IgnoresTileFees(GameState s, int playerId) => Mine(playerId);
}

/// <summary>The City Planner: after a peek, may swap that hidden tile for one not in play (decision #158).</summary>
public sealed class SwapPeekedTileModifier(ModifierContext c) : OwnedModifier(c), IRuleModifier
{
    public bool SwapsPeekedTiles(GameState s, int playerId) => Mine(playerId);
}

/// <summary>The Medic: the Hospital never bills or slows them down.</summary>
public sealed class HospitalImmunityModifier(ModifierContext c) : OwnedModifier(c), IRuleModifier
{
    public bool ShrugsOffHospital(GameState s, int playerId) => Mine(playerId);
}

/// <summary>The Medic: Insured markers cost less ("value" is the price, and 0 means free).</summary>
public sealed class InsuredMarkerCostModifier(ModifierContext c) : OwnedModifier(c), IRuleModifier
{
    public int? InsuredMarkerCost(GameState s, int playerId) => Mine(playerId) ? Value : null;
}

/// <summary>The Medic: Hospital bills and Insured markers other players pay go to the Medic, not the bank (decision #153).</summary>
public sealed class HospitalCollectorModifier(ModifierContext c) : OwnedModifier(c), IRuleModifier
{
    public bool CollectsHospitalMoney(GameState s, int payerId) => !Mine(payerId) && !s.Player(Owner).IsBankrupt;
}

/// <summary>The Politician's Fundraising: cards that give them cash give this much more, in percent, from the bank (decision #153).</summary>
public sealed class FundraisingModifier(ModifierContext c) : OwnedModifier(c), IRuleModifier
{
    public int CardCashBonusPercent(GameState s, int playerId) => Mine(playerId) ? Value : 0;
}
