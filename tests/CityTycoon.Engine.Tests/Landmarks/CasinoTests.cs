using CityTycoon.Engine.Actions;
using CityTycoon.Engine.Events;
using CityTycoon.Engine.Tests.Helpers;

namespace CityTycoon.Engine.Tests.Landmarks;

/// <summary>Section 15.3: the Casino. One bet, no maximum.</summary>
public class CasinoTests
{
    private static TestGame AtTheCasino(int players = 2)
    {
        var game = new TestGame(players);
        game.Player(0).Momentum = 0; // no Nudge question
        game.PlaceBefore(0, "casino", 5);
        game.Roll(0, 1, 4);
        Assert.Equal(DecisionKind.CasinoBet, game.DecisionFor(0)!.Kind);
        return game;
    }

    [Theory]
    [InlineData(true, 4, 5, 300)]    // High, rolled 9: win the bet
    [InlineData(true, 6, 6, 600)]    // High with doubles: win twice the bet
    [InlineData(true, 3, 4, -300)]   // 7 always loses
    [InlineData(false, 1, 3, 300)]   // Low, rolled 4: win
    [InlineData(false, 5, 6, -300)]  // Low, rolled 11: lose
    [InlineData(false, 2, 2, 600)]   // Low with doubles
    public void HighLow(bool high, int die1, int die2, int net)
    {
        var game = AtTheCasino();
        game.Dice.Add(die1, die2);
        game.Do(0, new BetHighLow(300, high));

        Assert.Equal(1500 + net, game.Player(0).Cash);
        Assert.Contains(game.State.Log, e => e is CasinoHighLow c && c.Net == net);
    }

    [Fact]
    public void ThereIsNoMaximum_YouCanBetEverything()
    {
        var game = AtTheCasino();
        game.Dice.Add(6, 5);
        game.Do(0, new BetHighLow(1500, High: true));
        Assert.Equal(3000, game.Player(0).Cash);
    }

    [Fact]
    public void YouCantBetMoreThanYouHave()
    {
        var game = AtTheCasino();
        Assert.False(game.Engine.Submit(0, new BetHighLow(1501, High: true)).Accepted);
        Assert.False(game.Engine.Submit(0, new BetHighLow(0, High: true)).Accepted);
    }

    [Fact]
    public void WalkingPast_ChangesNothing()
    {
        var game = AtTheCasino();
        game.Do(0, new NoThanks());
        Assert.Equal(1500, game.Player(0).Cash);
        Assert.Equal(DecisionKind.PostMoveActions, game.DecisionFor(0)!.Kind);
    }

    [Fact]
    public void DiceDuel_TheChallengedPlayerAnswers_AndTheHigherTotalTakesTheStake()
    {
        var game = AtTheCasino();
        game.Do(0, new ChallengeDuel(1, 200));
        var decision = game.DecisionFor(1)!;
        Assert.Equal(DecisionKind.RespondToDuel, decision.Kind);
        Assert.Equal(new DuelChallenge(0, 1, 200), decision.Duel);

        game.Dice.Add(3, 3, 6, 5); // 6 against 11
        game.Do(1, new AcceptDuel());

        Assert.Equal(1300, game.Player(0).Cash);
        Assert.Equal(1700, game.Player(1).Cash);
        Assert.Contains(game.State.Log, e => e is DuelWon { WinnerId: 1, Stake: 200 });
    }

    [Fact]
    public void DiceDuel_TiesRollAgain()
    {
        var game = AtTheCasino();
        game.Do(0, new ChallengeDuel(1, 100));
        game.Dice.Add(3, 4, 5, 2, 6, 6, 1, 1); // 7 v 7, then 12 v 2
        game.Do(1, new AcceptDuel());

        Assert.Equal(2, game.State.Log.OfType<DuelRolled>().Count());
        Assert.Equal(1600, game.Player(0).Cash);
    }

    [Fact]
    public void DiceDuel_TheChallengedPlayerMaySayNo()
    {
        var game = AtTheCasino();
        game.Do(0, new ChallengeDuel(1, 100));
        game.Do(1, new DeclineDuel());

        Assert.Equal(1500, game.Player(0).Cash);
        Assert.Contains(game.State.Log, e => e is DuelDeclined);
        Assert.Equal(DecisionKind.PostMoveActions, game.DecisionFor(0)!.Kind);
    }

    [Fact]
    public void DiceDuel_TheStakeIsCappedByWhoeverHasLessCash()
    {
        var game = AtTheCasino();
        game.Player(1).Cash = 80;
        Assert.False(game.Engine.Submit(0, new ChallengeDuel(1, 90)).Accepted);
        game.Do(0, new ChallengeDuel(1, 80));
    }
}
