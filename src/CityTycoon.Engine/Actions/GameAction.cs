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

/// <summary>In an auction: a sealed bid, paid in full.</summary>
public sealed record PlaceBid(int Amount) : GameAction;

/// <summary>What the engine is waiting for.</summary>
public enum DecisionKind { ChooseMoveMethod, JailChoice, PostMoveActions, SubmitBid }

/// <summary>
/// A choice the engine is waiting for, and from whom (Section 20.4).
/// Several can be pending at once, for example everyone's sealed bids.
/// </summary>
/// <param name="Prompt">A short sentence to show the player.</param>
/// <param name="PropertyId">For auctions: the property being sold.</param>
/// <param name="MinBid">For auctions: the lowest allowed bid.</param>
/// <param name="MaxBid">For auctions: the highest bid this player can afford.</param>
/// <param name="BidIncrement">For auctions: bids must be a multiple of this.</param>
public sealed record PendingDecision(
    int PlayerId,
    DecisionKind Kind,
    string Prompt,
    string? PropertyId = null,
    int MinBid = 0,
    int MaxBid = 0,
    int BidIncrement = 0);
