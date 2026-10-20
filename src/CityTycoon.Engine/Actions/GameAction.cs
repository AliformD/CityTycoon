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
public enum DecisionKind { ChooseMoveMethod, JailChoice, PostMoveActions, SubmitBid, RaiseMoney, RespondToTrade }

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
    TradeOffer? Trade = null);
