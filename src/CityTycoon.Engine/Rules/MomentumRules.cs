using CityTycoon.Engine.Events;
using CityTycoon.Engine.Modifiers;
using CityTycoon.Engine.State;
using CityTycoon.Engine.Steps;

namespace CityTycoon.Engine.Rules;

/// <summary>Momentum tokens (Section 7.3): gaining them, the limit, and what they cost.</summary>
public static class MomentumRules
{
    /// <summary>The most tokens this player can hold: 2, plus the Commuter's and Commuter Belt's extra room.</summary>
    public static int Cap(EngineContext ctx, PlayerState player) => RuleModifiers.MomentumCap(ctx.State, ctx.Content, player.Id);

    /// <summary>What a Precision Roll costs. Owning all 4 railroads (Rail Baron) lowers it.</summary>
    public static int PrecisionRollCost(EngineContext ctx, PlayerState player) => RuleModifiers.PrecisionRollCost(ctx.State, ctx.Content, player.Id);

    /// <summary>
    /// Gives tokens, but never past the limit. Tokens over the limit are simply
    /// lost. Only the tokens actually gained are logged.
    /// </summary>
    public static void Gain(EngineContext ctx, PlayerState player, int amount, string source)
    {
        int gained = Math.Clamp(Cap(ctx, player) - player.Momentum, 0, amount);
        if (gained == 0)
            return;
        player.Momentum += gained;
        ctx.Emit(new MomentumGained(player.Id, gained, source));
    }

    /// <summary>You can't spend tokens in Jail (Section 14.2).</summary>
    public static bool CanSpend(PlayerState player, int cost) => !player.InJail && player.Momentum >= cost;

    /// <summary>
    /// The Nudges allowed after rolling this total: up to 2 spaces either way,
    /// but you must still move at least 1 space forward. 0 (no Nudge) is always allowed.
    /// </summary>
    public static IEnumerable<int> NudgeOptions(EngineContext ctx, PlayerState player, int rolled)
    {
        int max = CanSpend(player, ctx.Rules.NudgeCost) ? ctx.Rules.NudgeMaxSpaces : 0;
        for (int spaces = -max; spaces <= max; spaces++)
        {
            if (rolled + spaces >= 1)
                yield return spaces;
        }
    }
}
