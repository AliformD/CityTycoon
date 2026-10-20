using CityTycoon.Engine.Content;
using CityTycoon.Engine.Modifiers;
using CityTycoon.Engine.State;
using CityTycoon.Engine.Steps;

namespace CityTycoon.Engine.Ordinances;

/// <summary>Everything an ordinance effect needs when it takes effect.</summary>
/// <param name="SetterId">The agenda setter. Some effects (Infrastructure Bond) ask them to break a tie.</param>
/// <param name="DistrictId">The district the setter picked, for ordinances that need one.</param>
public sealed record OrdinanceContext(EngineContext Ctx, OrdinanceDefinition Ordinance, int SetterId, string? DistrictId);

/// <summary>
/// What one kind of ordinance does (Section 11.4). Each effect type is a small class; an
/// entry in ordinances.json names the type and gives it settings, the same way cards do.
/// </summary>
public interface IOrdinanceEffect
{
    /// <summary>The name used in ordinances.json, like "Rezone".</summary>
    string Type { get; }

    /// <summary>Throws a <see cref="ContentException"/> if the JSON entry is missing a setting.</summary>
    void Validate(OrdinanceDefinition ordinance) { }

    /// <summary>
    /// False if the ordinance couldn't change anything right now, so it can't be put to a vote (Section 11.2).
    /// For a district ordinance, <see cref="Districts"/> is checked as well.
    /// </summary>
    bool CanPick(EngineContext ctx, OrdinanceDefinition ordinance) => true;

    /// <summary>The districts the agenda setter may choose from, for an ordinance that needs one.</summary>
    IReadOnlyList<string> Districts(EngineContext ctx, OrdinanceDefinition ordinance) => [];

    /// <summary>The one-time part of the ordinance, applied the moment it passes.</summary>
    void Apply(OrdinanceContext c) { }

    /// <summary>The rule change an active ordinance makes while it lasts (rent, building, Casino stakes), if any.</summary>
    IRuleModifier? Modifier(ActiveOrdinance active, OrdinanceDefinition ordinance, GameContent content) => null;
}
