using CityTycoon.Engine.Actions;
using CityTycoon.Engine.Events;
using CityTycoon.Engine.Tests.Helpers;

namespace CityTycoon.Engine.Tests.Momentum;

/// <summary>Section 7.3: Momentum tokens, Nudges and Precision Rolls.</summary>
public class MomentumTests
{
    [Fact]
    public void EveryoneStartsWithOneToken()
    {
        var game = new TestGame();
        Assert.All(game.State.Players, p => Assert.Equal(1, p.Momentum));
    }

    [Fact]
    public void PassingGo_GivesAToken()
    {
        var game = new TestGame();
        game.PlaceBefore(0, "old-town-event", 5);
        game.Roll(0, 1, 4);
        Assert.Equal(2, game.Player(0).Momentum);
        Assert.Contains(game.State.Log, e => e is MomentumGained { Source: "GO", Amount: 1 });
    }

    [Fact]
    public void TokensNeverGoPastTheLimit()
    {
        var game = new TestGame();
        game.Player(0).Momentum = 5;
        game.PlaceBefore(0, "old-town-event", 5);
        game.Roll(0, 1, 4);
        Assert.Equal(5, game.Player(0).Momentum);
        Assert.DoesNotContain(game.State.Log, e => e is MomentumGained);
    }

    [Fact]
    public void AfterRolling_YouMayNudgeUpToTwoSpacesEitherWay()
    {
        var game = new TestGame();
        game.PlaceBefore(0, "mall", 7);
        game.Dice.Add(3, 4);
        game.Do(0, new RollDice());

        var decision = game.DecisionFor(0)!;
        Assert.Equal(DecisionKind.ChooseNudge, decision.Kind);
        Assert.Equal([3, 4], decision.Dice!);
        Assert.Equal([new Nudge(-2), new Nudge(-1), new Nudge(0), new Nudge(1), new Nudge(2)], game.Engine.GetLegalActions(0));
    }

    [Fact]
    public void ANudge_CostsOneToken_AndChangesWhereYouLand()
    {
        var game = new TestGame();
        game.PlaceBefore(0, "mall", 9);
        game.Dice.Add(3, 4);
        game.Do(0, new RollDice());
        game.Do(0, new Nudge(2));

        Assert.Equal(game.State.IndexOf("mall"), game.Player(0).Position);
        Assert.Equal(0, game.Player(0).Momentum);
        Assert.Contains(game.State.Log, e => e is Nudged { Spaces: 2 });
    }

    [Fact]
    public void NotNudging_IsFree()
    {
        var game = new TestGame();
        game.PlaceBefore(0, "mall", 7);
        game.Dice.Add(3, 4);
        game.Do(0, new RollDice());
        game.Do(0, new Nudge(0));

        Assert.Equal(1, game.Player(0).Momentum);
        Assert.Equal(game.State.IndexOf("mall"), game.Player(0).Position);
    }

    [Fact]
    public void YouMustStillMoveAtLeastOneSpaceForward()
    {
        var game = new TestGame();
        game.Player(0).IsRecovering = true; // one die next turn, so a roll of 2 is possible without doubles
        game.SkipTurn(0);
        game.SkipTurn(1);
        game.Player(0).Momentum = 2;
        game.PlaceBefore(0, "mall", 2);
        game.Dice.Add(2);
        game.Do(0, new RollDice());

        Assert.Equal([new Nudge(-1), new Nudge(0), new Nudge(1), new Nudge(2)], game.Engine.GetLegalActions(0));
        Assert.False(game.Engine.Submit(0, new Nudge(-2)).Accepted);
    }

    [Fact]
    public void WithNoTokens_ThereIsNoNudgeQuestion()
    {
        var game = new TestGame();
        game.Player(0).Momentum = 0;
        game.PlaceBefore(0, "mall", 7);
        game.Dice.Add(3, 4);
        game.Do(0, new RollDice());
        Assert.Equal(DecisionKind.PostMoveActions, game.DecisionFor(0)!.Kind);
    }

    [Fact]
    public void ANudge_DoesntChangeDoubles()
    {
        var game = new TestGame();
        game.PlaceBefore(0, "mall", 5);
        game.Dice.Add(3, 3);
        game.Do(0, new RollDice());
        game.Do(0, new Nudge(-1));
        game.EndTurn(0);

        // Doubles still give another turn, even though the move was 5.
        Assert.Equal(DecisionKind.ChooseMoveMethod, game.DecisionFor(0)!.Kind);
    }

    [Fact]
    public void PrecisionRoll_NeedsTwoTokens()
    {
        var game = new TestGame();
        Assert.DoesNotContain(new PrecisionRoll(), game.Engine.GetLegalActions(0));
        game.Player(0).Momentum = 2;
        Assert.Contains(new PrecisionRoll(), game.Engine.GetLegalActions(0));
    }

    [Fact]
    public void PrecisionRoll_RollsThreeDice_AndYouKeepTwo()
    {
        var game = new TestGame();
        game.Player(0).Momentum = 3;
        game.PlaceBefore(0, "mall", 9);
        game.Dice.Add(2, 6, 3);

        game.Do(0, new PrecisionRoll());
        var decision = game.DecisionFor(0)!;
        Assert.Equal(DecisionKind.ChoosePrecisionDice, decision.Kind);
        Assert.Equal([2, 6, 3], decision.Dice!);
        Assert.Equal(1, game.Player(0).Momentum);

        game.Do(0, new ChooseDice(1, 2)); // 6 + 3 = 9
        game.Do(0, new Nudge(0));
        Assert.Equal(game.State.IndexOf("mall"), game.Player(0).Position);
    }

    [Fact]
    public void PrecisionRoll_DoublesCountOnlyIfTheKeptDiceMatch()
    {
        var game = new TestGame();
        game.Player(0).Momentum = 2;
        game.PlaceBefore(0, "mall", 8);
        game.Dice.Add(4, 4, 1);

        game.Do(0, new PrecisionRoll());
        game.Do(0, new ChooseDice(0, 1)); // 4 + 4: doubles
        game.EndTurn(0);

        Assert.Equal(DecisionKind.ChooseMoveMethod, game.DecisionFor(0)!.Kind); // another turn
    }

    [Fact]
    public void YouCantSpendTokensInJail()
    {
        var game = new TestGame();
        game.Player(1).Momentum = 3;
        game.JailThenStartTheirTurn(1);
        game.Dice.Add(2, 3);
        game.Do(1, new RollForDoubles());

        Assert.Null(game.DecisionFor(1)); // no Nudge question: still in Jail, and the turn is over
        Assert.Equal(3, game.Player(1).Momentum);
    }

    [Fact]
    public void AfterPayingBail_YouCanUseTokensAgain()
    {
        var game = new TestGame();
        game.Player(1).Momentum = 2;
        game.JailThenStartTheirTurn(1);
        game.Do(1, new PayBail());
        Assert.Contains(new PrecisionRoll(), game.Engine.GetLegalActions(1));
    }
}
