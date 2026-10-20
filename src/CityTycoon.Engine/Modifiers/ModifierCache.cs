using CityTycoon.Engine.Content;

namespace CityTycoon.Engine.Modifiers;

/// <summary>The active modifiers, kept with the <see cref="Signature"/> of the game they were gathered for.</summary>
internal sealed record ModifierCache(long Signature, GameContent Content, IReadOnlyList<IRuleModifier> Modifiers);
