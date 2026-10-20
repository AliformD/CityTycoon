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

    /// <summary>Unpaid bank loans (Section 15.7).</summary>
    public int Loans { get; set; }

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
}

/// <summary>A color group with doubled rent until the start of the chooser's next turn (Section 15.3).</summary>
public sealed record StadiumBoost(string GroupId, int ChosenById);

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
