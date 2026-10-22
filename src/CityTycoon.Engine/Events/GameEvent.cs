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
[JsonDerivedType(typeof(BidRaised), nameof(BidRaised))]
[JsonDerivedType(typeof(AuctionPassed), nameof(AuctionPassed))]
[JsonDerivedType(typeof(DiceTieWon), nameof(DiceTieWon))]
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
[JsonDerivedType(typeof(TradeCountered), nameof(TradeCountered))]
[JsonDerivedType(typeof(RaisingMoney), nameof(RaisingMoney))]
[JsonDerivedType(typeof(MomentumGained), nameof(MomentumGained))]
[JsonDerivedType(typeof(PrecisionRolled), nameof(PrecisionRolled))]
[JsonDerivedType(typeof(Nudged), nameof(Nudged))]
[JsonDerivedType(typeof(InsuranceBought), nameof(InsuranceBought))]
[JsonDerivedType(typeof(InsuranceUsed), nameof(InsuranceUsed))]
[JsonDerivedType(typeof(SentToHospital), nameof(SentToHospital))]
[JsonDerivedType(typeof(HospitalBillPaid), nameof(HospitalBillPaid))]
[JsonDerivedType(typeof(CasinoHighLow), nameof(CasinoHighLow))]
[JsonDerivedType(typeof(CasinoCoinFlip), nameof(CasinoCoinFlip))]
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
[JsonDerivedType(typeof(TileSwapped), nameof(TileSwapped))]
[JsonDerivedType(typeof(TileFeeWaived), nameof(TileFeeWaived))]
[JsonDerivedType(typeof(EventsArranged), nameof(EventsArranged))]
[JsonDerivedType(typeof(RecoverySkipped), nameof(RecoverySkipped))]
[JsonDerivedType(typeof(OwnerKeptProperty), nameof(OwnerKeptProperty))]
[JsonDerivedType(typeof(TileRevealed), nameof(TileRevealed))]
[JsonDerivedType(typeof(TileFeePaid), nameof(TileFeePaid))]
[JsonDerivedType(typeof(CatchUpPaid), nameof(CatchUpPaid))]
[JsonDerivedType(typeof(CouncilStarted), nameof(CouncilStarted))]
[JsonDerivedType(typeof(CouncilCancelled), nameof(CouncilCancelled))]
[JsonDerivedType(typeof(OrdinanceProposed), nameof(OrdinanceProposed))]
[JsonDerivedType(typeof(CouncilResolved), nameof(CouncilResolved))]
[JsonDerivedType(typeof(OrdinanceEnded), nameof(OrdinanceEnded))]
[JsonDerivedType(typeof(ZoningChanged), nameof(ZoningChanged))]
[JsonDerivedType(typeof(PermitsAdded), nameof(PermitsAdded))]
[JsonDerivedType(typeof(CouncilPayout), nameof(CouncilPayout))]
[JsonDerivedType(typeof(CouncilPaymentMade), nameof(CouncilPaymentMade))]
[JsonDerivedType(typeof(TileRevealedByCouncil), nameof(TileRevealedByCouncil))]
[JsonDerivedType(typeof(TransitHopped), nameof(TransitHopped))]
[JsonDerivedType(typeof(TransitRidden), nameof(TransitRidden))]
[JsonDerivedType(typeof(CharacterChosen), nameof(CharacterChosen))]
[JsonDerivedType(typeof(ExpressLineUsed), nameof(ExpressLineUsed))]
[JsonDerivedType(typeof(HostileTakeoverUsed), nameof(HostileTakeoverUsed))]
[JsonDerivedType(typeof(PerkIncome), nameof(PerkIncome))]
[JsonDerivedType(typeof(PerkPaid), nameof(PerkPaid))]
[JsonDerivedType(typeof(CasinoRerolled), nameof(CasinoRerolled))]
[JsonDerivedType(typeof(MoveRerolled), nameof(MoveRerolled))]
[JsonDerivedType(typeof(CouncilReversed), nameof(CouncilReversed))]
[JsonDerivedType(typeof(VoteDealMade), nameof(VoteDealMade))]
[JsonDerivedType(typeof(VoteDealDeclined), nameof(VoteDealDeclined))]
[JsonDerivedType(typeof(CardKept), nameof(CardKept))]
[JsonDerivedType(typeof(HospitalShrugged), nameof(HospitalShrugged))]
[JsonDerivedType(typeof(BidEdgeWon), nameof(BidEdgeWon))]
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
public sealed record AuctionStarted(string PropertyId, int MinBid, bool Open = false) : GameEvent;

/// <summary>In an open auction, a player raised the bid.</summary>
public sealed record BidRaised(string PropertyId, int PlayerId, int Amount, bool DownPayment = false) : GameEvent;

/// <summary>In an open auction, a player dropped out.</summary>
public sealed record AuctionPassed(string PropertyId, int PlayerId) : GameEvent;

/// <summary>A dice tie was settled in a player's favor by their character (the Mogul).</summary>
public sealed record DiceTieWon(int PlayerId, string Source) : GameEvent;

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
/// <param name="BuildingsSoldFor">What the bank paid for their buildings first. 0 in saves from before this was recorded.</param>
/// <param name="LoansPaidBack">What went to the bank to pay back their loans. 0 in saves from before this was recorded.</param>
public sealed record PlayerBankrupt(int PlayerId, int? CreditorId, int CashHandedOver, IReadOnlyList<string> PropertiesHandedOver,
    int BuildingsSoldFor = 0, int LoansPaidBack = 0) : GameEvent;
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
/// <param name="FromFees">The part of the fees the player who offered paid. <paramref name="ToFees"/> is the other player's part.
/// Both are 0 in saves from before this was recorded.</param>
public sealed record TradeCompleted(Actions.TradeOffer Offer, int MortgageFees, int FromFees = 0, int ToFees = 0) : GameEvent;
/// <param name="Reason">"declined", or why the trade couldn't happen when it was accepted.</param>
/// <param name="Why">What the player who declined said about it (bots explain themselves), or null.</param>
public sealed record TradeDeclined(Actions.TradeOffer Offer, string Reason, string? Why = null) : GameEvent;

/// <summary>Someone answered a trade offer with different terms. <paramref name="Counter"/> is written from their side.</summary>
public sealed record TradeCountered(Actions.TradeOffer Offer, Actions.TradeOffer Counter, string? Why = null) : GameEvent;

/// <summary>A player owes more cash than they have and must raise money first.</summary>
public sealed record RaisingMoney(int PlayerId, int AmountOwed) : GameEvent;

/// <summary>Momentum tokens gained (already limited by the token cap). <paramref name="Source"/> says why, like "GO".</summary>
public sealed record MomentumGained(int PlayerId, int Amount, string Source) : GameEvent;

/// <summary>A Precision Roll: the tokens spent and all 3 dice, before choosing 2 (Section 7.3).</summary>
public sealed record PrecisionRolled(int PlayerId, int TokensSpent, IReadOnlyList<int> Dice) : GameEvent;

/// <summary>A Nudge: moved <paramref name="Spaces"/> more (or fewer, if negative) than rolled.</summary>
public sealed record Nudged(int PlayerId, int Spaces, int TokensSpent) : GameEvent;

/// <summary>An Insured marker was bought. <paramref name="ToId"/> is who got the money: the Medic (decision #153), or null for the bank.</summary>
public sealed record InsuranceBought(int PlayerId, int Cost, int? ToId = null) : GameEvent;

/// <summary>An Insured marker was used up to cancel a Hospital bill.</summary>
public sealed record InsuranceUsed(int PlayerId) : GameEvent;

public sealed record SentToHospital(int PlayerId, string Reason) : GameEvent;
/// <summary>A Hospital bill was paid. <paramref name="ToId"/> is who got the money: the Medic (decision #153), or null for the bank.</summary>
public sealed record HospitalBillPaid(int PlayerId, int Amount, int? ToId = null) : GameEvent;

/// <summary>Landing on the Casino flips a coin (decision #153): the player wins or loses a little. <paramref name="Net"/> is plus or minus.</summary>
public sealed record CasinoCoinFlip(int PlayerId, int Net) : GameEvent;

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
/// <param name="Spaces">How far from where the mover would have landed (after their Nudge).</param>
/// <param name="CancelledNudge">True when the Detour undid the mover's Nudge (Decision #140).</param>
public sealed record Detoured(int ByPlayerId, int MoverId, int Spaces, bool CancelledNudge = false) : GameEvent;

/// <summary>Fire!: a property lost a building level.</summary>
public sealed record BuildingBurned(int PlayerId, string PropertyId, int NewLevel) : GameEvent;

/// <summary>Fire!: nothing burned, for this reason.</summary>
public sealed record FireAvoided(int PlayerId, string Reason) : GameEvent;

public sealed record LotteryRoll(int PlayerId, int Die);

/// <summary>One round of Lottery rolls. Ties roll again, so there can be several.</summary>
public sealed record LotteryRolled(IReadOnlyList<LotteryRoll> Rolls) : GameEvent;
/// <param name="Stake">What each player in <paramref name="Entrants"/> put in. 0 and null in saves from before this was recorded.</param>
public sealed record LotteryWon(int WinnerId, int Pot, int Stake = 0, IReadOnlyList<int>? Entrants = null) : GameEvent;

public sealed record RoadWorkStarted(int PlayerId) : GameEvent;
public sealed record RoadWorkCleared(int PlayerId, int TokensSpent) : GameEvent;

public sealed record RentBoostStarted(int PlayerId, string PropertyId, int Multiplier) : GameEvent;
public sealed record RentBoostEnded(int PlayerId, string PropertyId) : GameEvent;

/// <summary>Inside Source: a player secretly looked at a hidden tile. Only they learn what it is.</summary>
public sealed record TilePeeked(int PlayerId, string SpaceId) : GameEvent;

/// <summary>The City Planner swapped a hidden tile for one not in play. Nobody else learns either tile (decision #158).</summary>
public sealed record TileSwapped(int PlayerId, string SpaceId) : GameEvent;

/// <summary>The City Planner landed on a Government tile and paid no fee (decision #158).</summary>
public sealed record TileFeeWaived(int PlayerId, string TileId, int Amount) : GameEvent;

/// <summary>Tip-Off: a player looked at the top Event cards and put them back. The order stays secret.</summary>
public sealed record EventsArranged(int PlayerId, int Count) : GameEvent;

/// <summary>Get Out of Jail Free used to skip the 1-die Hospital recovery turn.</summary>
public sealed record RecoverySkipped(int PlayerId) : GameEvent;

/// <summary>A forced auction got no bids, so the owner keeps the property (Section 8.6).</summary>
public sealed record OwnerKeptProperty(string PropertyId, int OwnerId) : GameEvent;

/// <summary>Someone landed on a hidden Government tile, so everyone now knows what it is (Section 12.1).</summary>
public sealed record TileRevealed(int PlayerId, string SpaceId, string TileId) : GameEvent;

/// <summary>A tile's landing fee (Police Station ticket, Landfill cleanup, City Hall, Power Plant).</summary>
public sealed record TileFeePaid(int FromId, int? ToId, int Amount, string TileId) : GameEvent;

/// <summary>Instead of paying rent next to a Transit Hub, a player paid the fee and hopped onto the hub.</summary>
public sealed record TransitHopped(int PlayerId, string PropertyId, int Cost) : GameEvent;

/// <summary>A player paid the Transit Hub fare to ride to a railroad.</summary>
public sealed record TransitRidden(int PlayerId, string RailroadId, int Cost) : GameEvent;


/// <summary>A player picked (or was dealt) a character at the start of the game (Section 5). Everyone sees it.</summary>
public sealed record CharacterChosen(int PlayerId, string CharacterId) : GameEvent;

/// <summary>Express Line: a player paid to jump to a railroad they own, then rolls 1 die (Section 13.3).</summary>
public sealed record ExpressLineUsed(int PlayerId, string RailroadId, int Cost) : GameEvent;

/// <summary>The Red perk: a player forced an auction on an opponent's property (Section 8.6).</summary>
public sealed record HostileTakeoverUsed(int PlayerId, string PropertyId) : GameEvent;

/// <summary>A perk paid its owner (Community Roots, Dividends). <paramref name="Perk"/> is the perk's name.</summary>
public sealed record PerkIncome(int PlayerId, int Amount, string Perk) : GameEvent;

/// <summary>A perk made one player pay another (Utility Bill, Neighborhood Watch, Power Grid).</summary>
public sealed record PerkPaid(int FromId, int ToId, int Amount, string Perk) : GameEvent;

/// <summary>The Hustler used their one reroll. <paramref name="What"/> says what was rerolled.</summary>
public sealed record CasinoRerolled(int PlayerId, string What) : GameEvent;

/// <summary>
/// The Hustler rerolled one die of this movement roll (once per lap, decisions #152 and #153).
/// <paramref name="Die1"/> and <paramref name="Die2"/> are the old dice; <paramref name="Die"/> is which one (0 or 1) was rolled again.
/// Saves from before decision #153 have no Die: then the whole roll was thrown again.
/// </summary>
public sealed record MoveRerolled(int PlayerId, int Die1, int Die2, int? Die = null) : GameEvent;

/// <summary>The City Planner reversed a City Council result (once per game). <paramref name="NowPassed"/> is the new result.</summary>
/// <summary>A vote deal was accepted: the cash moved, and <c>Deal.ToId</c> must vote <c>Deal.Choice</c> (decision #159).</summary>
public sealed record VoteDealMade(Actions.VoteDeal Deal) : GameEvent;

/// <summary>A vote deal was turned down (decision #159).</summary>
public sealed record VoteDealDeclined(Actions.VoteDeal Deal) : GameEvent;

public sealed record CouncilReversed(int PlayerId, string OrdinanceId, string? DistrictId, bool NowPassed) : GameEvent;

/// <summary>The Arts District perk: a player drew two Opportunity cards and kept one (the other is discarded, unseen).</summary>
public sealed record CardKept(int PlayerId) : GameEvent;

/// <summary>The Medic was sent to the Hospital and shrugged it off.</summary>
public sealed record HospitalShrugged(int PlayerId) : GameEvent;

/// <summary>The Mogul's edge won an auction their real bid did not top.</summary>
public sealed record BidEdgeWon(int PlayerId, string PropertyId, int Bid, int TopBid) : GameEvent;

// ----- City Council (Section 11) -----

/// <summary>A City Council session begins. A Regular one is at the end of every 4th round; an Emergency one comes from City Hall or a card.</summary>
public sealed record CouncilStarted(bool Emergency, int SetterId) : GameEvent;

/// <summary>A session was called off, for example because no ordinance in the deck could do anything.</summary>
public sealed record CouncilCancelled(string Reason) : GameEvent;

/// <summary>The agenda setter chose this ordinance (and district, if it needs one) to put to a vote.</summary>
public sealed record OrdinanceProposed(int SetterId, string OrdinanceId, string? DistrictId) : GameEvent;

/// <summary>One player's revealed vote. <paramref name="Influence"/> is everything they added to their side.</summary>
public sealed record VoteReveal(int PlayerId, Actions.VoteChoice Choice, int Lobby, bool UsedLobbyist, int Influence);

/// <summary>
/// The votes are in and the roll is made (Section 11.3). <paramref name="Roll"/> is a number from 0
/// up to (but not including) Yes + No; the ordinance passes when the roll is below the Yes total.
/// </summary>
public sealed record CouncilResolved(string OrdinanceId, string? DistrictId, IReadOnlyList<VoteReveal> Votes,
    int YesInfluence, int NoInfluence, int Roll, bool Passed) : GameEvent;

/// <summary>A timed ordinance ran out.</summary>
public sealed record OrdinanceEnded(string OrdinanceId, string? DistrictId) : GameEvent;

/// <summary>A district's zoning level changed.</summary>
public sealed record ZoningChanged(string DistrictId, Content.ZoningLevel From, Content.ZoningLevel To, string OrdinanceId) : GameEvent;

public sealed record PermitsAdded(int Hotels, int Skyscrapers) : GameEvent;

/// <summary>An ordinance paid a player (Stimulus Check).</summary>
public sealed record CouncilPayout(int PlayerId, int Amount, string OrdinanceId) : GameEvent;

/// <summary>A player paid the bank because of an ordinance (Property Tax Hike, Infrastructure Bond).</summary>
public sealed record CouncilPaymentMade(int PlayerId, int Amount, string OrdinanceId) : GameEvent;

/// <summary>Public Works revealed a hidden Government tile.</summary>
public sealed record TileRevealedByCouncil(string SpaceId, string TileId) : GameEvent;

/// <summary>The catch-up bonus: the player with the lowest net worth was paid extra for passing GO (Section 16.2).</summary>
public sealed record CatchUpPaid(int PlayerId, int Amount) : GameEvent;
