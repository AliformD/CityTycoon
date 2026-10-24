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
[JsonDerivedType(typeof(CounterTrade), nameof(CounterTrade))]
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
[JsonDerivedType(typeof(HopToTransitHub), nameof(HopToTransitHub))]
[JsonDerivedType(typeof(RideToRailroad), nameof(RideToRailroad))]
[JsonDerivedType(typeof(PickCharacter), nameof(PickCharacter))]
[JsonDerivedType(typeof(ExpressLine), nameof(ExpressLine))]
[JsonDerivedType(typeof(HostileTakeover), nameof(HostileTakeover))]
[JsonDerivedType(typeof(RerollDice), nameof(RerollDice))]
[JsonDerivedType(typeof(KeepCard), nameof(KeepCard))]
[JsonDerivedType(typeof(PeekAtTile), nameof(PeekAtTile))]
[JsonDerivedType(typeof(SwapTile), nameof(SwapTile))]
[JsonDerivedType(typeof(ChooseOrdinance), nameof(ChooseOrdinance))]
[JsonDerivedType(typeof(ChooseDistrict), nameof(ChooseDistrict))]
[JsonDerivedType(typeof(CastVote), nameof(CastVote))]
[JsonDerivedType(typeof(OfferVoteDeal), nameof(OfferVoteDeal))]
[JsonDerivedType(typeof(AcceptVoteDeal), nameof(AcceptVoteDeal))]
[JsonDerivedType(typeof(DeclineVoteDeal), nameof(DeclineVoteDeal))]
[JsonDerivedType(typeof(ReverseVote), nameof(ReverseVote))]
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

/// <summary>Say no to a trade someone offered you. Bots add a short <paramref name="Why"/> for the log.</summary>
public sealed record DeclineTrade(string? Why = null) : GameAction;

/// <summary>
/// Say no to a trade, but send back different terms instead. The counter is
/// written from the side of the player sending it (they are its FromId), and
/// goes to whoever made the offer they are answering.
/// </summary>
/// <param name="Why">A short reason for the other player (bots fill it in), or null.</param>
public sealed record CounterTrade(TradeOffer Offer, string? Why = null) : GameAction;

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

/// <summary>Next to a revealed Transit Hub: pay the hop fee and move onto the hub instead of paying rent (Section 12.3).</summary>
public sealed record HopToTransitHub : GameAction;

/// <summary>On the Transit Hub: pay the fare and move to this railroad.</summary>
public sealed record RideToRailroad(string PropertyId) : GameAction;

/// <summary>At the start of the game: pick this character (Section 5).</summary>
public sealed record PickCharacter(string CharacterId) : GameAction;

/// <summary>Express Line (2 railroads): instead of rolling, pay and jump to this railroad you own, then roll 1 die (Section 13.3).</summary>
public sealed record ExpressLine(string RailroadId) : GameAction;

/// <summary>The Red perk: force an auction on this opponent's property, once per lap (Section 8.6).</summary>
public sealed record HostileTakeover(string PropertyId) : GameAction;

/// <summary>
/// The Hustler: roll again instead of keeping a bad roll. For a movement roll (once per lap) only one die
/// is rolled again, and <paramref name="Die"/> says which (0 for the first, 1 for the second; decision #153).
/// Null rolls all the dice again (a Dice Duel roll).
/// </summary>
public sealed record RerollDice(int? Die = null) : GameAction;

/// <summary>The City Planner: turn this City Council result around (passed becomes failed, and failed becomes passed). Once per game.</summary>
public sealed record ReverseVote : GameAction;

/// <summary>The Arts District perk: keep this one of the two Opportunity cards you drew.</summary>
public sealed record KeepCard(string CardId) : GameAction;

/// <summary>Secretly peek at the hidden Government tile on this space (the City Planner).</summary>
public sealed record PeekAtTile(string SpaceId) : GameAction;

/// <summary>The City Planner: swap the hidden tile just peeked at for a random one not in play (decision #158).</summary>
public sealed record SwapTile(string SpaceId) : GameAction;

/// <summary>City Council: the agenda setter picks which of the drawn ordinances to put to a vote (Section 11.2).</summary>
public sealed record ChooseOrdinance(string OrdinanceId) : GameAction;

/// <summary>City Council: pick a district for an ordinance that needs one.</summary>
public sealed record ChooseDistrict(string DistrictId) : GameAction;

public enum VoteChoice { Yes, No, Abstain }

/// <summary>
/// City Council: a secret vote (Section 11.3). <paramref name="Lobby"/> is cash you give the bank to add that much
/// influence to your side. <paramref name="PlayLobbyist"/> plays a Lobbyist card from your hand for 200 more.
/// Abstaining means no lobbying at all.
/// </summary>
public sealed record CastVote(VoteChoice Choice, int Lobby = 0, bool PlayLobbyist = false) : GameAction;

/// <summary>
/// A vote deal (decision #159): before a City Council vote, <paramref name="FromId"/> offers
/// <paramref name="ToId"/> cash to vote <paramref name="Choice"/>. If accepted, the cash is paid at once
/// and the engine holds them to that vote.
/// </summary>
public sealed record VoteDeal(int FromId, int ToId, int Amount, VoteChoice Choice);

/// <summary>Offer another player a vote deal before the City Council votes (decision #159).</summary>
public sealed record OfferVoteDeal(VoteDeal Deal) : GameAction;

/// <summary>Take the vote deal you were offered: the cash is yours, and you must vote that way.</summary>
public sealed record AcceptVoteDeal : GameAction;

/// <summary>Turn down the vote deal you were offered.</summary>
public sealed record DeclineVoteDeal : GameAction;

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
/// <summary>Borrow this much from the bank: one of the loan sizes in rules.json (decision #154).</summary>
public sealed record TakeLoan(int Amount) : ManagementAction;

/// <summary>Pay back one loan of this size.</summary>
public sealed record RepayLoan(int Amount) : ManagementAction;
public sealed record ProposeTrade(TradeOffer Offer) : ManagementAction;

/// <summary>
/// A trade offer (Section 15.9): what the proposer gives and what they want back.
/// Cards are Opportunity card IDs from the players' hands.
/// </summary>
public sealed record TradeOffer(
    int FromId,
    int ToId,
    int GiveCash,
    int GetCash,
    IReadOnlyList<string> GiveProperties,
    IReadOnlyList<string> GetProperties)
{
    /// <summary>Opportunity cards the proposer gives.</summary>
    public IReadOnlyList<string> GiveCards { get; init; } = [];

    /// <summary>
    /// Opportunity cards the proposer wants back. Hands are secret, so this can only
    /// hold cards the other player offered first (in the trade a counter answers).
    /// </summary>
    public IReadOnlyList<string> GetCards { get; init; } = [];

    /// <summary>The same trade, written from the other player's side.</summary>
    public TradeOffer Reversed() =>
        new(ToId, FromId, GetCash, GiveCash, GetProperties, GiveProperties) { GiveCards = GetCards, GetCards = GiveCards };
}

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
    TransitHop,
    TransitRide,
    ChooseCharacter,
    ChooseReroll,
    KeepCard,
    PeekTile,
    ChooseOrdinance,
    ChooseDistrict,
    CouncilVote,
    RerollMove,
    ReverseCouncilVote,
    SwapTile,
    OfferVoteDeal,
    AnswerVoteDeal,
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
/// <param name="AmountOwed">For raising money: how much must be paid. For the Hospital: what an Insured marker costs.</param>
/// <param name="Trade">For trade replies: the offer.</param>
/// <param name="CountersLeft">For trade replies: how many more counter-offers this trade can have (0 means only accept or decline).</param>
/// <param name="Dice">For Precision Rolls: the dice rolled. For Nudges: the dice being used.</param>
/// <param name="MaxBet">For the Casino: the most this player can bet.</param>
/// <param name="Duel">For Dice Duel replies: the challenge.</param>
/// <param name="Cards">Card IDs to show: the triggering card for a reaction, or the Event cards a Tip-Off reveals.</param>
/// <param name="OtherPlayerId">For a Detour: the player about to move.</param>
/// <param name="Spaces">For a Detour: how far that player is about to move.</param>
/// <param name="OpenAuction">True when this is a turn in an open (ascending) auction rather than a secret bid.</param>
/// <param name="HighBidderId">In an open auction: who holds the highest bid so far (null before the first bid).</param>
/// <param name="HighBid">In an open auction: the highest bid so far.</param>
/// <param name="Bidders">In an open auction: everyone in it, in bidding order.</param>
/// <param name="DroppedOut">In an open auction: the bidders who have dropped out.</param>
/// <param name="Ordinances">City Council: the ordinance IDs the agenda setter can pick from.</param>
/// <param name="OrdinanceId">City Council: the ordinance being voted on.</param>
/// <param name="DistrictId">City Council: the district the ordinance is about, if it has one.</param>
/// <param name="Districts">City Council: the districts that can be picked.</param>
/// <param name="MaxLobby">City Council: the most this player can lobby (their cash).</param>
/// <param name="HasLobbyist">City Council: true if this player holds a Lobbyist card.</param>
/// <param name="Passed">City Council, for the City Planner's reversal: true if the ordinance passed.</param>
/// <param name="Deal">City Council: the vote deal this player is asked to answer, or (when voting) the one they accepted.</param>
/// <param name="MustBid">For a silent auction: true if this player can't pass (they started it with Hostile Takeover, decision #164).</param>
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
    int CountersLeft = 0,
    IReadOnlyList<int>? Dice = null,
    int MaxBet = 0,
    DuelChallenge? Duel = null,
    IReadOnlyList<string>? Cards = null,
    int? OtherPlayerId = null,
    int Spaces = 0,
    bool OpenAuction = false,
    int? HighBidderId = null,
    int HighBid = 0,
    IReadOnlyList<int>? Bidders = null,
    IReadOnlyList<int>? DroppedOut = null,
    IReadOnlyList<string>? Ordinances = null,
    string? OrdinanceId = null,
    string? DistrictId = null,
    IReadOnlyList<string>? Districts = null,
    int MaxLobby = 0,
    bool HasLobbyist = false,
    bool Passed = false,
    VoteDeal? Deal = null,
    bool MustBid = false);
