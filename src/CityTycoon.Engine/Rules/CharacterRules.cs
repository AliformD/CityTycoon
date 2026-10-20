using CityTycoon.Engine.Events;
using CityTycoon.Engine.Modifiers;
using CityTycoon.Engine.State;
using CityTycoon.Engine.Steps;

namespace CityTycoon.Engine.Rules;

/// <summary>Picking and starting with a character (Sections 5 and 6).</summary>
public static class CharacterRules
{
    /// <summary>The IDs of the characters nobody has picked yet, in file order.</summary>
    public static List<string> Available(GameState state, Content.GameContent content) =>
        content.Characters
            .Where(c => state.Players.All(p => p.CharacterId != c.Id))
            .Select(c => c.Id)
            .ToList();

    /// <summary>
    /// Gives a player their character and its starting effects: the Commuter
    /// begins with 3 Momentum tokens instead of 1 (setup step 11).
    /// </summary>
    public static void Assign(EngineContext ctx, PlayerState player, string characterId)
    {
        player.CharacterId = characterId;
        ctx.Emit(new CharacterChosen(player.Id, characterId));
        player.Momentum = RuleModifiers.StartingMomentum(ctx.State, ctx.Content, player.Id);
    }

    /// <summary>
    /// Everyone has their character: the City Planner takes their first secret
    /// peek (setup step 12). The peeks are queued in turn order, so the first
    /// player's goes first.
    /// </summary>
    public static void FinishSetup(EngineContext ctx)
    {
        foreach (PlayerState player in Enumerable.Reverse(ctx.State.Players))
        {
            for (int i = 0; i < RuleModifiers.StartPeeks(ctx.State, ctx.Content, player.Id); i++)
                ctx.Push(new PeekTileStep { PlayerId = player.Id });
        }
    }
}
