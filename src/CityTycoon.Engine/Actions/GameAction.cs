using System.Text.Json.Serialization;

namespace CityTycoon.Engine.Actions;

/// <summary>
/// A player's answer to a decision (Section 20.4). Every button in the UI and
/// every bot choice turns into one of these.
///
/// The [JsonDerivedType] lines let a list of mixed actions be saved as JSON
/// and loaded back as the right types (needed for replays and online play).
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(RollDice), nameof(RollDice))]
[JsonDerivedType(typeof(EndTurn), nameof(EndTurn))]
[JsonDerivedType(typeof(PayBail), nameof(PayBail))]
[JsonDerivedType(typeof(RollForDoubles), nameof(RollForDoubles))]
[JsonDerivedType(typeof(PassBid), nameof(PassBid))]
[JsonDerivedType(typeof(PlaceBid), nameof(PlaceBid))]
[JsonDerivedType(typeof(AcceptTrade), nameof(AcceptTrade))]
[JsonDerivedType(typeof(DeclineTrade), nameof(DeclineTrade))]
[JsonDerivedType(typeof(ForfeitProperty), nameof(ForfeitProperty))]
[JsonDerivedType(typeof(Build), nameof(Build))]
[JsonDerivedType(typeof(SellBuilding), nameof(SellBuilding))]
[JsonDerivedType(typeof(Mortgage), nameof(Mortgage))]
[JsonDerivedType(typeof(Unmortgage), nameof(Unmortgage))]
[JsonDerivedType(typeof(TakeLoan), nameof(TakeLoan))]
[JsonDerivedType(typeof(RepayLoan), nameof(RepayLoan))]
[JsonDerivedType(typeof(ProposeTrade), nameof(ProposeTrade))]
[JsonDerivedType(typeof(PrecisionRoll), nameof(PrecisionRoll))]
[JsonDerivedType(typeof(ChooseDice), nameof(ChooseDice))]
[JsonDerivedType(typeof(Nudge), nameof(Nudge))]
[JsonDerivedType(typeof(NoThanks), nameof(NoThanks))]
[JsonDerivedType(typeof(BuyInsurance), nameof(BuyInsurance))]
[JsonDerivedType(typeof(BetHighLow), nameof(BetHighLow))]
[JsonDerivedType(typeof(ChallengeDuel), nameof(ChallengeDuel))]
[JsonDerivedType(typeof(AcceptDuel), nameof(AcceptDuel))]
[JsonDerivedType(typeof(DeclineDuel), nameof(DeclineDuel))]
[JsonDerivedType(typeof(PickStadiumGroup), nameof(PickStadiumGroup))]
[JsonDerivedType(typeof(PlayCard), nameof(PlayCard))]
[JsonDerivedType(typeof(DiscardCard), nameof(DiscardCard))]
[JsonDerivedType(typeof(TakeMomentum), nameof(TakeMomentum))]
[JsonDerivedType(typeof(DrawOpportunity), nameof(DrawOpportunity))]
[JsonDerivedType(typeof(BuyCard), nameof(BuyCard))]
[JsonDerivedType(typeof(SellCard), nameof(SellCard))]
[JsonDerivedType(typeof(ChooseProperty), nameof(ChooseProperty))]
[JsonDerivedType(typeof(ArrangeCards), nameof(ArrangeCards))]
[JsonDerivedType(typeof(ClearRoadWork), nameof(ClearRoadWork))]
public abstract record GameAction;

/// <summary>Roll the normal 2 dice.</summary>
public sealed record RollDice : GameAction;

/// <summary>Finish the post-move phase.</summary>
public sealed record EndTurn : GameAction;

/// <summary>In Jail: pay bail, then roll and move.</summary>
public sealed record PayBail : GameAction;

/// <summary>In Jail: try to roll doubles to get out.</summary>
public sealed record RollForDoubles : GameAction;

/// <summary>In an auction: don't bid.</summary>
public sealed record PassBid : GameAction;

/// <summary>
/// In an auction: a sealed bid. With <paramref name="DownPayment"/> you pay
/// 20% now and the rest at your next GO (Section 8.5).
/// </summary>
public sealed record PlaceBid(int Amount, bool DownPayment = false) : GameAction;

/// <summary>Say yes to a trade someone offered you.</summary>
public sealed record AcceptTrade : GameAction;

/// <summary>Say no to a trade someone offered you.</summary>
public sealed record DeclineTrade : GameAction;

/// <summary>Give up a pending property instead of paying its balance (Section 8.5).</summary>
public sealed record ForfeitProperty : GameAction;

/// <summary>Spend Momentum tokens to roll 3 dice and keep 2 (Section 7.3).</summary>
public sealed record PrecisionRoll : GameAction;

/// <summary>After a Precision Roll: keep these two dice (positions 0, 1 or 2 in the roll).</summary>
public sealed record ChooseDice(int First, int Second) : GameAction;

/// <summary>
/// After rolling: move this many spaces more (positive) or fewer (negative)
/// than the dice say. 0 means no Nudge and costs nothing (Section 7.3).
/// </summary>
public sealed record Nudge(int Spaces) : GameAction;

/// <summary>Turn down an optional offer, like an Insured marker or a Casino bet.</summary>
public sealed record NoThanks : GameAction;

/// <summary>At the Hospital: buy an Insured marker (Section 15.3).</summary>
public sealed record BuyInsurance : GameAction;

/// <summary>At the Casino: bet that the next 2 dice total High (8-12) or Low (2-6).</summary>
public sealed record BetHighLow(int Amount, bool High) : GameAction;

/// <summary>At the Casino: challenge another player to a Dice Duel for this stake.</summary>
public sealed record ChallengeDuel(int OpponentId, int Amount) : GameAction;

/// <summary>Say yes to a Dice Duel.</summary>
public sealed record AcceptDuel : GameAction;

/// <summary>Say no to a Dice Duel.</summary>
public sealed record DeclineDuel : GameAction;

/// <summary>At the Stadium: double this color group's rent until your next turn.</summary>
public sealed record PickStadiumGroup(string GroupId) : GameAction;

/// <summary>
/// Play an Opportunity card from your hand (Section 15.4). Which extra fields
/// are used depends on the card: a player to target, a space or property to
/// target, or a number (spaces for Shortcut and Detour, the stake for Dice Duel).
/// </summary>
public sealed record PlayCard(string CardId, int? TargetPlayerId = null, string? TargetSpaceId = null, int Number = 0) : GameAction;

/// <summary>Over the hand limit at the end of your turn: throw this card away.</summary>
public sealed record DiscardCard(string CardId) : GameAction;

/// <summary>At City Park: take a Momentum token.</summary>
public sealed record TakeMomentum : GameAction;

/// <summary>At City Park: draw an Opportunity card.</summary>
public sealed record DrawOpportunity : GameAction;

/// <summary>At the Mall: buy the card in this shop slot (0 is the cheapest).</summary>
public sealed record BuyCard(int Slot) : GameAction;

/// <summary>At the Mall: sell this card from your hand to the bank.</summary>
public sealed record SellCard(string CardId) : GameAction;

/// <summary>Pick one property (for example which building the Fire! card hits when there's a tie).</summary>
public sealed record ChooseProperty(string PropertyId) : GameAction;

/// <summary>
/// Tip-Off: put the cards you looked at back in this order. Each number is a
/// position in the list you were shown, so [2, 0, 1] puts the third card on top.
/// </summary>
public sealed record ArrangeCards(IReadOnlyList<int> Order) : GameAction;

/// <summary>Road Work: spend Momentum tokens to roll both dice this turn after all.</summary>
public sealed record ClearRoadWork : GameAction;

/// <summary>A Dice Duel challenge waiting for an answer.</summary>
public sealed record DuelChallenge(int ChallengerId, int OpponentId, int Stake);

/// <summary>
/// "Management" actions: things you can do in between the main choices, like
/// building or taking a loan (Section 7.2, pre-roll phase). After one of these,
/// the engine asks the same question again, so you can do several in a row.
/// </summary>
public abstract record ManagementAction : GameAction;

public sealed record Build(string PropertyId) : ManagementAction;
public sealed record SellBuilding(string PropertyId) : ManagementAction;
public sealed record Mortgage(string PropertyId) : ManagementAction;
public sealed record Unmortgage(string PropertyId) : ManagementAction;
public sealed record TakeLoan : ManagementAction;
public sealed record RepayLoan : ManagementAction;
public sealed record ProposeTrade(TradeOffer Offer) : ManagementAction;

/// <summary>
/// A trade offer (Section 15.9): what the proposer gives and what they want back.
/// </summary>
public sealed record TradeOffer(
    int FromId,
    int ToId,
    int GiveCash,
    int GetCash,
    IReadOnlyList<string> GiveProperties,
    IReadOnlyList<string> GetProperties);

/// <summary>
/// Which management actions a decision allows. [Flags] means several can be
/// combined with |, like <c>Mortgage | TakeLoan</c>.
/// </summary>
[Flags]
public enum ManagementOptions
{
    None = 0,
    Build = 1,
    Sell = 2,
    Mortgage = 4,
    Unmortgage = 8,
    TakeLoan = 16,
    RepayLoan = 32,
    Trade = 64,

    /// <summary>Your own turn, before rolling or after moving (Section 7.2).</summary>
    OwnTurn = Build | Sell | Mortgage | Unmortgage | TakeLoan | RepayLoan | Trade,

    /// <summary>In Jail you can't build, but you can do the rest (Section 14.2).</summary>
    InJail = Sell | Mortgage | Unmortgage | TakeLoan | RepayLoan | Trade,

    /// <summary>When you owe more cash than you have (Sections 9.3, 15.7).</summary>
    RaiseMoney = Sell | Mortgage | TakeLoan,
}

/// <summary>What the engine is waiting for.</summary>
public enum DecisionKind
{
    ChooseMoveMethod,
    JailChoice,
    PostMoveActions,
    SubmitBid,
    RaiseMoney,
    RespondToTrade,
    ChoosePrecisionDice,
    ChooseNudge,
    HospitalVisit,
    CasinoBet,
    RespondToDuel,
    ChooseStadiumGroup,
    PlayReaction,
    DiscardCards,
    CityParkChoice,
    MallVisit,
    ChooseFireProperty,
    ArrangeEvents,
}

/// <summary>
/// A choice the engine is waiting for, and from whom (Section 20.4).
/// Several can be pending at once, for example everyone's sealed bids.
/// </summary>
/// <param name="Prompt">A short sentence to show the player.</param>
/// <param name="PropertyId">For auctions: the property being sold.</param>
/// <param name="MinBid">For auctions: the lowest allowed bid.</param>
/// <param name="MaxBid">For auctions: the highest bid this player can afford.</param>
/// <param name="BidIncrement">For auctions: bids must be a multiple of this.</param>
/// <param name="MaxDownPaymentBid">For auctions: the highest bid this player can make with a down payment (0 if they can't).</param>
/// <param name="Management">Which management actions (build, loans...) are allowed while deciding.</param>
/// <param name="AmountOwed">For raising money: how much must be paid.</param>
/// <param name="Trade">For trade replies: the offer.</param>
/// <param name="Dice">For Precision Rolls: the dice rolled. For Nudges: the dice being used.</param>
/// <param name="MaxBet">For the Casino: the most this player can bet.</param>
/// <param name="Duel">For Dice Duel replies: the challenge.</param>
/// <param name="Cards">Card IDs to show: the triggering card for a reaction, or the Event cards a Tip-Off reveals.</param>
/// <param name="OtherPlayerId">For a Detour: the player about to move.</param>
/// <param name="Spaces">For a Detour: how far that player is about to move.</param>
public sealed record PendingDecision(
    int PlayerId,
    DecisionKind Kind,
    string Prompt,
    string? PropertyId = null,
    int MinBid = 0,
    int MaxBid = 0,
    int BidIncrement = 0,
    int MaxDownPaymentBid = 0,
    ManagementOptions Management = ManagementOptions.None,
    int AmountOwed = 0,
    TradeOffer? Trade = null,
    IReadOnlyList<int>? Dice = null,
    int MaxBet = 0,
    DuelChallenge? Duel = null,
    IReadOnlyList<string>? Cards = null,
    int? OtherPlayerId = null,
    int Spaces = 0);
