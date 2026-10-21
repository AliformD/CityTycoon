using CityTycoon.Engine.Actions;
using CityTycoon.Engine.Events;
using CityTycoon.Engine.Tests.Helpers;

namespace CityTycoon.Engine.Tests.Perks;

/// <summary>Sections 13.3 and 13.4: the railroad and utility bonuses.</summary>
public class BonusTests
{
    private static readonly string[] Stations = ["old-town-station", "midtown-station", "harbor-station", "uptown-station"];
    private static readonly string[] Utilities = ["city-power-light", "city-water-works"];

    [Fact]
    public void ExpressLine_NeedsTwoRailroads()
    {
        var game = new TestGame();
        game.Give(0, Stations[0]);
        Assert.DoesNotContain(game.Engine.GetLegalActions(0), a => a is ExpressLine);
        game.Give(0, Stations[1]);
        Assert.Contains(new ExpressLine("midtown-station"), game.Engine.GetLegalActions(0));
    }

    [Fact]
    public void ExpressLine_PaysTheBank_JumpsThere_ThenRollsOneDie()
    {
        var game = new TestGame();
        game.Give(0, Stations[0], Stations[2]);
        game.Dice.Add(3); // the one die after the jump
        game.Do(0, new ExpressLine("harbor-station"));
        if (game.DecisionFor(0)?.Kind == DecisionKind.ChooseNudge)
            game.Do(0, new Nudge(0));

        Assert.Contains(game.State.Log, e => e is ExpressLineUsed { PlayerId: 0, RailroadId: "harbor-station", Cost: 50 });
        Assert.Equal(game.State.IndexOf("harbor-station") + 3, game.Player(0).Position);
        Assert.Equal(1500 - 50, game.Player(0).Cash);
    }

    [Fact]
    public void ExpressLine_IsNotOfferedWithoutTheCash()
    {
        var game = new TestGame();
        game.Give(0, Stations[0], Stations[2]);
        game.Player(0).Cash = 49;
        Assert.DoesNotContain(game.Engine.GetLegalActions(0), a => a is ExpressLine);
    }

    [Fact]
    public void ExpressLine_IsNotOfferedForAMortgagedRailroad()
    {
        var game = new TestGame();
        game.Give(0, Stations[0], Stations[2]);
        game.Property("harbor-station").IsMortgaged = true;
        Assert.DoesNotContain(game.Engine.GetLegalActions(0), a => a is ExpressLine);
    }

    [Fact]
    public void TollCollector_ThreeRailroads_GainsATokenWhenPaidRent()
    {
        var game = new TestGame();
        game.Give(1, Stations[0], Stations[1], Stations[2]);
        game.Player(1).Momentum = 0;
        int before = game.Player(1).Momentum;
        game.RollOnto(0, "harbor-station");
        Assert.Equal(before + 1, game.Player(1).Momentum);
    }

    [Fact]
    public void TollCollector_TwoRailroads_GainsNothing()
    {
        var game = new TestGame();
        game.Give(1, Stations[0], Stations[1], "harbor-station");
        game.Property("harbor-station").IsMortgaged = true; // only two count
        int before = game.Player(1).Momentum;
        game.PlaceBefore(0, "midtown-station", 4);
        game.Roll(0, 1, 3);
        Assert.Equal(before, game.Player(1).Momentum);
    }

    [Fact]
    public void RailBaron_PrecisionRollsCostOneToken()
    {
        var game = new TestGame();
        game.Player(0).Momentum = 1;
        Assert.DoesNotContain(game.Engine.GetLegalActions(0), a => a is PrecisionRoll);

        game.Give(0, Stations);
        Assert.Contains(game.Engine.GetLegalActions(0), a => a is PrecisionRoll);
        game.Dice.Add(1, 2, 3);
        game.Do(0, new PrecisionRoll());
        Assert.Equal(0, game.Player(0).Momentum);
    }

    [Fact]
    public void UtilityBill_OpponentsPassingGo_PayTheOwner()
    {
        var game = new TestGame();
        game.Give(1, Utilities);
        game.PlaceBefore(0, "go", 5);
        game.Roll(0, 1, 4);
        Assert.Contains(game.State.Log, e => e is PerkPaid { FromId: 0, ToId: 1, Amount: 20, Perk: "Utility Bill" });
        Assert.Equal(1500 + 200 - 20 + 100, game.Player(0).Cash);   // the poorer player also gets the $100 catch-up bonus
        Assert.Equal(1500 + 20, game.Player(1).Cash);
    }

    [Fact]
    public void UtilityBill_TheOwnerDoesNotPayThemselves()
    {
        var game = new TestGame();
        game.Give(0, Utilities);
        game.PlaceBefore(0, "go", 5);
        game.Roll(0, 1, 4);
        Assert.DoesNotContain(game.State.Log, e => e is PerkPaid { Perk: "Utility Bill" });
    }

    [Fact]
    public void PowerGrid_OpponentsBuilding_PayTheOwner()
    {
        var game = new TestGame();
        game.Give(1, Utilities);
        game.Give(0, "mill-street", "cobble-lane");
        int cash = game.Player(0).Cash;
        game.Do(0, new Build("mill-street"));
        Assert.Contains(game.State.Log, e => e is PerkPaid { FromId: 0, ToId: 1, Amount: 10, Perk: "Power Grid" });
        Assert.Equal(1510, game.Player(1).Cash);
        Assert.True(game.Player(0).Cash < cash - 10);
        Assert.Equal(1, game.Property("mill-street").Level);
    }

    [Fact]
    public void PowerGrid_BuildingIsBlockedIfYouCannotAffordTheFee()
    {
        var game = new TestGame();
        game.Give(1, Utilities);
        game.Give(0, "mill-street", "cobble-lane");
        var content = game.Engine.Content;
        game.Player(0).Cash = content.Property("mill-street").HouseCost + 5; // enough for the house, not the $10 fee
        Assert.DoesNotContain(game.Engine.GetLegalActions(0), a => a is Build);
    }
}
