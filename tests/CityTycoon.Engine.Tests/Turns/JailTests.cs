using CityTycoon.Engine.Actions;
using CityTycoon.Engine.Tests.Helpers;

namespace CityTycoon.Engine.Tests.Turns;

/// <summary>Section 14: Jail (the Phase 1 basics).</summary>
public class JailTests
{
    [Fact]
    public void JailedPlayer_IsAskedToPayBailOrRollForDoubles()
    {
        var game = new TestGame();
        game.JailThenStartTheirTurn(1);
        Assert.Equal(DecisionKind.JailChoice, game.DecisionFor(1)!.Kind);
        Assert.Equal([new PayBail(), new RollForDoubles()], game.Engine.GetLegalActions(1));
    }

    [Fact]
    public void PayBail_CostsBail_ThenYouRollNormally()
    {
        var game = new TestGame();
        game.JailThenStartTheirTurn(1);
        game.Do(1, new PayBail());

        Assert.Equal(1450, game.Player(1).Cash);
        Assert.False(game.Player(1).InJail);
        Assert.Equal(DecisionKind.ChooseMoveMethod, game.DecisionFor(1)!.Kind);
    }

    [Fact]
    public void PayBail_ThenDoubles_GivesNoExtraTurn()
    {
        var game = new TestGame();
        game.JailThenStartTheirTurn(1);
        game.Do(1, new PayBail());
        game.Roll(1, 2, 2);
        game.EveryonePasses();
        game.EndTurn(1);
        Assert.Equal(DecisionKind.ChooseMoveMethod, game.DecisionFor(0)!.Kind);
    }

    [Fact]
    public void RollingDoubles_GetsYouOut_AndMoves_ButGivesNoExtraTurn()
    {
        var game = new TestGame();
        game.JailThenStartTheirTurn(1);
        game.Dice.Add(3, 3);
        game.Do(1, new RollForDoubles());

        Assert.False(game.Player(1).InJail);
        Assert.Equal(game.State.IndexOf("jail") + 6, game.Player(1).Position);
        game.EveryonePasses();
        game.EndTurn(1);
        Assert.Equal(DecisionKind.ChooseMoveMethod, game.DecisionFor(0)!.Kind);
    }

    [Fact]
    public void FailingOnTheFirstJailTurn_KeepsYouInJail()
    {
        var game = new TestGame();
        game.JailThenStartTheirTurn(1);
        game.Dice.Add(1, 2);
        game.Do(1, new RollForDoubles());

        Assert.True(game.Player(1).InJail);
        Assert.Equal(1500, game.Player(1).Cash);
        Assert.Equal(DecisionKind.ChooseMoveMethod, game.DecisionFor(0)!.Kind);
    }

    [Fact]
    public void FailingOnTheSecondJailTurn_MeansPayingBail_AndMovingByThatRoll()
    {
        var game = new TestGame();
        game.JailThenStartTheirTurn(1);
        game.Dice.Add(1, 2);
        game.Do(1, new RollForDoubles());
        game.SkipTurn(0);

        game.Dice.Add(1, 3);
        game.Do(1, new RollForDoubles());

        Assert.False(game.Player(1).InJail);
        Assert.Equal(1450, game.Player(1).Cash);
        Assert.Equal(game.State.IndexOf("jail") + 4, game.Player(1).Position);
    }

    [Fact]
    public void YouCantChooseBail_WithoutTheCash()
    {
        var game = new TestGame();
        game.Player(1).Cash = 30;
        game.JailThenStartTheirTurn(1);
        Assert.Equal([new RollForDoubles()], game.Engine.GetLegalActions(1));
    }

    [Fact]
    public void PlayersInJail_CanStillBidInAuctions()
    {
        var game = new TestGame();
        game.Player(1).InJail = true;
        game.RollOnto(0, "market-street");
        Assert.Equal(DecisionKind.SubmitBid, game.DecisionFor(1)!.Kind);
    }
}
