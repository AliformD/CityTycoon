using CityTycoon.Engine.Content;

namespace CityTycoon.Engine.Modifiers;

/// <summary>
/// What a modifier is told when it is created from a content file: its settings,
/// who owns it, and where it came from.
/// </summary>
/// <param name="Spec">The entry in the JSON file, like <c>{ "type": "GoCash", "value": 50 }</c>.</param>
/// <param name="OwnerId">The player whose character or perk this is.</param>
/// <param name="Source">What to call it in the log, like "Community Roots" or "The Mogul".</param>
/// <param name="GroupId">For a color perk: the color group it comes from. Null for characters and bonuses.</param>
public sealed record ModifierContext(ModifierSpec Spec, int OwnerId, string Source, GameContent Content, string? GroupId = null);

/// <summary>
/// The shared start of every character and perk modifier. It remembers its
/// owner and its settings; each subclass overrides only the hooks it cares
/// about (see <see cref="IRuleModifier"/>).
/// </summary>
public abstract class OwnedModifier(ModifierContext context) : IRuleModifier
{
    protected ModifierContext Context { get; } = context;

    public string Source => Context.Source;

    /// <summary>The owner as a plain number (the interface's <c>OwnerId</c> is "maybe a number").</summary>
    public int Owner => Context.OwnerId;

    int? IRuleModifier.OwnerId => Context.OwnerId;

    /// <summary>The "value" setting from the content file.</summary>
    protected int Value => Context.Spec.Value;

    protected GameContent Content => Context.Content;

    /// <summary>True if this player is the owner of this modifier.</summary>
    protected bool Mine(int playerId) => playerId == Owner;
}
