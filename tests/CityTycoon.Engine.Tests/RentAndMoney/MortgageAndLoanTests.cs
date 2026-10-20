using CityTycoon.Engine.Actions;
using CityTycoon.Engine.Events;
using CityTycoon.Engine.Rules;
using CityTycoon.Engine.Tests.Helpers;

namespace CityTycoon.Engine.Tests.RentAndMoney;

/// <summary>Section 15.7 (loans) and 15.8 (mortgages).</summary>
public class MortgageAndLoanTests
{
    [Fact]
    public void Mortgaging_GivesHalfTheAssessedValue()
    {
        var game = new TestGame();
        game.Give(0, "market-street"); // assessed $180
        game.Do(0, new Mortgage("market-street"));

        Assert.True(game.Property("market-street").IsMortgaged);
        Assert.Equal(1500 + 90, game.Player(0).Cash);
    }

    [Fact]
    public void Unmortgaging_CostsTheMortgageValuePlusTenPercent_RoundedUp()
    {
        var game = new TestGame();
        game.Give(0, "mill-street"); // mortgage value $30, so $33 to unmortgage
        game.Property("mill-street").IsMortgaged = true;
        game.Do(0, new Unmortgage("mill-street"));

        Assert.False(game.Property("mill-street").IsMortgaged);
        Assert.Equal(1500 - 33, game.Player(0).Cash);
        Assert.Contains(game.State.Log, e => e is PropertyUnmortgaged { Cost: 33 });
    }

    [Fact]
    public void YouCantMortgage_WhileTheGroupHasBuildings()
    {
        var game = new TestGame();
        game.Give(0, "mill-street", "cobble-lane");
        game.Property("cobble-lane").Level = 1;
        Assert.Contains("Sell the buildings", game.Engine.WhyIllegal(0, new Mortgage("mill-street")));
    }

    [Fact]
    public void MortgagedProperties_CollectNoRent()
    {
        var game = new TestGame();
        game.Give(1, "market-street");
        game.Property("market-street").IsMortgaged = true;
        game.RollOnto(0, "market-street");
        Assert.Equal(1500, game.Player(0).Cash);
    }

    [Fact]
    public void ALoan_Gives200_AndYouCanHoldTwo()
    {
        var game = new TestGame();
        game.Do(0, new TakeLoan());
        game.Do(0, new TakeLoan());

        Assert.Equal(1900, game.Player(0).Cash);
        Assert.Equal(2, game.Player(0).Loans);
        Assert.DoesNotContain(new TakeLoan(), game.Engine.GetLegalActions(0));
    }

    [Fact]
    public void RepayingALoan_Costs250()
    {
        var game = new TestGame();
        game.Do(0, new TakeLoan());
        game.Do(0, new RepayLoan());

        Assert.Equal(1500 + 200 - 250, game.Player(0).Cash);
        Assert.Equal(0, game.Player(0).Loans);
    }

    [Fact]
    public void YouCanOnlyRepay_WhenYouHaveALoanAndTheCash()
    {
        var game = new TestGame();
        Assert.DoesNotContain(new RepayLoan(), game.Engine.GetLegalActions(0));
        game.Player(0).Loans = 1;
        game.Player(0).Cash = 100;
        Assert.DoesNotContain(new RepayLoan(), game.Engine.GetLegalActions(0));
    }

    [Fact]
    public void NetWorth_CountsEachLoanAsMinus250_AndMortgagedPropertiesAtHalf()
    {
        var game = new TestGame();
        game.Give(0, "market-street");
        game.Do(0, new Mortgage("market-street"));
        game.Do(0, new TakeLoan());

        // Cash 1500 + 90 + 200, property 180 - 90, loan -250.
        Assert.Equal(1790 + 90 - 250, NetWorth.Calculate(game.State, game.Engine.Content, 0));
    }
}
