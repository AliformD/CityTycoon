using CityTycoon.Engine.Content;
using CityTycoon.Engine.Events;
using CityTycoon.Engine.Rules;
using CityTycoon.Engine.State;
using CityTycoon.Engine.Tests.Helpers;

namespace CityTycoon.Engine.Tests.GameFlow;

/// <summary>Section 16: game modes, net worth, and the winner.</summary>
public class GameEndTests
{
    [Fact]
    public void ARound_EndsWhenEveryPlayerHasHadATurn()
    {
        var game = new TestGame(players: 3);
        game.SkipTurn(0);
        game.SkipTurn(1);
        Assert.Equal(1, game.State.Round);
        game.SkipTurn(2);
        Assert.Equal(2, game.State.Round);
        Assert.Contains(game.State.Log, e => e is RoundEnded { Round: 1 });
    }

    [Fact]
    public void TimedMode_EndsAfterTheLastRound()
    {
        var game = new TestGame(players: 2, maxRounds: 2);
        game.SkipTurn(0); game.SkipTurn(1);
        Assert.False(game.State.IsOver);
        game.SkipTurn(0); game.SkipTurn(1);
        Assert.True(game.State.IsOver);
        Assert.Empty(game.Engine.GetPendingDecisions());
    }

    [Fact]
    public void ClassicMode_EndsWhenOnlyOnePlayerIsLeft()
    {
        var game = new TestGame(players: 2, mode: GameMode.Classic);
        game.Give(1, "platinum-row");
        game.Player(0).Cash = 10;
        game.RollOnto(0, "platinum-row");

        Assert.True(game.State.IsOver);
        Assert.Equal([1], game.State.Result!.WinnerIds);
    }

    [Fact]
    public void Winner_HasTheHighestNetWorth()
    {
        var game = new TestGame(players: 2, maxRounds: 1);
        game.Give(1, "platinum-row"); // +$400 net worth
        game.SkipTurn(0);
        game.SkipTurn(1);
        Assert.Equal([1], game.State.Result!.WinnerIds);
    }

    [Fact]
    public void NetWorthTie_GoesToTheMostCash()
    {
        var game = new TestGame(players: 2, maxRounds: 1);
        game.Give(0, "skyline-drive"); // $350
        game.Player(0).Cash = 1150;    // 1150 + 350 = 1500
        game.SkipTurn(0);
        game.SkipTurn(1);              // player 1: $1500 cash
        Assert.Equal([1], game.State.Result!.WinnerIds);
    }

    [Fact]
    public void AFullTie_IsAShareWin()
    {
        var game = new TestGame(players: 2, maxRounds: 1);
        game.SkipTurn(0);
        game.SkipTurn(1);
        Assert.Equal([0, 1], game.State.Result!.WinnerIds.Order());
    }

    [Fact]
    public void NetWorth_IsCashPlusAssessedValue_HalfIfMortgaged()
    {
        var game = new TestGame();
        game.Give(0, "platinum-row", "mill-street");
        game.Property("mill-street").IsMortgaged = true;
        Assert.Equal(1500 + 400 + 30, NetWorth.Calculate(game.State, GameContent.Default, 0));
    }
}
