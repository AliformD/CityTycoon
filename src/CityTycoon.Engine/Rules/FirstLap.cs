using CityTycoon.Engine.State;
using CityTycoon.Engine.Steps;

namespace CityTycoon.Engine.Rules;

/// <summary>The first-lap house rules (fixed-price buying, no Momentum), being tried out in the Sim.</summary>
public static class FirstLap
{
    /// <summary>Is this player still on their first lap, by the <c>firstLapEnds</c> rule?</summary>
    public static bool IsOn(EngineContext ctx, PlayerState player) => ctx.Rules.FirstLapEnds switch
    {
        "first" => ctx.State.Players.All(p => p.GoPasses == 0),
        "last" => ctx.State.ActivePlayers.Any(p => p.GoPasses == 0),
        _ => player.GoPasses == 0,
    };

    /// <summary>True when Momentum and roll changes are switched off for this player right now.</summary>
    public static bool NoMomentum(EngineContext ctx, PlayerState player) =>
        ctx.Rules.FirstLapNoMomentum && IsOn(ctx, player);
}
