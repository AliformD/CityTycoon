using CityTycoon.Engine.Actions;
using CityTycoon.Engine.Events;
using CityTycoon.Engine.Tests.Helpers;

namespace CityTycoon.Engine.Tests.Turns;

/// <summary>Section 7: turns, doubles, and movement. Section 15.1: GO.</summary>
public class MovementTests
{
    [Fact]
    public void FirstDecision_IsThe1stPlayerChoosingHowToMove()
    {
        var game = new TestGame();
        PendingDecision d = Assert.Single(game.Engine.GetPendingDecisions());
        Assert.Equal(0, d.PlayerId);
        Assert.Equal(DecisionKind.ChooseMoveMethod, d.Kind);
    }

    [Fact]
    public void Roll_MovesThePlayerByTheDiceTotal()
    {
        var game = new TestGame();
        game.PlaceBefore(0, "jail", 7);
        game.Roll(0, 3, 4);
        Assert.Equal(game.State.IndexOf("jail"), game.Player(0).Position);
        Assert.Equal(DecisionKind.PostMoveActions, game.DecisionFor(0)!.Kind);
    }

    [Fact]
    public void PassingGo_CollectsSalary()
    {
        var game = new TestGame();
        game.PlaceBefore(0, "go", 2);
        var result = game.Roll(0, 3, 4);
        game.EveryonePasses(); // in case it landed on a property
        Assert.Contains(result.Events, e => e is PassedGo);
        Assert.Equal(1500 + 200, game.Player(0).Cash);
    }

    [Fact]
    public void LandingOnGo_CollectsSalary()
    {
        var game = new TestGame();
        game.PlaceBefore(0, "go", 7);
        game.Roll(0, 3, 4);
        Assert.Equal(0, game.Player(0).Position);
        Assert.Equal(1700, game.Player(0).Cash);
    }

    [Fact]
    public void Doubles_GiveAnotherTurn()
    {
        var game = new TestGame();
        game.PlaceBefore(0, "jail", 4);
        game.Roll(0, 2, 2);
        game.EndTurn(0);
        Assert.Equal(DecisionKind.ChooseMoveMethod, game.DecisionFor(0)!.Kind);
    }

    [Fact]
    public void NoDoubles_PassesTheTurnToTheNextPlayer()
    {
        var game = new TestGame();
        game.SkipTurn(0);
        Assert.Equal(DecisionKind.ChooseMoveMethod, game.DecisionFor(1)!.Kind);
        Assert.Null(game.DecisionFor(0));
    }

    [Fact]
    public void ThreeDoublesInARow_SendsYouToJail_InsteadOfMoving()
    {
        var game = new TestGame();
        game.Player(0).Position = 0;
        game.Roll(0, 1, 1); game.EveryonePasses(); game.EndTurn(0);
        game.Roll(0, 2, 2); game.EveryonePasses(); game.EndTurn(0);
        int before = game.Player(0).Position;
        var result = game.Roll(0, 3, 3);

        Assert.DoesNotContain(result.Events, e => e is PlayerMoved);
        Assert.True(game.Player(0).InJail);
        Assert.Equal(game.State.IndexOf("jail"), game.Player(0).Position);
        Assert.NotEqual(before, game.Player(0).Position);
        Assert.Equal(DecisionKind.ChooseMoveMethod, game.DecisionFor(1)!.Kind); // turn over
    }

    [Fact]
    public void GoToJailSpace_SendsYouToJail_WithoutCollectingGo()
    {
        var game = new TestGame();
        game.RollOnto(0, "go-to-jail");
        Assert.True(game.Player(0).InJail);
        Assert.Equal(game.State.IndexOf("jail"), game.Player(0).Position);
        Assert.Equal(1500, game.Player(0).Cash);
    }

    [Fact]
    public void GoingToJailOnDoubles_EndsTheTurn_WithNoExtraTurn()
    {
        var game = new TestGame();
        game.PlaceBefore(0, "go-to-jail", 4);
        game.Roll(0, 2, 2);
        Assert.Equal(DecisionKind.ChooseMoveMethod, game.DecisionFor(1)!.Kind);
    }

    [Fact]
    public void LandingOnJail_IsJustVisiting()
    {
        var game = new TestGame();
        game.RollOnto(0, "jail");
        Assert.False(game.Player(0).InJail);
    }

    [Fact]
    public void Submit_RejectsAnActionFromAPlayerWhoIsntBeingAsked()
    {
        var game = new TestGame();
        Assert.False(game.Engine.Submit(1, new RollDice()).Accepted);
    }

    [Fact]
    public void Submit_RejectsAnActionThatDoesntFitTheDecision()
    {
        var game = new TestGame();
        Assert.False(game.Engine.Submit(0, new EndTurn()).Accepted);
    }
}
