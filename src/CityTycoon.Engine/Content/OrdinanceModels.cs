using System.Text.Json;

namespace CityTycoon.Engine.Content;

/// <summary>ordinances.json (Section 11.4): the deck the City Council draws from.</summary>
public sealed record OrdinancesFile(int ContentVersion, IReadOnlyList<OrdinanceDefinition> Ordinances);

/// <summary>What an ordinance needs picked before it can be voted on.</summary>
public enum OrdinanceTarget { None, District }

/// <summary>
/// One Ordinance card. Like a card, the <see cref="Effect"/> names an effect type (a small C#
/// class in the Ordinances folder) plus its settings, so a new ordinance that reuses an
/// effect type is just a JSON edit.
/// </summary>
/// <param name="Copies">How many of this card are in the deck.</param>
/// <param name="Target">"district" means the agenda setter also picks a district.</param>
/// <param name="Duration">How many rounds a passed ordinance lasts. Leave it out for one that happens once (or is permanent).</param>
/// <param name="Lasting">True for an ordinance that stays on the table forever (Historic Preservation).</param>
public sealed record OrdinanceDefinition(
    string Id,
    string Name,
    string Text,
    JsonElement Effect,
    int Copies = 1,
    OrdinanceTarget Target = OrdinanceTarget.None,
    int? Duration = null,
    bool Lasting = false)
{
    /// <summary>The effect type's name, like "Rezone".</summary>
    public string EffectType => Effect.TryGetProperty("type", out JsonElement t) ? t.GetString() ?? "" : "";

    /// <summary>True if passing this ordinance puts it on the table for a while (or forever).</summary>
    public bool StaysActive => Duration != null || Lasting;

    public int Int(string name, int? fallback = null) =>
        Effect.TryGetProperty(name, out JsonElement v) && v.TryGetInt32(out int i) ? i
        : fallback ?? throw new ContentException($"Ordinance '{Id}' needs a whole number \"{name}\" in its effect.");
}
