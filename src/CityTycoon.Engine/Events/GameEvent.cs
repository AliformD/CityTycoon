using System.Text.Json.Serialization;

namespace CityTycoon.Engine.Events;

/// <summary>
/// Something that happened (Section 20.4). Events are used for the game log,
/// for animations, for statistics, and to check two games played the same way.
/// They are records: once created they never change.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(GameStarted), nameof(GameStarted))]
[JsonDerivedType(typeof(TurnStarted), nameof(TurnStarted))]
[JsonDerivedType(typeof(DiceRolled), nameof(DiceRolled))]
[JsonDerivedType(typeof(PlayerMoved), nameof(PlayerMoved))]
[JsonDerivedType(typeof(PassedGo), nameof(PassedGo))]
[JsonDerivedType(typeof(LandedOn), nameof(LandedOn))]
[JsonDerivedType(typeof(AuctionStarted), nameof(AuctionStarted))]
[JsonDerivedType(typeof(BidsRevealed), nameof(BidsRevealed))]
[JsonDerivedType(typeof(AuctionWon), nameof(AuctionWon))]
[JsonDerivedType(typeof(AuctionNoBids), nameof(AuctionNoBids))]
[JsonDerivedType(typeof(RentPaid), nameof(RentPaid))]
[JsonDerivedType(typeof(TaxPaid), nameof(TaxPaid))]
[JsonDerivedType(typeof(BailPaid), nameof(BailPaid))]
[JsonDerivedType(typeof(SentToJail), nameof(SentToJail))]
[JsonDerivedType(typeof(LeftJail), nameof(LeftJail))]
[JsonDerivedType(typeof(StayedInJail), nameof(StayedInJail))]
[JsonDerivedType(typeof(PlayerBankrupt), nameof(PlayerBankrupt))]
[JsonDerivedType(typeof(RoundEnded), nameof(RoundEnded))]
[JsonDerivedType(typeof(GameEnded), nameof(GameEnded))]
[JsonDerivedType(typeof(BuildingBuilt), nameof(BuildingBuilt))]
[JsonDerivedType(typeof(BuildingSold), nameof(BuildingSold))]
[JsonDerivedType(typeof(PropertyMortgaged), nameof(PropertyMortgaged))]
[JsonDerivedType(typeof(PropertyUnmortgaged), nameof(PropertyUnmortgaged))]
[JsonDerivedType(typeof(LoanTaken), nameof(LoanTaken))]
[JsonDerivedType(typeof(LoanRepaid), nameof(LoanRepaid))]
[JsonDerivedType(typeof(DownPaymentPaid), nameof(DownPaymentPaid))]
[JsonDerivedType(typeof(PropertyForfeited), nameof(PropertyForfeited))]
[JsonDerivedType(typeof(TradeProposed), nameof(TradeProposed))]
[JsonDerivedType(typeof(TradeCompleted), nameof(TradeCompleted))]
[JsonDerivedType(typeof(TradeDeclined), nameof(TradeDeclined))]
[JsonDerivedType(typeof(RaisingMoney), nameof(RaisingMoney))]
[JsonDerivedType(typeof(MomentumGained), nameof(MomentumGained))]
[JsonDerivedType(typeof(PrecisionRolled), nameof(PrecisionRolled))]
[JsonDerivedType(typeof(Nudged), nameof(Nudged))]
[JsonDerivedType(typeof(InsuranceBought), nameof(InsuranceBought))]
[JsonDerivedType(typeof(InsuranceUsed), nameof(InsuranceUsed))]
[JsonDerivedType(typeof(SentToHospital), nameof(SentToHospital))]
[JsonDerivedType(typeof(HospitalBillPaid), nameof(HospitalBillPaid))]
[JsonDerivedType(typeof(CasinoHighLow), nameof(CasinoHighLow))]
[JsonDerivedType(typeof(DuelChallenged), nameof(DuelChallenged))]
[JsonDerivedType(typeof(DuelDeclined), nameof(DuelDeclined))]
[JsonDerivedType(typeof(DuelRolled), nameof(DuelRolled))]
[JsonDerivedType(typeof(DuelWon), nameof(DuelWon))]
[JsonDerivedType(typeof(StadiumChosen), nameof(StadiumChosen))]
[JsonDerivedType(typeof(StadiumEnded), nameof(StadiumEnded))]
public abstract record GameEvent;

public sealed record GameStarted(ulong Seed, IReadOnlyList<int> TurnOrder) : GameEvent;
public sealed record TurnStarted(int PlayerId, int Round) : GameEvent;
/// <summary>The dice used to move. On a one-die turn (Hospital recovery), <see cref="Die2"/> is 0.</summary>
public sealed record DiceRolled(int PlayerId, int Die1, int Die2) : GameEvent
{
    public int Total => Die1 + Die2;
    public bool IsOneDie => Die2 == 0;
    public bool IsDoubles => !IsOneDie && Die1 == Die2;
}
public sealed record PlayerMoved(int PlayerId, int From, int To) : GameEvent;
public sealed record PassedGo(int PlayerId, int Salary) : GameEvent;
public sealed record LandedOn(int PlayerId, string SpaceId) : GameEvent;
public sealed record AuctionStarted(string PropertyId, int MinBid) : GameEvent;

/// <summary>One revealed sealed bid. A null amount means the player passed.</summary>
public sealed record RevealedBid(int PlayerId, int? Amount);
public sealed record BidsRevealed(string PropertyId, IReadOnlyList<RevealedBid> Bids) : GameEvent;
/// <param name="PaidNow">The whole price, or just the deposit for a down payment.</param>
public sealed record AuctionWon(string PropertyId, int PlayerId, int Price, int AssessedValue, bool DownPayment = false, int PaidNow = 0) : GameEvent;
public sealed record AuctionNoBids(string PropertyId) : GameEvent;
public sealed record RentPaid(int FromId, int ToId, string PropertyId, int Amount) : GameEvent;
public sealed record TaxPaid(int PlayerId, string SpaceId, int Amount) : GameEvent;
public sealed record BailPaid(int PlayerId, int Amount) : GameEvent;
public sealed record SentToJail(int PlayerId, string Reason) : GameEvent;
public sealed record LeftJail(int PlayerId, string How) : GameEvent;
public sealed record StayedInJail(int PlayerId, int JailTurn) : GameEvent;

/// <param name="CreditorId">The player who was owed, or null for the bank.</param>
public sealed record PlayerBankrupt(int PlayerId, int? CreditorId, int CashHandedOver, IReadOnlyList<string> PropertiesHandedOver) : GameEvent;
public sealed record RoundEnded(int Round) : GameEvent;
public sealed record GameEnded(IReadOnlyList<int> WinnerIds, string Reason) : GameEvent;

public sealed record BuildingBuilt(int PlayerId, string PropertyId, int NewLevel, int Cost) : GameEvent;
public sealed record BuildingSold(int PlayerId, string PropertyId, int NewLevel, int Refund) : GameEvent;
public sealed record PropertyMortgaged(int PlayerId, string PropertyId, int Amount) : GameEvent;
public sealed record PropertyUnmortgaged(int PlayerId, string PropertyId, int Cost) : GameEvent;
public sealed record LoanTaken(int PlayerId, int Amount) : GameEvent;
public sealed record LoanRepaid(int PlayerId, int Amount) : GameEvent;

/// <summary>A pending property's balance was paid off at GO (Section 8.5).</summary>
public sealed record DownPaymentPaid(int PlayerId, string PropertyId, int Amount) : GameEvent;

/// <summary>A pending property's balance couldn't be paid, so it went back to the bank.</summary>
public sealed record PropertyForfeited(int PlayerId, string PropertyId) : GameEvent;

public sealed record TradeProposed(Actions.TradeOffer Offer) : GameEvent;

/// <param name="MortgageFees">What the receivers paid the bank for mortgaged properties (Section 15.9).</param>
public sealed record TradeCompleted(Actions.TradeOffer Offer, int MortgageFees) : GameEvent;
public sealed record TradeDeclined(Actions.TradeOffer Offer, string Reason) : GameEvent;

/// <summary>A player owes more cash than they have and must raise money first.</summary>
public sealed record RaisingMoney(int PlayerId, int AmountOwed) : GameEvent;

/// <summary>Momentum tokens gained (already limited by the token cap). <paramref name="Source"/> says why, like "GO".</summary>
public sealed record MomentumGained(int PlayerId, int Amount, string Source) : GameEvent;

/// <summary>A Precision Roll: the tokens spent and all 3 dice, before choosing 2 (Section 7.3).</summary>
public sealed record PrecisionRolled(int PlayerId, int TokensSpent, IReadOnlyList<int> Dice) : GameEvent;

/// <summary>A Nudge: moved <paramref name="Spaces"/> more (or fewer, if negative) than rolled.</summary>
public sealed record Nudged(int PlayerId, int Spaces, int TokensSpent) : GameEvent;

public sealed record InsuranceBought(int PlayerId, int Cost) : GameEvent;

/// <summary>An Insured marker was used up to cancel a Hospital bill.</summary>
public sealed record InsuranceUsed(int PlayerId) : GameEvent;

public sealed record SentToHospital(int PlayerId, string Reason) : GameEvent;
public sealed record HospitalBillPaid(int PlayerId, int Amount) : GameEvent;

/// <summary>A High/Low bet at the Casino. <paramref name="Net"/> is what the player won (or lost, if negative).</summary>
public sealed record CasinoHighLow(int PlayerId, int Bet, bool CalledHigh, int Die1, int Die2, int Net) : GameEvent
{
    public int Total => Die1 + Die2;
}

public sealed record DuelChallenged(int ChallengerId, int OpponentId, int Stake) : GameEvent;
public sealed record DuelDeclined(int ChallengerId, int OpponentId) : GameEvent;

/// <summary>One round of Dice Duel rolls. Ties roll again, so there can be several.</summary>
public sealed record DuelRolled(int ChallengerId, int ChallengerTotal, int OpponentId, int OpponentTotal) : GameEvent;
public sealed record DuelWon(int WinnerId, int LoserId, int Stake) : GameEvent;

public sealed record StadiumChosen(int PlayerId, string GroupId) : GameEvent;
public sealed record StadiumEnded(int PlayerId, string GroupId) : GameEvent;
