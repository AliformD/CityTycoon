namespace CityTycoon.Engine.Content;

/// <summary>
/// Every tunable number in the game, loaded from <c>content/rules.json</c>.
///
/// Why a class instead of reading the JSON directly? A typed class means the
/// compiler catches typos like <c>rules.GoSalery</c>, and your editor can
/// auto-complete the names. The <c>required</c> keyword means loading fails
/// loudly if a key is missing from the file, instead of quietly using 0.
/// </summary>
public sealed class RulesConfig
{
    public required int ContentVersion { get; init; }

    // Section 6 and 7.3: starting values and Momentum tokens.
    public required int StartingCash { get; init; }
    public required int StartingMomentum { get; init; }
    public required int MomentumCap { get; init; }
    /// <summary>The furthest a Nudge can move you, either way (the Commuter adds more).</summary>
    public required int NudgeMaxSpaces { get; init; }
    /// <summary>Momentum tokens a Nudge costs for each space it moves you (decision #151).</summary>
    public required int NudgeCost { get; init; }
    public required int PrecisionRollCost { get; init; }

    // Section 15.1, 15.2 and 16.2: GO and taxes.
    public required int GoSalary { get; init; }
    public required int GoMomentumGain { get; init; }
    public required int CatchUpBonus { get; init; }
    public required int HandCardNetWorth { get; init; }
    public required int IncomeTax { get; init; }
    public required int LuxuryTax { get; init; }

    // Section 8: auctions and down payments.
    public required int AuctionMinBidPercent { get; init; }
    public required int ForcedAuctionMinBidPercent { get; init; }
    public required int BidIncrement { get; init; }
    public required int DownPaymentPercent { get; init; }
    public required int MaxPendingProperties { get; init; }

    // Section 8.7 and 15.8: mortgages.
    public required int MortgagePercent { get; init; }
    public required int UnmortgageFeePercent { get; init; }

    // Section 9 and 10: building and permits.
    public required int BuildLimitPerTurn { get; init; }
    public required int SkyscraperCostMultiplier { get; init; }
    public required double SkyscraperRentMultiplier { get; init; }
    public required int MaxBuildDiscountPercent { get; init; }

    // Section 13.1, step 2: percentage rent changes (tiles and more) add up, then stay in this range.
    public required int MinRentPercent { get; init; }
    public required int MaxRentPercent { get; init; }
    public required IReadOnlyList<PermitSupply> PermitsByPlayerCount { get; init; }

    // Section 11: City Council.
    public required int CouncilEveryNRounds { get; init; }
    public required int VoteInfluence { get; init; }
    public required int PublicOpinionInfluence { get; init; }

    // Section 15.7: loans.
    /// <summary>The loans the bank offers (decision #154): how much it lends, and what paying it back costs. Bigger loans cost more interest.</summary>
    public required IReadOnlyList<LoanTerms> LoanOptions { get; init; }
    public required int MaxLoans { get; init; }

    /// <summary>The terms for a loan of this size. A size the bank doesn't offer (a loan from an old save) is charged 25% interest.</summary>
    public LoanTerms Loan(int amount) => LoanOptions.FirstOrDefault(o => o.Amount == amount) ?? new LoanTerms(amount, amount * 5 / 4);

    /// <summary>The biggest loan the bank offers.</summary>
    public int BiggestLoan => LoanOptions.Count == 0 ? 0 : LoanOptions.Max(o => o.Amount);

    // Section 14: Jail.
    public required int JailBail { get; init; }
    public required int MaxJailTurns { get; init; }
    public required double JailRentMultiplier { get; init; }

    // Section 15.3 and 15.4: landmarks and cards.
    public required int HospitalBill { get; init; }
    public required int InsuredMarkerCost { get; init; }
    public required IReadOnlyList<int> MallPrices { get; init; }
    public required int MallSellPrice { get; init; }
    public required int HandLimit { get; init; }

    // Section 16: game modes.
    public required int TimedModeDefaultRounds { get; init; }

    // Numbers the spec mentions outside the Section 18 table.
    // They live here too, so nothing in the code is hard-coded.
    public required int MinPlayers { get; init; }
    public required int MaxPlayers { get; init; }
    public required IReadOnlyList<int> RailroadRents { get; init; }
    public required IReadOnlyList<int> UtilityRentMultipliers { get; init; }
    public required int MaxDoublesBeforeJail { get; init; }

    // Section 9 and 10: building levels, zoning and selling.
    /// <summary>The zoning levels from lowest to highest, with the tallest building each allows.</summary>
    public required IReadOnlyList<ZoningRule> ZoningLevels { get; init; }
    public required ZoningLevel StartingZoning { get; init; }
    /// <summary>How many random districts start one level higher (Commercial) (Section 6, step 8).</summary>
    public required int StartingCommercialDistricts { get; init; }
    /// <summary>Building level 5 is a hotel, level 6 a skyscraper (Section 9.1).</summary>
    public required int HotelLevel { get; init; }
    public required int SkyscraperLevel { get; init; }
    /// <summary>Selling a building level gives back this percent of what it cost (Section 9.3).</summary>
    public required int BuildingSellPercent { get; init; }
    /// <summary>Discounted building costs are rounded down to a multiple of this (Section 9.4).</summary>
    public required int BuildDiscountRoundTo { get; init; }

    /// <summary>A limit so a player (or a bot) can't flood others with trade offers in one turn.</summary>
    public required int MaxTradeProposalsPerTurn { get; init; }
    /// <summary>How many counter-offers one trade can go through before someone must accept or decline, so a haggle can't go on forever.</summary>
    public required int MaxTradeCounters { get; init; }

    // Section 7.3 and 15.3: Precision Roll and landmarks.
    /// <summary>How many dice a Precision Roll rolls (you keep 2).</summary>
    public required int PrecisionRollDice { get; init; }
    /// <summary>Dice Poker at the Casino (decision #164): the free chip the house gives everyone who lands there.</summary>
    public required int CasinoFreeChip { get; init; }
    /// <summary>What each Dice Poker hand pays back for every $100 riding on it (the stake included).</summary>
    public required CasinoPayTable CasinoPays { get; init; }
    /// <summary>A Showdown (decision #164): what each player puts in, and what the challenged player pays to fold instead.</summary>
    public required int ShowdownStake { get; init; }
    public required int ShowdownFoldPayment { get; init; }
    /// <summary>House rule: allow down payments in open auctions too. Off by default, so only silent auctions take them (decision #163).</summary>
    public bool OpenAuctionDownPayments { get; init; }
    /// <summary>
    /// Before a council vote, players may bid to pay the Politician to vote their way (decision #162). Only matters
    /// when a Politician is playing. Off: the Politician just votes, and may still lobby.
    /// </summary>
    public bool VoteContracts { get; init; }
    public required int StadiumRentMultiplier { get; init; }
    /// <summary>Momentum tokens gained at City Park.</summary>
    public required int CityParkMomentum { get; init; }

    /// <summary>The tallest building level allowed under a zoning level (Section 10.1).</summary>
    public int MaxBuildingLevel(ZoningLevel level) =>
        ZoningLevels.First(z => z.Level == level).MaxBuildingLevel;

    /// <summary>Finds the hotel and skyscraper permit supply for a player count (Section 10.3).</summary>
    public PermitSupply PermitsFor(int playerCount) =>
        PermitsByPlayerCount.FirstOrDefault(p => playerCount >= p.MinPlayers && playerCount <= p.MaxPlayers)
        ?? throw new ArgumentOutOfRangeException(nameof(playerCount), $"No permit supply is set for {playerCount} players.");
}

/// <summary>Zoning levels, lowest first (Section 10.1).</summary>
public enum ZoningLevel { Restricted, Residential, Commercial, HighRise }

public sealed record ZoningRule(ZoningLevel Level, int MaxBuildingLevel);

/// <summary>One row of the permit table in Section 10.3.</summary>
public sealed record PermitSupply(int MinPlayers, int MaxPlayers, int HotelPermits, int SkyscraperPermits);

/// <summary>One kind of bank loan: borrow <paramref name="Amount"/>, pay back <paramref name="Repay"/> (Section 15.7).</summary>
public sealed record LoanTerms(int Amount, int Repay)
{
    /// <summary>What the loan costs on top of the money borrowed.</summary>
    public int Interest => Repay - Amount;
}

/// <summary>
/// The Dice Poker pay table, as dollars paid back for every $100 on the hand (300 means 3 times).
/// Hands left out pay nothing.
/// </summary>
public sealed record CasinoPayTable
{
    public int FiveOfAKind { get; init; }
    public int FourOfAKind { get; init; }
    public int FullHouse { get; init; }
    public int Straight { get; init; }
    public int ThreeOfAKind { get; init; }
    public int TwoPair { get; init; }
    public int Pair { get; init; }
}
