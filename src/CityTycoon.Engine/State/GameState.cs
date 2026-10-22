using CityTycoon.Engine.Actions;
using CityTycoon.Engine.Content;
using CityTycoon.Engine.Events;
using CityTycoon.Engine.Randomness;
using CityTycoon.Engine.Steps;

namespace CityTycoon.Engine.State;

public enum GameMode { Classic, Timed }

/// <summary>Hidden: a pass-the-device screen guards private choices. Open: everything is shown (Section 25).</summary>
public enum PrivacyMode { Hidden, Open }

/// <summary>
/// Everything about one game at this moment (Section 20.2).
///
/// This is a plain data object: it holds information but makes no decisions.
/// The rules live in the steps and in <see cref="GameEngine"/>. Keeping the
/// data separate means the whole game can be saved as JSON and loaded back.
/// </summary>
public sealed class GameState
{
    public GameSettings Settings { get; set; } = new();

    /// <summary>The generated ring of 48 spaces, in board order (index 0 is GO).</summary>
    public List<BoardSpace> Board { get; set; } = [];

    /// <summary>Every player, listed in turn order.</summary>
    public List<PlayerState> Players { get; set; } = [];

    /// <summary>Who owns what, keyed by property ID.</summary>
    public Dictionary<string, PropertyState> Properties { get; set; } = [];

    /// <summary>Each district's zoning level, keyed by district ID (Section 10).</summary>
    public Dictionary<string, ZoningLevel> Zoning { get; set; } = [];

    /// <summary>Hotel permits left in the citywide supply (Section 10.3).</summary>
    public int HotelPermits { get; set; }

    /// <summary>Skyscraper permits left in the citywide supply.</summary>
    public int SkyscraperPermits { get; set; }

    /// <summary>Color groups whose rent is doubled by a Stadium visit (Section 15.3).</summary>
    public List<StadiumBoost> StadiumBoosts { get; set; } = [];

    /// <summary>Single properties with boosted rent, like a Grand Opening card (Section 15.4).</summary>
    public List<PropertyBoost> PropertyBoosts { get; set; } = [];

    /// <summary>
    /// The Opportunity draw pile, as card IDs. Index 0 is the top card.
    /// A card with several copies appears several times.
    /// </summary>
    public List<string> OpportunityDeck { get; set; } = [];
    public List<string> OpportunityDiscard { get; set; } = [];

    /// <summary>The Event draw pile. Index 0 is the top card.</summary>
    public List<string> EventDeck { get; set; } = [];
    public List<string> EventDiscard { get; set; } = [];

    /// <summary>The Ordinance draw pile for the City Council (Section 11.4). Index 0 is the top card.</summary>
    public List<string> OrdinanceDeck { get; set; } = [];
    public List<string> OrdinanceDiscard { get; set; } = [];

    /// <summary>Ordinances that passed and are still in force (Section 11.3, step 7).</summary>
    public List<ActiveOrdinance> ActiveOrdinances { get; set; } = [];

    /// <summary>The Mall shop: face-up Opportunity cards, cheapest slot first (Section 15.3).</summary>
    public List<string> MallShop { get; set; } = [];

    public int Round { get; set; } = 1;

    /// <summary>Position in <see cref="Players"/> of the player whose turn it is.</summary>
    public int ActivePlayerIndex { get; set; }

    public Pcg32 Rng { get; set; } = new();

    /// <summary>
    /// The resolution stack (Section 20.3). The last item is the "top", the
    /// step being worked on right now. See docs/ARCHITECTURE.md.
    /// </summary>
    public List<Step> Stack { get; set; } = [];

    /// <summary>The choices the game is waiting for right now.</summary>
    public List<PendingDecision> Pending { get; set; } = [];

    /// <summary>Everything that has happened, in order.</summary>
    public List<GameEvent> Log { get; set; } = [];

    /// <summary>Filled in when the game ends.</summary>
    public GameResult? Result { get; set; }

    public bool IsOver => Result != null;

    /// <summary>The active rule modifiers from the last time they were gathered (see RuleModifiers.Active). Never saved.</summary>
    internal Modifiers.ModifierCache? ModifierCache { get; set; }

    public PlayerState ActivePlayer => Players[ActivePlayerIndex];

    public PlayerState Player(int playerId) =>
        Players.FirstOrDefault(p => p.Id == playerId) ?? throw new KeyNotFoundException($"No player with ID {playerId}.");

    /// <summary>Finds a space's current board index by its ID. Never assume a fixed index (Section 4.3).</summary>
    public int IndexOf(string spaceId)
    {
        int index = Board.FindIndex(s => s.Id == spaceId);
        return index >= 0 ? index : throw new KeyNotFoundException($"No space with ID '{spaceId}' on this board.");
    }

    public BoardSpace Space(string spaceId) => Board[IndexOf(spaceId)];

    /// <summary>Players still in the game, in turn order.</summary>
    public IEnumerable<PlayerState> ActivePlayers => Players.Where(p => !p.IsBankrupt);
}

public sealed class GameSettings
{
    public ulong Seed { get; set; }
    public GameMode Mode { get; set; } = GameMode.Timed;

    /// <summary>The round limit in Timed mode.</summary>
    public int MaxRounds { get; set; }

    public PrivacyMode Privacy { get; set; } = PrivacyMode.Hidden;
}

public sealed class PlayerState
{
    /// <summary>A stable ID (the player's seat number at setup). It doesn't change when turn order is shuffled.</summary>
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int Cash { get; set; }

    /// <summary>Board index of the space the player is on.</summary>
    public int Position { get; set; }

    public bool IsBankrupt { get; set; }
    public bool InJail { get; set; }

    /// <summary>How many Jail turns the player has started (Section 14.3).</summary>
    public int JailTurns { get; set; }

    /// <summary>Doubles rolled in a row this turn. Three sends you to Jail.</summary>
    public int DoublesInARow { get; set; }

    /// <summary>The character this player picked (Section 5), or null in a game without characters.</summary>
    public string? CharacterId { get; set; }

    /// <summary>True once this player has used their Hostile Takeover this lap. Passing GO makes it available again (Section 15.1).</summary>
    public bool TakeoverUsed { get; set; }

    /// <summary>True once the Hustler has rerolled their movement dice this lap. Passing GO makes it available again.</summary>
    public bool MoveRerollUsed { get; set; }

    /// <summary>True once the City Planner has reversed a City Council result (once per game).</summary>
    public bool VoteReversalUsed { get; set; }

    /// <summary>
    /// How many unpaid bank loans the player has (Section 15.7). Setting it (old saves, tests) adds or removes
    /// loans of the old $200 size. It comes before <see cref="LoanBook"/>, so a save that has both reads the real list last.
    /// </summary>
    public int Loans
    {
        get => LoanBook.Count;
        set
        {
            while (LoanBook.Count > value)
                LoanBook.RemoveAt(LoanBook.Count - 1);
            while (LoanBook.Count < value)
                LoanBook.Add(LegacyLoanAmount);
        }
    }

    /// <summary>The size of a loan from before loans came in sizes (decision #154).</summary>
    public const int LegacyLoanAmount = 200;

    /// <summary>The amount borrowed for each unpaid loan, oldest first (decision #154). What paying one back costs depends on its size.</summary>
    public List<int> LoanBook { get; set; } = [];

    /// <summary>Building levels added this turn, for the build limit (Section 9.2, rule 6).</summary>
    public int BuildsThisTurn { get; set; }

    /// <summary>Trade offers made this turn (a limit stops offer spam).</summary>
    public int TradesProposedThisTurn { get; set; }

    /// <summary>Momentum tokens, for Nudges and Precision Rolls (Section 7.3).</summary>
    public int Momentum { get; set; }

    /// <summary>Holds an Insured marker, which cancels one Hospital bill (Section 15.3).</summary>
    public bool IsInsured { get; set; }

    /// <summary>Sent to the Hospital: rolls only 1 die on their next turn while they recover.</summary>
    public bool IsRecovering { get; set; }

    /// <summary>Opportunity cards in hand, as card IDs (Section 15.4). Secret from other players.</summary>
    public List<string> Hand { get; set; } = [];

    /// <summary>Hidden Government tiles this player has secretly peeked at (space IDs).</summary>
    public List<string> KnownTiles { get; set; } = [];

    /// <summary>Extra building levels allowed this turn (Construction Permit).</summary>
    public int ExtraBuildsThisTurn { get; set; }

    /// <summary>Percent off every building level this turn (Construction Permit).</summary>
    public int BuildDiscountThisTurn { get; set; }

    /// <summary>
    /// Road Work: next turn rolls only 1 die unless this many Momentum tokens are spent.
    /// 0 means no Road Work.
    /// </summary>
    public int RoadWorkCost { get; set; }
}

/// <summary>
/// A passed ordinance that is still in force. <paramref name="RoundsLeft"/> counts down at the end of
/// each round; null means it never runs out (Historic Preservation).
/// </summary>
public sealed record ActiveOrdinance(string OrdinanceId, string? DistrictId, int? RoundsLeft);

/// <summary>
/// A color group with doubled rent until the start of the chooser's next turn (Section 15.3).
/// <paramref name="Multiplier"/> is how many times over (3 for the Hustler); 0 in older saves means the normal 2.
/// </summary>
public sealed record StadiumBoost(string GroupId, int ChosenById, int Multiplier = 0);

/// <summary>One property's rent multiplied until the start of the chooser's next turn (Grand Opening).</summary>
public sealed record PropertyBoost(string PropertyId, int ChosenById, int Multiplier);

public sealed class PropertyState
{
    public string PropertyId { get; set; } = "";

    /// <summary>The owner's player ID, or null if the bank owns it.</summary>
    public int? OwnerId { get; set; }

    /// <summary>0 = no buildings, 1-4 = houses, 5 = hotel, 6 = skyscraper.</summary>
    public int Level { get; set; }

    public bool IsMortgaged { get; set; }

    /// <summary>
    /// Won with a down payment and not fully paid yet (Section 8.5). A pending
    /// property collects no rent, can't be built on, mortgaged or traded, and
    /// doesn't count towards sets.
    /// </summary>
    public bool IsPending { get; set; }

    /// <summary>For a pending property: what's still owed, due at the owner's next GO.</summary>
    public int PendingBalance { get; set; }

    /// <summary>True if the owner can count this property: owned, and not pending.</summary>
    public bool IsOwnedOutright(int playerId) => OwnerId == playerId && !IsPending;
}

/// <summary>One space on this game's generated board.</summary>
public sealed class BoardSpace
{
    public int Index { get; set; }
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public SpaceKind Kind { get; set; }

    /// <summary>The district (board side) this space is in, or null for corners.</summary>
    public string? DistrictId { get; set; }

    public TaxType? Tax { get; set; }
    public LandmarkType? Landmark { get; set; }

    /// <summary>For Government tiles: which tile this is. Secret until revealed (see <see cref="IsRevealed"/>).</summary>
    public string? TileId { get; set; }

    /// <summary>For Government tiles: has anyone landed on it yet?</summary>
    public bool IsRevealed { get; set; }
}

public sealed class GameResult
{
    /// <summary>Usually one winner; more than one means a shared win (Section 16.3).</summary>
    public List<int> WinnerIds { get; set; } = [];
    public string Reason { get; set; } = "";
    public List<Standing> Standings { get; set; } = [];
}

/// <summary>One line of the final scoreboard.</summary>
public sealed record Standing(int PlayerId, int Rank, int NetWorth, int Cash, bool IsBankrupt);
