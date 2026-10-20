using CityTycoon.Engine.Actions;
using CityTycoon.Engine.Events;
using CityTycoon.Engine.Tests.Helpers;

namespace CityTycoon.Engine.Tests.Cards;

/// <summary>Section 15.5: every Event card. Each test puts the card on top of the deck and lands on an Event space.</summary>
public class EventCardTests
{
    [Fact]
    public void EventCards_ArePublic_AndGoToTheDiscardPile()
    {
        var game = new TestGame();
        game.DrawEvent(0, "tax-refund");
        Assert.Contains(game.State.Log, e => e is EventDrawn { PlayerId: 0, CardId: "tax-refund" });
        Assert.Contains("tax-refund", game.State.EventDiscard);
    }

    [Fact]
    public void SprainedAnkle_SendsYouToTheHospital()
    {
        var game = new TestGame();
        game.DrawEvent(0, "sprained-ankle");
        Assert.Equal(game.State.IndexOf("hospital"), game.Player(0).Position);
        Assert.Equal(1400, game.Player(0).Cash);
        Assert.True(game.Player(0).IsRecovering);
    }

    [Fact]
    public void Fire_KnocksALevelOffYourTallestProperty()
    {
        var game = new TestGame();
        game.Give(0, "mill-street", "cobble-lane");
        game.Property("mill-street").Level = 1;
        game.Property("cobble-lane").Level = 2;
        game.DrawEvent(0, "fire");
        Assert.Equal(1, game.Property("cobble-lane").Level);
        Assert.Equal(1, game.Property("mill-street").Level);
        Assert.Equal(1500, game.Player(0).Cash); // no money back
    }

    [Fact]
    public void Fire_WithATie_YouChoose()
    {
        var game = new TestGame();
        game.Give(0, "mill-street", "cobble-lane");
        game.Property("mill-street").Level = 2;
        game.Property("cobble-lane").Level = 2;
        game.DrawEvent(0, "fire");
        Assert.Equal(DecisionKind.ChooseFireProperty, game.DecisionFor(0)!.Kind);
        game.Do(0, new ChooseProperty("mill-street"));
        Assert.Equal(1, game.Property("mill-street").Level);
        Assert.Equal(2, game.Property("cobble-lane").Level);
    }

    [Fact]
    public void Fire_HotelBurnsDownToFourHouses_AndReturnsItsPermit()
    {
        var game = new TestGame();
        game.Give(0, "mill-street");
        game.Property("mill-street").Level = 5;
        game.State.HotelPermits = 3;
        game.DrawEvent(0, "fire");
        Assert.Equal(4, game.Property("mill-street").Level);
        Assert.Equal(4, game.State.HotelPermits);
    }

    [Fact]
    public void Fire_AnInsuredMarker_StopsIt()
    {
        var game = new TestGame();
        game.Give(0, "mill-street");
        game.Property("mill-street").Level = 3;
        game.Player(0).IsInsured = true;
        game.DrawEvent(0, "fire");
        Assert.Equal(3, game.Property("mill-street").Level);
        Assert.False(game.Player(0).IsInsured);
    }

    [Fact]
    public void Fire_WithNoBuildings_DoesNothing()
    {
        var game = new TestGame();
        game.Player(0).IsInsured = true;
        game.DrawEvent(0, "fire");
        Assert.True(game.Player(0).IsInsured); // the marker isn't wasted
        Assert.Contains(game.State.Log, e => e is FireAvoided { Reason: "no buildings" });
    }

    [Fact]
    public void Recession_EveryoneButThePoorestPaysTenPercent()
    {
        var game = new TestGame(players: 4);
        game.Player(1).Cash = 500;
        game.Player(2).Cash = 500; // tied for least: also exempt
        game.Player(3).Cash = 1001;
        game.DrawEvent(0, "recession");
        Assert.Equal(1350, game.Player(0).Cash);
        Assert.Equal(500, game.Player(1).Cash);
        Assert.Equal(500, game.Player(2).Cash);
        Assert.Equal(901, game.Player(3).Cash); // 10% of 1001 rounds down to 100
    }

    [Fact]
    public void HousingBoom_PaysForHotelsAndSkyscrapers()
    {
        var game = new TestGame();
        game.Give(1, "mill-street", "cobble-lane");
        game.Property("mill-street").Level = 5;
        game.Property("cobble-lane").Level = 6;
        game.Give(0, "elm-avenue");
        game.Property("elm-avenue").Level = 4; // houses don't count
        game.DrawEvent(0, "housing-boom");
        Assert.Equal(1500, game.Player(0).Cash);
        Assert.Equal(1600, game.Player(1).Cash);
    }

    [Fact]
    public void CityFestival_YouGetMore()
    {
        var game = new TestGame(players: 3);
        game.DrawEvent(0, "city-festival");
        Assert.Equal(1600, game.Player(0).Cash);
        Assert.Equal(1550, game.Player(1).Cash);
        Assert.Equal(1550, game.Player(2).Cash);
    }

    [Fact]
    public void BlockParty_EveryoneElsePaysYou()
    {
        var game = new TestGame(players: 3);
        game.DrawEvent(0, "block-party");
        Assert.Equal(1550, game.Player(0).Cash);
        Assert.Equal(1475, game.Player(1).Cash);
        Assert.Equal(1475, game.Player(2).Cash);
    }

    [Fact]
    public void BlockParty_SomeoneShortMustRaiseMoney()
    {
        var game = new TestGame(players: 2);
        game.Player(1).Cash = 10;
        game.Give(1, "elm-avenue");
        game.DrawEvent(0, "block-party");
        Assert.Equal(DecisionKind.RaiseMoney, game.DecisionFor(1)!.Kind);
    }

    [Fact]
    public void Lottery_HighestRollTakesThePot_TiesRollAgain()
    {
        var game = new TestGame(players: 3);
        game.Player(2).Cash = 15; // can't afford the $20, so sits out
        game.StackEvents("lottery");
        game.PlaceBefore(0, "midtown-event", 5);
        game.Dice.Add(1, 4); // the move
        game.Dice.Add(5, 5); // tie between players 0 and 1
        game.Dice.Add(2, 6); // player 1 wins
        game.Do(0, new RollDice());
        game.Do(0, new Nudge(0));
        Assert.Equal(1480, game.Player(0).Cash);
        Assert.Equal(1520, game.Player(1).Cash);
        Assert.Equal(15, game.Player(2).Cash);
        Assert.Contains(game.State.Log, e => e is LotteryWon { WinnerId: 1, Pot: 40 });
    }

    [Fact]
    public void RushHour_EveryoneMovesBackTwo_WithoutResolvingOrGo()
    {
        var game = new TestGame(players: 3);
        game.Player(1).Position = 1; // just after GO: going back crosses GO without collecting
        game.Player(2).InJail = true;
        game.Player(2).Position = game.State.IndexOf("jail");
        game.DrawEvent(0, "rush-hour");
        int n = game.State.Board.Count;
        Assert.Equal(game.State.IndexOf("midtown-event") - 2, game.Player(0).Position);
        Assert.Equal(n - 1, game.Player(1).Position);
        Assert.Equal(1500, game.Player(1).Cash);
        Assert.Equal(game.State.IndexOf("jail"), game.Player(2).Position);
        Assert.Equal(DecisionKind.PostMoveActions, game.DecisionFor(0)!.Kind);
    }

    [Fact]
    public void RoadWork_NextTurnRollsOneDie()
    {
        var game = new TestGame();
        game.DrawEvent(0, "road-work");
        game.EndTurn(0);
        game.SkipTurn(1);

        game.Player(0).Momentum = 0;
        Assert.DoesNotContain(new ClearRoadWork(), game.Engine.GetLegalActions(0));
        game.PlaceBefore(0, "jail", 4);
        game.Dice.Add(4);
        game.Do(0, new RollDice());
        Assert.Equal(game.State.IndexOf("jail"), game.Player(0).Position);
    }

    [Fact]
    public void RoadWork_SpendATokenToRollBothDice()
    {
        var game = new TestGame();
        game.DrawEvent(0, "road-work");
        game.EndTurn(0);
        game.SkipTurn(1);

        game.Player(0).Momentum = 2;
        game.Do(0, new ClearRoadWork());
        Assert.Equal(1, game.Player(0).Momentum);
        game.PlaceBefore(0, "jail", 5);
        game.Roll(0, 1, 4);
        Assert.Equal(game.State.IndexOf("jail"), game.Player(0).Position);
    }

    [Fact]
    public void MomentumSurge_EveryoneGainsATokenAndYouGainTwo()
    {
        var game = new TestGame();
        game.DrawEvent(0, "momentum-surge");
        Assert.Equal(3, game.Player(0).Momentum);
        Assert.Equal(2, game.Player(1).Momentum);
    }

    [Fact]
    public void PropertyReassessment_PaysPerBuilding()
    {
        var game = new TestGame();
        game.Give(0, "mill-street", "cobble-lane");
        game.Property("mill-street").Level = 3; // 3 houses: $75
        game.Property("cobble-lane").Level = 5; // a hotel: $100
        game.DrawEvent(0, "property-reassessment");
        Assert.Equal(1325, game.Player(0).Cash);
        Assert.Contains(game.State.Log, e => e is CardPaymentMade { FromId: 0, ToId: null, Amount: 175 });
    }

    [Theory]
    [InlineData("tax-refund", 1600)]
    [InlineData("inheritance", 1650)]
    public void MoneyCards_PayYou(string card, int cash)
    {
        var game = new TestGame();
        game.DrawEvent(0, card);
        Assert.Equal(cash, game.Player(0).Cash);
    }

    [Fact]
    public void AdvanceToGo_CollectsGoEffects()
    {
        var game = new TestGame();
        game.Player(0).Momentum = 0;
        game.DrawEvent(0, "advance-to-go");
        Assert.Equal(0, game.Player(0).Position);
        Assert.Equal(1700, game.Player(0).Cash);
        Assert.Equal(1, game.Player(0).Momentum);
    }

    [Fact]
    public void GoToJail_EndsTheTurn_EvenAfterDoubles()
    {
        var game = new TestGame();
        game.StackEvents("go-to-jail");
        game.PlaceBefore(0, "midtown-event", 4);
        game.Roll(0, 2, 2);
        Assert.True(game.Player(0).InJail);
        Assert.Equal(DecisionKind.ChooseMoveMethod, game.DecisionFor(1)!.Kind);
    }

    [Fact]
    public void EmergencySession_CallsACouncilSession_WithTheDrawerSettingTheAgenda()
    {
        var game = new TestGame();
        game.DrawEvent(0, "emergency-session");
        Assert.Contains(game.State.Log, e => e is CouncilStarted { Emergency: true, SetterId: 0 });
        Assert.Equal(DecisionKind.ChooseOrdinance, game.DecisionFor(0)!.Kind);
    }
}
