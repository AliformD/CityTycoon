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

    public TestGame(int players = 2, GameMode mode = GameMode.Timed, int maxRounds = 30, ulong seed = 12345, string[]? characters = null)
    {
        var setup = new GameSetup
        {
            Players = Enumerable.Range(0, players).Select(i => new PlayerSetup(((char)('A' + i)).ToString())).ToList(),
            Mode = mode,
            MaxRounds = maxRounds,
            Seed = seed,
            ShuffleTurnOrder = false,
        };
        if (characters != null)
            setup.Players = setup.Players.Select((p, i) => i < characters.Length ? p with { CharacterId = characters[i] } : p).ToList();
        Engine = GameEngine.NewGame(setup, dice: Dice);
    }

    /// <summary>Gives a player a character straight away (the pick step has its own tests).</summary>
    public void SetCharacter(int playerId, string characterId) => Player(playerId).CharacterId = characterId;

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

    /// <summary>
    /// Makes the Old Town Government tile a particular tile. With the test
    /// seed it sits between Maple Avenue and Birch Avenue (both Light Blue).
    /// </summary>
    public BoardSpace SetTile(string tileId, bool revealed = true, string spaceId = "old-town-government")
    {
        BoardSpace space = State.Space(spaceId);
        space.TileId = tileId;
        space.IsRevealed = revealed;
        return space;
    }

    /// <summary>Puts Opportunity cards straight into a player's hand.</summary>
    public void GiveCard(int playerId, params string[] cardIds) => Player(playerId).Hand.AddRange(cardIds);

    /// <summary>Puts these Event cards on top of the Event deck, first one on top.</summary>
    public void StackEvents(params string[] cardIds) => State.EventDeck.InsertRange(0, cardIds);

    /// <summary>Puts these Opportunity cards on top of the Opportunity deck, first one on top.</summary>
    public void StackOpportunities(params string[] cardIds) => State.OpportunityDeck.InsertRange(0, cardIds);

    /// <summary>Lands a player on an Event space with this card on top of the deck.</summary>
    public ActionResult DrawEvent(int playerId, string cardId)
    {
        StackEvents(cardId);
        return RollOnto(playerId, "midtown-event");
    }

    /// <summary>Submits an action and fails the test if the engine rejects it.</summary>
    public ActionResult Do(int playerId, GameAction action)
    {
        ActionResult result = Engine.Submit(playerId, action);
        Assert.True(result.Accepted, result.Error);
        return result;
    }

    /// <summary>
    /// Scripts the next two dice, then rolls them. If the player could Nudge,
    /// they choose not to, so the move is exactly the dice total.
    /// </summary>
    public ActionResult Roll(int playerId, int die1, int die2)
    {
        Dice.Add(die1, die2);
        ActionResult result = Do(playerId, new RollDice());
        if (DecisionFor(playerId)?.Kind == DecisionKind.ChooseNudge)
            result = Do(playerId, new Nudge(0));
        return result;
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

    /// <summary>Every player still asked passes in the current auction (sealed or open) until it is over.</summary>
    public void EveryonePasses()
    {
        for (int guard = 0; guard < 100; guard++)
        {
            var asked = Engine.GetPendingDecisions().Where(d => d.Kind == DecisionKind.SubmitBid).ToList();
            if (asked.Count == 0)
                return;
            foreach (var d in asked)
                Do(d.PlayerId, new PassBid());
        }
        throw new InvalidOperationException("The auction never ended.");
    }

    public PendingDecision? DecisionFor(int playerId) =>
        Engine.GetPendingDecisions().FirstOrDefault(d => d.PlayerId == playerId);

    /// <summary>The legal actions that answer the decision itself, without the "manage your money" extras.</summary>
    public List<GameAction> MainActions(int playerId) =>
        Engine.GetLegalActions(playerId).Where(a => a is not ManagementAction).ToList();
}

public static class TestGameExtensions
{
    /// <summary>Plays a quiet turn: land on Jail (just visiting, so nothing happens) and end the turn.</summary>
    public static void SkipTurn(this TestGame game, int playerId)
    {
        game.PlaceBefore(playerId, "jail", 5);
        game.Roll(playerId, 2, 3);
        game.EndTurn(playerId);
    }

    /// <summary>Rolls onto Jail (just visiting) and stops at the post-move actions, without ending the turn.</summary>
    public static void SkipTurnWithoutEnding(this TestGame game, int playerId)
    {
        game.PlaceBefore(playerId, "jail", 5);
        game.Roll(playerId, 2, 3);
    }

    /// <summary>Same as <see cref="SkipTurn"/>, for a turn that has already started.</summary>
    public static void SkipTurnFromHere(this TestGame game, int playerId) => game.SkipTurn(playerId);

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
