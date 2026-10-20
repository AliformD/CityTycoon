using System.Text.Json;

namespace CityTycoon.Engine.Content;

/// <summary>Which pile a card belongs to.</summary>
public enum CardDeck { Opportunity, Event }

/// <summary>When a card is used (Section 15.4).</summary>
public enum CardTiming
{
    /// <summary>Event cards: they happen as soon as they're drawn.</summary>
    Immediate,

    /// <summary>Played from your hand on your own turn.</summary>
    OwnTurn,

    /// <summary>Played from your hand when its <see cref="CardTrigger"/> happens, even on someone else's turn.</summary>
    Reaction,

    /// <summary>Played during a City Council vote (Phase 8).</summary>
    Council,
}

/// <summary>What opens a Reaction window (Section 17.2).</summary>
public enum CardTrigger
{
    None,

    /// <summary>You owe someone rent (Rent Strike).</summary>
    RentOwed,

    /// <summary>Another player played a card against you (Insurance).</summary>
    CardPlayedAgainstYou,

    /// <summary>Another player has rolled and nudged and is about to move (Detour).</summary>
    OpponentMoving,
}

/// <summary>What the player picks when playing a card.</summary>
public enum CardTarget { None, Opponent, OwnProperty, OpponentProperty, HiddenTile, Space }

/// <summary>An opportunity.json or events.json file (Section 21.2). Extra files (like opportunity.expansion1.json) load alongside.</summary>
public sealed record CardsFile(int ContentVersion, IReadOnlyList<CardDefinition> Cards);

/// <summary>
/// One card. The <see cref="Effect"/> names an effect type (a small C# class)
/// plus its settings, so a new card that reuses an effect type is just a JSON edit.
/// </summary>
/// <param name="Copies">How many of this card are in the deck.</param>
public sealed record CardDefinition(
    string Id,
    CardDeck Deck,
    string Name,
    string Text,
    JsonElement Effect,
    int Copies = 1,
    CardTiming Timing = CardTiming.Immediate,
    CardTrigger Trigger = CardTrigger.None,
    CardTarget Target = CardTarget.None)
{
    /// <summary>The effect type's name, like "CollectFromBank".</summary>
    public string EffectType => Effect.TryGetProperty("type", out JsonElement t) ? t.GetString() ?? "" : "";

    /// <summary>A whole-number setting from the effect, like "amount".</summary>
    public int Int(string name, int? fallback = null) =>
        Effect.TryGetProperty(name, out JsonElement v) && v.TryGetInt32(out int i) ? i
        : fallback ?? throw new ContentException($"Card '{Id}' needs a whole number \"{name}\" in its effect.");

    /// <summary>A number setting that may have a decimal point, like "multiplier": 0.5.</summary>
    public double Number(string name, double? fallback = null) =>
        Effect.TryGetProperty(name, out JsonElement v) && v.TryGetDouble(out double d) ? d
        : fallback ?? throw new ContentException($"Card '{Id}' needs a number \"{name}\" in its effect.");

    public string Setting(string name, string? fallback = null) =>
        Effect.TryGetProperty(name, out JsonElement v) && v.ValueKind == JsonValueKind.String ? v.GetString()!
        : fallback ?? throw new ContentException($"Card '{Id}' needs a text setting \"{name}\" in its effect.");

    public bool Flag(string name) =>
        Effect.TryGetProperty(name, out JsonElement v) && v.ValueKind == JsonValueKind.True;
}
