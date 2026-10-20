using CityTycoon.Engine.Content;
using CityTycoon.Engine.Events;
using CityTycoon.Engine.State;
using CityTycoon.Engine.Steps;

namespace CityTycoon.Engine.Ordinances;

/// <summary>Keeping track of passed ordinances: which are in force, which districts are frozen, and counting rounds down.</summary>
public static class OrdinanceRules
{
    /// <summary>True if Historic Preservation has locked this district's zoning (Section 11.4).</summary>
    public static bool IsFrozen(GameState state, GameContent content, string districtId) =>
        state.ActiveOrdinances.Any(a => a.DistrictId == districtId && content.Ordinance(a.OrdinanceId).EffectType == "Preserve");

    /// <summary>The districts whose zoning can still change, in board order.</summary>
    public static IEnumerable<string> ChangeableDistricts(GameState state, GameContent content) =>
        content.Board.Districts.Select(d => d.Id).Where(id => !IsFrozen(state, content, id));

    /// <summary>Moves a district's zoning up or down one level and logs it. Returns false if it can't go that way.</summary>
    public static bool ChangeZoning(EngineContext ctx, string districtId, int direction, string ordinanceId)
    {
        ZoningLevel from = ctx.State.Zoning[districtId];
        int to = (int)from + direction;
        if (to < 0 || to > (int)ZoningLevel.HighRise || IsFrozen(ctx.State, ctx.Content, districtId))
            return false;
        ctx.State.Zoning[districtId] = (ZoningLevel)to;
        ctx.Emit(new ZoningChanged(districtId, from, (ZoningLevel)to, ordinanceId));
        return true;
    }

    /// <summary>
    /// Puts a passed ordinance on the table. Passing the same one again (same district) just
    /// refreshes its timer instead of stacking a second copy.
    /// </summary>
    public static void Activate(GameState state, OrdinanceDefinition def, string? districtId)
    {
        int? rounds = def.Duration;
        ActiveOrdinance fresh = new(def.Id, districtId, rounds);
        int existing = state.ActiveOrdinances.FindIndex(a => a.OrdinanceId == def.Id && a.DistrictId == districtId);
        if (existing >= 0)
            state.ActiveOrdinances[existing] = fresh;
        else
            state.ActiveOrdinances.Add(fresh);
    }

    /// <summary>
    /// End of round: every timed ordinance has one fewer round left, and the ones that hit zero end
    /// (Section 11.3, step 7). A Regular session happens after this, so an ordinance that
    /// passes at the end of round 4 with a duration of 3 covers rounds 5, 6 and 7.
    /// </summary>
    public static void EndOfRound(EngineContext ctx)
    {
        var remaining = new List<ActiveOrdinance>();
        foreach (ActiveOrdinance active in ctx.State.ActiveOrdinances)
        {
            if (active.RoundsLeft is not int left)
            {
                remaining.Add(active);
            }
            else if (left > 1)
            {
                remaining.Add(active with { RoundsLeft = left - 1 });
            }
            else
            {
                ctx.Emit(new OrdinanceEnded(active.OrdinanceId, active.DistrictId));
            }
        }
        ctx.State.ActiveOrdinances = remaining;
    }
}
