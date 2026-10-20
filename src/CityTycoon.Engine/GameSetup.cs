using CityTycoon.Engine.State;

namespace CityTycoon.Engine;

/// <summary>The choices made on the new-game screen (Section 6).</summary>
public sealed class GameSetup
{
    /// <summary>The players, in seat order. Each player's ID is their seat number (0, 1, 2...).</summary>
    public List<PlayerSetup> Players { get; set; } = [];

    public GameMode Mode { get; set; } = GameMode.Timed;

    /// <summary>The round limit for Timed mode. Leave empty to use rules.json's default.</summary>
    public int? MaxRounds { get; set; }

    public PrivacyMode Privacy { get; set; } = PrivacyMode.Hidden;

    /// <summary>A seed replays the same game. Leave empty for a random one.</summary>
    public ulong? Seed { get; set; }

    /// <summary>Normally true. Tests turn it off so seat 0 always goes first.</summary>
    public bool ShuffleTurnOrder { get; set; } = true;

    /// <summary>How the players who haven't been given a character get one (Section 5).</summary>
    public CharacterMode Characters { get; set; } = CharacterMode.None;
}

/// <summary>How characters are handed out when a game starts (Section 5).</summary>
public enum CharacterMode
{
    /// <summary>No characters, unless a player was given one in <see cref="PlayerSetup"/>. Handy for tests of the plain rules.</summary>
    None,

    /// <summary>Players pick in reverse turn order, so the last player picks first. Bots pick at random.</summary>
    Pick,

    /// <summary>Everyone is dealt a random, different character.</summary>
    Random,
}

/// <param name="CharacterId">A character chosen before the game starts. Leave empty to pick (or be dealt) one later.</param>
public sealed record PlayerSetup(string Name, string? CharacterId = null);
