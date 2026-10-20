using CityTycoon.Engine.Tests.Helpers;

namespace CityTycoon.Engine.Tests.RentAndMoney;

/// <summary>Section 14.2: frozen assets. An owner in Jail collects half rent.</summary>
public class JailRentTests
{
    [Fact]
    public void AnOwnerInJail_CollectsHalfRent()
    {
        var game = new TestGame();
        game.Give(1, "market-street", "baker-street", "fountain-square");
        game.Property("market-street").Level = 2; // $200
        game.Player(1).InJail = true;

        game.RollOnto(0, "market-street");
        Assert.Equal(1500 - 100, game.Player(0).Cash);
    }

    [Fact]
    public void HalfRent_RoundsDown_ButIsNeverBelowOneDollar()
    {
        var game = new TestGame();
        game.Give(1, "mill-street"); // $2 base rent
        game.Player(1).InJail = true;
        game.PlaceBefore(0, "mill-street", 5);
        game.Roll(0, 1, 4); // passes GO on the way
        Assert.Equal(1500 + 200 - 1, game.Player(0).Cash);
    }

    [Fact]
    public void Stadium_AndJail_Together()
    {
        var game = new TestGame();
        game.Give(1, "market-street"); // $14
        game.State.StadiumBoosts.Add(new State.StadiumBoost("orange", 1));
        game.Player(1).InJail = true;
        game.RollOnto(0, "market-street");
        Assert.Equal(1500 - 14, game.Player(0).Cash); // ×2, then ×0.5
    }
}
