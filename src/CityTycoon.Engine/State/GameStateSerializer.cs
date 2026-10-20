using System.Text.Json;
using CityTycoon.Engine.Content;

namespace CityTycoon.Engine.State;

/// <summary>Saves a whole game to JSON text and loads it back (used by tests now, save files in Phase 9).</summary>
public static class GameStateSerializer
{
    public static string ToJson(GameState state) => JsonSerializer.Serialize(state, ContentLoader.JsonOptions);

    public static GameState FromJson(string json) =>
        JsonSerializer.Deserialize<GameState>(json, ContentLoader.JsonOptions)
        ?? throw new JsonException("The saved game is empty.");
}
