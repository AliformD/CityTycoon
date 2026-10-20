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
    public required int NudgeMaxSpaces { get; init; }
    public required int NudgeCost { get; init; }
    public required int PrecisionRollCost { get; init; }

    // Section 15.1, 15.2 and 16.2: GO and taxes.
    public required int GoSalary { get; init; }
    public required int GoMomentumGain { get; init; }
    public required int CatchUpBonus { get; init; }
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
    public required IReadOnlyList<PermitSupply> PermitsByPlayerCount { get; init; }

    // Section 11: City Council.
    public required int CouncilEveryNRounds { get; init; }
    public required int VoteInfluence { get; init; }
    public required int PoliticianVoteInfluence { get; init; }
    public required int PublicOpinionInfluence { get; init; }

    // Section 15.7: loans.
    public required int LoanAmount { get; init; }
    public required int LoanRepay { get; init; }
    public required int MaxLoans { get; init; }

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

    /// <summary>Finds the hotel and skyscraper permit supply for a player count (Section 10.3).</summary>
    public PermitSupply PermitsFor(int playerCount) =>
        PermitsByPlayerCount.FirstOrDefault(p => playerCount >= p.MinPlayers && playerCount <= p.MaxPlayers)
        ?? throw new ArgumentOutOfRangeException(nameof(playerCount), $"No permit supply is set for {playerCount} players.");
}

/// <summary>One row of the permit table in Section 10.3.</summary>
public sealed record PermitSupply(int MinPlayers, int MaxPlayers, int HotelPermits, int SkyscraperPermits);
