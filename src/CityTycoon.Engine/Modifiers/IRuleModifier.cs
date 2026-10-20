using CityTycoon.Engine.Content;
using CityTycoon.Engine.State;
using CityTycoon.Engine.Tiles;

namespace CityTycoon.Engine.Modifiers;

/// <summary>
/// Something that changes a rule (Section 20.6): a revealed Government tile,
/// a character's ability, or a perk from owning a whole color group, or a
/// railroad or utility bonus.
///
/// The engine asks every active modifier at fixed "hook points" (rent, build
/// cost, GO, loans...) instead of checking for each tile or character by
/// name. So a new modifier is a new class, and the rent, build and GO code
/// never has to change. The methods with bodies are the "no change" answers:
/// a modifier only overrides the hooks it cares about.
///
/// Modifiers that belong to a player (characters and perks) have an
/// <see cref="OwnerId"/>. They only affect that player, or, for a few hooks
/// (Utility Bill, Neighborhood Watch...), they make an opponent pay the owner.
/// </summary>
public interface IRuleModifier
{
    /// <summary>What to call this modifier in a rent breakdown or the game log, like "Landfill next door" or "Dividends".</summary>
    string Source { get; }

    /// <summary>The player this modifier belongs to, or null for a Government tile (which belongs to nobody).</summary>
    int? OwnerId => null;

    // ----- Rent, building and cards (Government tiles, and some perks) -----

    /// <summary>A percentage added to this property's rent (−50 means 50% less). Section 13.1, step 2.</summary>
    int RentPercent(GameState state, string propertyId) => 0;

    /// <summary>True if this property ignores every negative rent modifier (Prime Real Estate). Section 13.1.</summary>
    bool IgnoresNegativeRent(GameState state, string propertyId) => false;

    /// <summary>A percentage taken off the cost of building on this property. Section 9.4.</summary>
    int BuildDiscountPercent(GameState state, string propertyId) => 0;

    /// <summary>False if a card of this polarity may not target this property. Helpful cards are never stopped here.</summary>
    bool CanBeTargetedByCards(GameState state, string propertyId, CardPolarity polarity) => true;

    /// <summary>True if the Fire! Event can't touch this property.</summary>
    bool ProtectsFromFire(GameState state, string propertyId) => false;

    /// <summary>False if this player can't be the target of this card (the Lawyer and Audit).</summary>
    bool CanPlayerBeTargeted(GameState state, int playerId, CardDefinition card) => true;

    // ----- Passing GO (Section 15.1) -----

    /// <summary>Extra cash when this player passes GO (Community Roots).</summary>
    int GoCash(GameState state, int playerId) => 0;

    /// <summary>Extra Momentum tokens when this player passes GO (Commuter Belt).</summary>
    int GoMomentum(GameState state, int playerId) => 0;

    /// <summary>How much an opponent passing GO pays this modifier's owner (Utility Bill).</summary>
    int GoToll(GameState state, int passerId) => 0;

    /// <summary>How many hidden Government tiles this player may secretly peek at when passing GO (the City Planner).</summary>
    int GoPeeks(GameState state, int playerId) => 0;

    // ----- Momentum (Section 7.3) -----

    /// <summary>Extra room for Momentum tokens (Commuter, Commuter Belt).</summary>
    int MomentumCapBonus(GameState state, int playerId) => 0;

    /// <summary>How many tokens this player starts the game with, if more than the normal 1 (the Commuter).</summary>
    int StartingMomentum(GameState state, int playerId) => 0;

    /// <summary>Tokens taken off the Precision Roll price (Rail Baron).</summary>
    int PrecisionRollDiscount(GameState state, int playerId) => 0;

    /// <summary>Tokens the rent owner gains when someone pays them rent on this kind of property (Toll Collector).</summary>
    int MomentumFromRent(GameState state, int ownerId, PropertyType type) => 0;

    // ----- Cards (Section 15.4) -----

    /// <summary>Extra room in this player's hand of Opportunity cards (Arts District).</summary>
    int HandLimitBonus(GameState state, int playerId) => 0;

    /// <summary>True if this player draws 2 Opportunity cards and keeps 1 (Arts District).</summary>
    bool DrawsTwoKeepsOne(GameState state, int playerId) => false;

    // ----- Loans and the start of a turn -----

    /// <summary>The cost to repay one loan, if this modifier changes it (Bank Partner). Return 0 for "no change".</summary>
    int LoanRepayCost(GameState state, int playerId) => 0;

    /// <summary>How many loans this player may hold, if this modifier changes it (Bank Partner). Return 0 for "no change".</summary>
    int MaxLoans(GameState state, int playerId) => 0;

    /// <summary>Cash paid to this player at the start of their turn (Dividends).</summary>
    int TurnStartIncome(GameState state, int playerId) => 0;

    /// <summary>
    /// How much a player starting their turn pays this modifier's owner (Neighborhood Watch).
    /// <paramref name="jailedOrRecovering"/> is true in Jail or while recovering from the Hospital.
    /// </summary>
    int TurnStartCharge(GameState state, int playerId, bool jailedOrRecovering) => 0;

    // ----- Building -----

    /// <summary>How much a player pays this modifier's owner each time they build a level (Power Grid).</summary>
    int BuildFee(GameState state, int builderId) => 0;

    // ----- Moving, the Hospital and Jail -----

    /// <summary>The price of the Express Line for this player, or 0 if they don't have it (Section 13.3).</summary>
    int ExpressLineCost(GameState state, int playerId) => 0;

    /// <summary>True if this player may take a Hostile Takeover once per lap (Section 8.6).</summary>
    bool CanHostileTakeover(GameState state, int playerId) => false;

    /// <summary>True if this player never pays bail (the Lawyer).</summary>
    bool FreeBail(GameState state, int playerId) => false;

    /// <summary>True if this player is never billed or slowed by the Hospital (the Medic).</summary>
    bool ShrugsOffHospital(GameState state, int playerId) => false;

    /// <summary>The price of an Insured marker for this player, or 0 for "no change" (the Medic).</summary>
    int InsuredMarkerCost(GameState state, int playerId) => 0;

    // ----- Auctions, the Casino and the Council -----

    /// <summary>How much higher this player's bid counts when bids are compared (the Mogul).</summary>
    int BidBonus(GameState state, int playerId) => 0;

    /// <summary>True if this player may reroll once at the Casino (the Hustler).</summary>
    bool CasinoReroll(GameState state, int playerId) => false;

    /// <summary>This player's City Council influence, if this modifier changes it (the Politician). Return 0 for "no change".</summary>
    int VoteInfluence(GameState state, int playerId) => 0;

    /// <summary>How many hidden Government tiles this player may secretly peek at when the game starts (the City Planner).</summary>
    int StartPeeks(GameState state, int playerId) => 0;
}
