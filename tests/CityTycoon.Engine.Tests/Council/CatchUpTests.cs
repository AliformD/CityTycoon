using CityTycoon.Engine.Events;
using CityTycoon.Engine.Tests.Helpers;

namespace CityTycoon.Engine.Tests.Council;

/// <summary>Section 16.2 and 15.1 step 4: the lowest net worth gets $100 for passing GO.</summary>
public class CatchUpTests
{
    [Fact]
    public void TheLowestNetWorth_GetsTheBonus()
    {
        var game = new TestGame(3);
        game.Player(1).Cash = 2000;
        game.Player(2).Cash = 2000;
        game.PlaceBefore(0, "go", 5);
        game.Roll(0, 1, 4);
        Assert.Equal(1500 + 200 + 100, game.Player(0).Cash);
        Assert.Contains(game.State.Log, e => e is CatchUpPaid { PlayerId: 0, Amount: 100 });
    }

    [Fact]
    public void SomeoneElseBeingPoorer_MeansNoBonus()
    {
        var game = new TestGame(3);
        game.Player(1).Cash = 100;
        game.PlaceBefore(0, "go", 5);
        game.Roll(0, 1, 4);
        Assert.Equal(1500 + 200, game.Player(0).Cash);
        Assert.DoesNotContain(game.State.Log, e => e is CatchUpPaid);
    }

    [Fact]
    public void ATie_ForLowest_AllCount()
    {
        var game = new TestGame(2);
        game.Player(1).Cash = 1700;   // exactly what player 0 holds after the salary
        game.PlaceBefore(0, "go", 5);
        game.Roll(0, 1, 4);
        Assert.Contains(game.State.Log, e => e is CatchUpPaid { PlayerId: 0 });
    }
}

/// <summary>Section 16.3: net worth, including $50 per Opportunity card in hand, and its breakdown.</summary>
public class NetWorthBreakdownTests
{
    [Fact]
    public void EachCardInHand_AddsFifty_AndTheBreakdownAddsUp()
    {
        var game = new TestGame();
        game.Give(0, "mill-street");
        game.Property("mill-street").Level = 2;
        game.GiveCard(0, "lobbyist", "lobbyist");
        game.Player(0).Loans = 1;

        var parts = Engine.Rules.NetWorth.Explain(game.State, Engine.Content.GameContent.Default, 0);
        Assert.Equal(1500, parts.Cash);
        Assert.Equal(100, parts.Cards);
        Assert.Equal(100, parts.Buildings);   // two levels at $50
        Assert.Equal(250, parts.Loans);
        Assert.Equal(parts.Total, Engine.Rules.NetWorth.Calculate(game.State, Engine.Content.GameContent.Default, 0));
        Assert.Equal(parts.Total, game.Engine.GetView(0).Player(0).NetWorth);
    }
}
