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

    /// <summary>The furthest this player can Nudge: 2 spaces, or 4 for the Commuter (decision #151).</summary>
    public static int NudgeMaxSpaces(EngineContext ctx, PlayerState player) => RuleModifiers.NudgeMaxSpaces(ctx.State, ctx.Content, player.Id);

    /// <summary>What a Nudge of this many spaces costs: 1 token for each space, either way (decision #151).</summary>
    public static int NudgeCost(EngineContext ctx, int spaces) => Math.Abs(spaces) * ctx.Rules.NudgeCost;

    /// <summary>
    /// The Nudges allowed after rolling this total. Each space costs a token, so 1 token moves you
    /// 1 space either way and 2 tokens 2 spaces, up to the player's limit (2, the Commuter 4).
    /// You must still move at least 1 space forward. 0 (no Nudge) is always allowed.
    /// </summary>
    public static IEnumerable<int> NudgeOptions(EngineContext ctx, PlayerState player, int rolled)
    {
        int affordable = player.InJail || ctx.Rules.NudgeCost <= 0 ? 0 : player.Momentum / ctx.Rules.NudgeCost;
        int max = Math.Min(NudgeMaxSpaces(ctx, player), affordable);
        for (int spaces = -max; spaces <= max; spaces++)
        {
            if (rolled + spaces >= 1)
                yield return spaces;
        }
    }
}
