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
[JsonDerivedType(typeof(OpportunityDrawn), nameof(OpportunityDrawn))]
[JsonDerivedType(typeof(EventDrawn), nameof(EventDrawn))]
[JsonDerivedType(typeof(NoCardToDraw), nameof(NoCardToDraw))]
[JsonDerivedType(typeof(DeckReshuffled), nameof(DeckReshuffled))]
[JsonDerivedType(typeof(CardPlayed), nameof(CardPlayed))]
[JsonDerivedType(typeof(CardCancelled), nameof(CardCancelled))]
[JsonDerivedType(typeof(CardDiscarded), nameof(CardDiscarded))]
[JsonDerivedType(typeof(MallCardBought), nameof(MallCardBought))]
[JsonDerivedType(typeof(MallCardSold), nameof(MallCardSold))]
[JsonDerivedType(typeof(CashFromBank), nameof(CashFromBank))]
[JsonDerivedType(typeof(CashToBank), nameof(CashToBank))]
[JsonDerivedType(typeof(CardPaymentMade), nameof(CardPaymentMade))]
[JsonDerivedType(typeof(RentReduced), nameof(RentReduced))]
[JsonDerivedType(typeof(Detoured), nameof(Detoured))]
[JsonDerivedType(typeof(BuildingBurned), nameof(BuildingBurned))]
[JsonDerivedType(typeof(FireAvoided), nameof(FireAvoided))]
[JsonDerivedType(typeof(LotteryRolled), nameof(LotteryRolled))]
[JsonDerivedType(typeof(LotteryWon), nameof(LotteryWon))]
[JsonDerivedType(typeof(RoadWorkStarted), nameof(RoadWorkStarted))]
[JsonDerivedType(typeof(RoadWorkCleared), nameof(RoadWorkCleared))]
[JsonDerivedType(typeof(RentBoostStarted), nameof(RentBoostStarted))]
[JsonDerivedType(typeof(RentBoostEnded), nameof(RentBoostEnded))]
[JsonDerivedType(typeof(TilePeeked), nameof(TilePeeked))]
[JsonDerivedType(typeof(EventsArranged), nameof(EventsArranged))]
[JsonDerivedType(typeof(RecoverySkipped), nameof(RecoverySkipped))]
[JsonDerivedType(typeof(CardNotReadyYet), nameof(CardNotReadyYet))]
[JsonDerivedType(typeof(OwnerKeptProperty), nameof(OwnerKeptProperty))]
[JsonDerivedType(typeof(TileRevealed), nameof(TileRevealed))]
[JsonDerivedType(typeof(TileFeePaid), nameof(TileFeePaid))]
[JsonDerivedType(typeof(TileNotReadyYet), nameof(TileNotReadyYet))]
[JsonDerivedType(typeof(TransitHopped), nameof(TransitHopped))]
[JsonDerivedType(typeof(TransitRidden), nameof(TransitRidden))]
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
/// <param name="PaidToId">In a forced auction won by someone else, the owner who was paid. Otherwise null (the bank).</param>
public sealed record AuctionWon(string PropertyId, int PlayerId, int Price, int AssessedValue, bool DownPayment = false, int PaidNow = 0, int? PaidToId = null) : GameEvent;
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

/// <summary>A player drew an Opportunity card. Which card it was stays secret.</summary>
public sealed record OpportunityDrawn(int PlayerId, string Source) : GameEvent;

/// <summary>A player drew an Event card. Everyone sees it, since it happens straight away.</summary>
public sealed record EventDrawn(int PlayerId, string CardId) : GameEvent;

/// <summary>Both the deck and its discard pile were empty, so no card could be drawn.</summary>
public sealed record NoCardToDraw(int PlayerId, Content.CardDeck Deck) : GameEvent;

/// <summary>A deck ran out, so its discard pile was shuffled into a new deck (Section 15.6).</summary>
public sealed record DeckReshuffled(Content.CardDeck Deck, int Cards) : GameEvent;

/// <summary>An Opportunity card was played from a hand. Its targets are filled in when it has them.</summary>
public sealed record CardPlayed(int PlayerId, string CardId, int? TargetPlayerId = null, string? TargetSpaceId = null, int Number = 0) : GameEvent;

/// <summary>An Insurance card cancelled another card.</summary>
public sealed record CardCancelled(int PlayerId, string CardId, int CancelledById) : GameEvent;

public sealed record CardDiscarded(int PlayerId, string CardId) : GameEvent;
public sealed record MallCardBought(int PlayerId, string CardId, int Price) : GameEvent;
public sealed record MallCardSold(int PlayerId, string CardId, int Price) : GameEvent;

/// <summary>The bank paid a player because of a card. <paramref name="CardId"/> says which.</summary>
public sealed record CashFromBank(int PlayerId, int Amount, string CardId) : GameEvent;

/// <summary>A player paid the bank because of a card.</summary>
public sealed record CashToBank(int PlayerId, int Amount, string CardId) : GameEvent;

/// <summary>A card made one player pay another (or the bank, when <paramref name="ToId"/> is null).</summary>
public sealed record CardPaymentMade(int FromId, int? ToId, int Amount, string CardId) : GameEvent;

/// <summary>Rent Strike: rent owed went down.</summary>
public sealed record RentReduced(int PlayerId, string PropertyId, int From, int To) : GameEvent;

/// <summary>A Detour moved another player's landing spot.</summary>
public sealed record Detoured(int ByPlayerId, int MoverId, int Spaces) : GameEvent;

/// <summary>Fire!: a property lost a building level.</summary>
public sealed record BuildingBurned(int PlayerId, string PropertyId, int NewLevel) : GameEvent;

/// <summary>Fire!: nothing burned, for this reason.</summary>
public sealed record FireAvoided(int PlayerId, string Reason) : GameEvent;

public sealed record LotteryRoll(int PlayerId, int Die);

/// <summary>One round of Lottery rolls. Ties roll again, so there can be several.</summary>
public sealed record LotteryRolled(IReadOnlyList<LotteryRoll> Rolls) : GameEvent;
public sealed record LotteryWon(int WinnerId, int Pot) : GameEvent;

public sealed record RoadWorkStarted(int PlayerId) : GameEvent;
public sealed record RoadWorkCleared(int PlayerId, int TokensSpent) : GameEvent;

public sealed record RentBoostStarted(int PlayerId, string PropertyId, int Multiplier) : GameEvent;
public sealed record RentBoostEnded(int PlayerId, string PropertyId) : GameEvent;

/// <summary>Inside Source: a player secretly looked at a hidden tile. Only they learn what it is.</summary>
public sealed record TilePeeked(int PlayerId, string SpaceId) : GameEvent;

/// <summary>Tip-Off: a player looked at the top Event cards and put them back. The order stays secret.</summary>
public sealed record EventsArranged(int PlayerId, int Count) : GameEvent;

/// <summary>Get Out of Jail Free used to skip the 1-die Hospital recovery turn.</summary>
public sealed record RecoverySkipped(int PlayerId) : GameEvent;

/// <summary>A card whose effect needs a feature from a later phase (like the City Council) did nothing.</summary>
public sealed record CardNotReadyYet(string CardId) : GameEvent;

/// <summary>A forced auction got no bids, so the owner keeps the property (Section 8.6).</summary>
public sealed record OwnerKeptProperty(string PropertyId, int OwnerId) : GameEvent;

/// <summary>Someone landed on a hidden Government tile, so everyone now knows what it is (Section 12.1).</summary>
public sealed record TileRevealed(int PlayerId, string SpaceId, string TileId) : GameEvent;

/// <summary>A tile's landing fee (Police Station ticket, Landfill cleanup, City Hall, Power Plant).</summary>
public sealed record TileFeePaid(int FromId, int? ToId, int Amount, string TileId) : GameEvent;

/// <summary>A tile effect that needs a feature from a later phase (City Hall's Emergency Council) did nothing.</summary>
public sealed record TileNotReadyYet(string TileId) : GameEvent;

/// <summary>Instead of paying rent next to a Transit Hub, a player paid the fee and hopped onto the hub.</summary>
public sealed record TransitHopped(int PlayerId, string PropertyId, int Cost) : GameEvent;

/// <summary>A player paid the Transit Hub fare to ride to a railroad.</summary>
public sealed record TransitRidden(int PlayerId, string RailroadId, int Cost) : GameEvent;

