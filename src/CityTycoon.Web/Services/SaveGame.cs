using System.Text.Json;
using System.Text.Json.Serialization;
using CityTycoon.AI;
using CityTycoon.Engine.State;

namespace CityTycoon.Web.Services;

/// <summary>
/// A save file (Phase 9): the whole game state as JSON, plus who is a person and who is a bot.
/// The game state alone is enough to carry on exactly where it stopped, because the random
/// generator's position is part of it.
/// <see cref="Notes"/> holds each bot's private notebook (decision #150), so the game log download
/// still has the bots' thinking after a game is loaded. Older saves have none, which is fine.
/// </summary>
public sealed record SavedGame(int Version, string SavedAt, Dictionary<int, SeatKind> Seats, List<int> AutoPassReactions, string State,
    Dictionary<int, List<BotNote>>? Notes = null)
{
    public const int CurrentVersion = 1;

    private static readonly JsonSerializerOptions Options = new() { Converters = { new JsonStringEnumConverter() } };

    public string ToJson() => JsonSerializer.Serialize(this, Options);

    /// <summary>Reads a save file. Throws <see cref="InvalidDataException"/> with a plain message if it isn't one of ours.</summary>
    public static SavedGame FromJson(string json)
    {
        try
        {
            var save = JsonSerializer.Deserialize<SavedGame>(json, Options);
            if (save == null || string.IsNullOrEmpty(save.State))
                throw new InvalidDataException("That file doesn't look like a City Tycoon save.");
            if (save.Version > CurrentVersion)
                throw new InvalidDataException("That save is from a newer version of the game.");
            GameStateSerializer.FromJson(save.State);   // make sure the game inside can be read
            return save;
        }
        catch (JsonException)
        {
            throw new InvalidDataException("That file doesn't look like a City Tycoon save.");
        }
    }

    /// <summary>A one-line description for the "continue" button, like "Round 12 of 30, 4 players".</summary>
    public string Describe()
    {
        var state = GameStateSerializer.FromJson(State);
        string rounds = state.Settings.Mode == GameMode.Timed ? $"Round {state.Round} of {state.Settings.MaxRounds}" : $"Round {state.Round}";
        return $"{rounds}, {state.Players.Count} players ({string.Join(", ", state.Players.Select(p => p.Name))}), saved {SavedAt}";
    }
}
