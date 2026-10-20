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
}

public sealed record PlayerSetup(string Name);
