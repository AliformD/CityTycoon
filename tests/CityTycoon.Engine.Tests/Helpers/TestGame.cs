using CityTycoon.Engine.Actions;
using CityTycoon.Engine.Randomness;
using CityTycoon.Engine.State;

namespace CityTycoon.Engine.Tests.Helpers;

/// <summary>
/// Sets up a game for a test and gives short helper methods, so each test can
/// focus on the one rule it checks. Turn order is NOT shuffled: player 0 goes
/// first, then 1, and so on. The dice are scripted (Section 26.2).
/// </summary>
public sealed class TestGame
{
    public GameEngine Engine { get; }
    public ScriptedDice Dice { get; } = new();
    public GameState State => Engine.State;

    public TestGame(int players = 2, GameMode mode = GameMode.Timed, int maxRounds = 30, ulong seed = 12345)
    {
        var setup = new GameSetup
        {
            Players = Enumerable.Range(0, players).Select(i => new PlayerSetup(((char)('A' + i)).ToString())).ToList(),
            Mode = mode,
            MaxRounds = maxRounds,
            Seed = seed,
            ShuffleTurnOrder = false,
        };
        Engine = GameEngine.NewGame(setup, dice: Dice);
    }

    public PlayerState Player(int id) => State.Player(id);

    public PropertyState Property(string id) => State.Properties[id];

    /// <summary>Hands properties straight to a player, skipping the auction.</summary>
    public void Give(int playerId, params string[] propertyIds)
    {
        foreach (string id in propertyIds)
            State.Properties[id].OwnerId = playerId;
    }

    /// <summary>Puts a player a set number of spaces before a space, so a roll of that number lands on it.</summary>
    public void PlaceBefore(int playerId, string spaceId, int spaces)
    {
        int n = State.Board.Count;
        Player(playerId).Position = (State.IndexOf(spaceId) - spaces + n) % n;
    }

    /// <summary>Submits an action and fails the test if the engine rejects it.</summary>
    public ActionResult Do(int playerId, GameAction action)
    {
        ActionResult result = Engine.Submit(playerId, action);
        Assert.True(result.Accepted, result.Error);
        return result;
    }

    /// <summary>Scripts the next two dice, then rolls them.</summary>
    public ActionResult Roll(int playerId, int die1, int die2)
    {
        Dice.Add(die1, die2);
        return Do(playerId, new RollDice());
    }

    /// <summary>
    /// Moves a player onto a space with a small roll that isn't doubles (1+4,
    /// or less for spaces near the start), without passing GO on the way.
    /// </summary>
    public ActionResult RollOnto(int playerId, string spaceId)
    {
        int distance = Math.Min(5, State.IndexOf(spaceId) - 1);
        Assert.True(distance >= 3, $"{spaceId} is too close to GO for RollOnto.");
        PlaceBefore(playerId, spaceId, distance);
        return Roll(playerId, 1, distance - 1);
    }

    public void EndTurn(int playerId) => Do(playerId, new EndTurn());

    /// <summary>Every player still asked passes in the current auction.</summary>
    public void EveryonePasses()
    {
        foreach (var d in Engine.GetPendingDecisions().Where(d => d.Kind == DecisionKind.SubmitBid).ToList())
            Do(d.PlayerId, new PassBid());
    }

    public PendingDecision? DecisionFor(int playerId) =>
        Engine.GetPendingDecisions().FirstOrDefault(d => d.PlayerId == playerId);
}

public static class TestGameExtensions
{
    /// <summary>Plays a quiet turn: land on the Casino (does nothing yet) and end the turn.</summary>
    public static void SkipTurn(this TestGame game, int playerId)
    {
        game.PlaceBefore(playerId, "casino", 5);
        game.Roll(playerId, 2, 3);
        game.EndTurn(playerId);
    }

    /// <summary>Puts a player in Jail, then lets player 0 finish a quiet turn so the jailed player is up next.</summary>
    public static void JailThenStartTheirTurn(this TestGame game, int playerId)
    {
        var player = game.Player(playerId);
        player.InJail = true;
        player.Position = game.State.IndexOf("jail");
        for (int id = 0; id < playerId; id++)
            game.SkipTurn(id);
    }
}
