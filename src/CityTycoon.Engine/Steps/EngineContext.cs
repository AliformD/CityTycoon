using CityTycoon.Engine.Content;
using CityTycoon.Engine.Events;
using CityTycoon.Engine.Randomness;
using CityTycoon.Engine.State;

namespace CityTycoon.Engine.Steps;

/// <summary>
/// The toolbox every step is handed: the game state, the content, the dice,
/// and ways to record events and push new steps.
/// </summary>
public sealed class EngineContext(GameState state, GameContent content, IDiceSource dice)
{
    public GameState State { get; } = state;
    public GameContent Content { get; } = content;
    public RulesConfig Rules => Content.Rules;
    public IDiceSource Dice { get; } = dice;

    /// <summary>Records something that happened in the game log.</summary>
    public void Emit(GameEvent gameEvent) => State.Log.Add(gameEvent);

    /// <summary>Puts a new step on top of the stack, so it runs next.</summary>
    public void Push(Step step) => State.Stack.Add(step);

    public PlayerState Player(int playerId) => State.Player(playerId);
}
