using CityTycoon.Engine.Actions;
using CityTycoon.Engine.Content;
using CityTycoon.Engine.Events;
using CityTycoon.Engine.State;
using CityTycoon.Engine.Tests.Helpers;

namespace CityTycoon.Engine.Tests.Tiles;

/// <summary>
/// Section 12: Government tiles. With the test seed, the Old Town tile sits
/// between Maple Avenue and Birch Avenue (Light Blue: $6 rent, $50 a house),
/// and <see cref="TestGame.SetTile"/> makes it whichever tile a test needs.
/// </summary>
public class GovernmentTileTests
{
    // ----- Hidden and revealed (Section 12.1) -----

    [Fact]
    public void HiddenTile_HasNoEffect()
    {
        var game = new TestGame();
        game.SetTile("landfill", revealed: false);
        game.Give(1, "maple-avenue");
        game.RollOnto(0, "maple-avenue");
        Assert.Equal(1494, game.Player(0).Cash); // full $6
    }

    [Fact]
    public void LandingOnATile_RevealsItToEveryone_AndAppliesItsLandingEffect()
    {
        var game = new TestGame();
        game.SetTile("police-station", revealed: false);
        Assert.Null(game.Engine.GetView(1).Spaces[game.State.IndexOf("old-town-government")].TileId);

        game.RollOnto(0, "old-town-government");

        Assert.True(game.State.Space("old-town-government").IsRevealed);
        Assert.Contains(game.State.Log, e => e is TileRevealed { PlayerId: 0, TileId: "police-station" });
        Assert.Equal("police-station", game.Engine.GetView(1).Spaces[game.State.IndexOf("old-town-government")].TileId);
        Assert.Equal(1475, game.Player(0).Cash); // the $25 parking ticket
    }

    [Fact]
    public void PassingOverATile_DoesntRevealIt()
    {
        var game = new TestGame();
        game.SetTile("landfill", revealed: false);
        game.PlaceBefore(0, "old-town-government", 2);
        game.Roll(0, 1, 2); // one space past it
        Assert.False(game.State.Space("old-town-government").IsRevealed);
    }

    [Fact]
    public void RushHour_MovingBackOntoATile_DoesntRevealIt()
    {
        var game = new TestGame();
        game.SetTile("landfill", revealed: false);
        game.Player(1).Position = game.State.IndexOf("old-town-government") + 2;
        game.DrawEvent(0, "rush-hour");
        Assert.Equal(game.State.IndexOf("old-town-government"), game.Player(1).Position);
        Assert.False(game.State.Space("old-town-government").IsRevealed);
    }

    [Fact]
    public void TilesAreInDifferentPlaces_InDifferentGames()
    {
        var layouts = Enumerable.Range(1, 10).Select(seed =>
        {
            var engine = GameEngine.NewGame(new GameSetup { Players = [new("A"), new("B")], Seed = (ulong)seed });
            return string.Join(",", engine.State.Board.Where(s => s.Kind == SpaceKind.GovernmentTile).Select(s => $"{s.Index}:{s.TileId}"));
        }).ToList();
        Assert.True(layouts.Distinct().Count() > 5, string.Join(" | ", layouts));
    }

    // ----- Neighbor effects (Section 12.3) -----

    [Fact]
    public void Landfill_NeighborRentIsHalved()
    {
        var game = new TestGame();
        game.SetTile("landfill");
        game.Give(1, "maple-avenue");
        game.RollOnto(0, "maple-avenue");
        Assert.Equal(1497, game.Player(0).Cash);
    }

    [Fact]
    public void CityHall_NeighborRentIsUpAQuarter_RoundedDown()
    {
        var game = new TestGame();
        game.SetTile("city-hall");
        game.Give(1, "maple-avenue");
        game.Property("maple-avenue").Level = 1; // $30
        game.RollOnto(0, "maple-avenue");
        Assert.Equal(1463, game.Player(0).Cash); // $37.50 rounds down to $37
    }

    [Fact]
    public void ATileOnlyAffectsItsTwoNeighbors()
    {
        var game = new TestGame();
        game.SetTile("landfill");
        game.Give(1, "elm-avenue"); // two spaces away
        game.RollOnto(0, "elm-avenue");
        Assert.Equal(1494, game.Player(0).Cash);
    }

    [Fact]
    public void PowerPlant_NeighborRentDown_AndBuildingThereHalfPrice()
    {
        var game = new TestGame();
        game.SetTile("power-plant");
        game.Give(1, "maple-avenue");
        Assert.Equal(4, game.Engine.ExplainRent("maple-avenue").Total); // $4.50 rounds down
        Assert.Equal(25, game.Engine.GetView(1).Properties["maple-avenue"].NextLevelCost);
        Assert.Equal(50, game.Engine.GetView(1).Properties["elm-avenue"].NextLevelCost);
    }

    [Fact]
    public void School_BuildingOnNeighborsCostsAQuarterLess()
    {
        var game = new TestGame();
        game.SetTile("school");
        game.Give(0, "maple-avenue", "birch-avenue", "elm-avenue");
        game.SkipTurnWithoutEnding(0);
        game.Do(0, new Build("maple-avenue"));
        Assert.Equal(1465, game.Player(0).Cash); // $37.50 rounds down to $35
    }

    [Fact]
    public void School_AddsToAConstructionPermit()
    {
        var game = new TestGame();
        game.SetTile("school");
        game.Give(0, "maple-avenue", "birch-avenue", "elm-avenue");
        game.Player(0).BuildDiscountThisTurn = 25;
        Assert.Equal(25, game.Engine.GetView(0).Properties["maple-avenue"].NextLevelCost); // 50% off
    }

    [Fact]
    public void PoliceStation_BlocksNegativeCards_ButNotHelpfulOnes()
    {
        var game = new TestGame();
        game.Give(1, "maple-avenue", "elm-avenue");
        game.Give(0, "birch-avenue");
        game.GiveCard(0, "hostile-bid", "grand-opening");
        var before = game.Engine.GetLegalActions(0).OfType<PlayCard>().ToList();
        Assert.Contains(new PlayCard("hostile-bid", TargetSpaceId: "maple-avenue"), before);
        Assert.Contains(new PlayCard("grand-opening", TargetSpaceId: "birch-avenue"), before);

        game.SetTile("police-station");
        var after = game.Engine.GetLegalActions(0).OfType<PlayCard>().ToList();
        Assert.DoesNotContain(new PlayCard("hostile-bid", TargetSpaceId: "maple-avenue"), after);
        Assert.Contains(new PlayCard("grand-opening", TargetSpaceId: "birch-avenue"), after); // helpful: allowed
        Assert.Contains(new PlayCard("hostile-bid", TargetSpaceId: "elm-avenue"), after); // not a neighbor
        Assert.False(game.Engine.Submit(0, new PlayCard("hostile-bid", TargetSpaceId: "maple-avenue")).Accepted);
    }

    [Fact]
    public void PoliceStation_DoesntStopAHelpfulCardOnAProtectedProperty()
    {
        var game = new TestGame();
        game.SetTile("police-station");
        game.Give(0, "maple-avenue");
        game.GiveCard(0, "grand-opening");
        Assert.Contains(new PlayCard("grand-opening", TargetSpaceId: "maple-avenue"), game.Engine.GetLegalActions(0));
        game.Do(0, new PlayCard("grand-opening", TargetSpaceId: "maple-avenue"));
        Assert.Contains(game.State.PropertyBoosts, b => b.PropertyId == "maple-avenue");
    }

    [Fact]
    public void FireStation_NeighborsAreSafeFromFire()
    {
        var game = new TestGame();
        game.SetTile("fire-station");
        game.Give(0, "maple-avenue", "elm-avenue");
        game.Property("maple-avenue").Level = 3; // tallest, but protected
        game.Property("elm-avenue").Level = 1;
        game.DrawEvent(0, "fire");
        Assert.Equal(3, game.Property("maple-avenue").Level);
        Assert.Equal(0, game.Property("elm-avenue").Level);
    }

    [Fact]
    public void FireStation_WhenEverythingIsSafe_TheInsuredMarkerIsKept()
    {
        var game = new TestGame();
        game.SetTile("fire-station");
        game.Give(0, "maple-avenue");
        game.Property("maple-avenue").Level = 2;
        game.Player(0).IsInsured = true;
        game.DrawEvent(0, "fire");
        Assert.Equal(2, game.Property("maple-avenue").Level);
        Assert.True(game.Player(0).IsInsured);
        Assert.Contains(game.State.Log, e => e is FireAvoided { Reason: "Fire Station" });
    }

    [Fact]
    public void TransitHub_HopInsteadOfPayingRent()
    {
        var game = new TestGame();
        game.SetTile("transit-hub");
        game.Give(1, "maple-avenue");
        game.RollOnto(0, "maple-avenue");
        Assert.Equal(DecisionKind.TransitHop, game.DecisionFor(0)!.Kind);

        game.Do(0, new HopToTransitHub());
        Assert.Equal(1475, game.Player(0).Cash);
        Assert.Equal(1500, game.Player(1).Cash); // no rent
        Assert.Equal(game.State.IndexOf("old-town-government"), game.Player(0).Position);
        Assert.Equal(DecisionKind.PostMoveActions, game.DecisionFor(0)!.Kind); // no ride offered
    }

    [Fact]
    public void TransitHub_OrJustPayTheRent()
    {
        var game = new TestGame();
        game.SetTile("transit-hub");
        game.Give(1, "maple-avenue");
        game.RollOnto(0, "maple-avenue");
        game.Do(0, new NoThanks());
        Assert.Equal(1494, game.Player(0).Cash);
        Assert.Equal(1506, game.Player(1).Cash);
    }

    [Fact]
    public void TransitHub_NotOfferedWhenYouCantAffordTheHop()
    {
        var game = new TestGame();
        game.SetTile("transit-hub");
        game.Give(1, "maple-avenue");
        game.Player(0).Cash = 20;
        game.RollOnto(0, "maple-avenue");
        Assert.Equal(14, game.Player(0).Cash);
    }

    // ----- Landing effects (Section 12.3) -----

    [Fact]
    public void FireStation_LandingGainsAToken()
    {
        var game = new TestGame();
        game.SetTile("fire-station", revealed: false);
        game.Player(0).Momentum = 0;
        game.RollOnto(0, "old-town-government");
        Assert.Equal(1, game.Player(0).Momentum);
    }

    [Fact]
    public void School_LandingDrawsAnOpportunityCard()
    {
        var game = new TestGame();
        game.SetTile("school");
        game.StackOpportunities("audit");
        game.RollOnto(0, "old-town-government");
        Assert.Equal(["audit"], game.Player(0).Hand);
    }

    [Fact]
    public void Landfill_LandingCostsACleanupFee()
    {
        var game = new TestGame();
        game.SetTile("landfill");
        game.RollOnto(0, "old-town-government");
        Assert.Contains(game.State.Log, e => e is TileFeePaid { FromId: 0, ToId: null, Amount: 25, TileId: "landfill" });
    }

    [Fact]
    public void CityHall_LandingPays10PerBuildingLevel()
    {
        var game = new TestGame();
        game.SetTile("city-hall");
        game.Give(0, "gallery-row", "studio-street");
        game.Property("gallery-row").Level = 2;
        game.Property("studio-street").Level = 5; // a hotel is 5 levels
        game.RollOnto(0, "old-town-government");
        Assert.Equal(1430, game.Player(0).Cash);
        Assert.Contains(game.State.Log, e => e is TileNotReadyYet { TileId: "city-hall" });
    }

    [Fact]
    public void PowerPlant_LandingPaysWhoeverOwnsBothUtilities()
    {
        var game = new TestGame();
        game.SetTile("power-plant");
        game.Give(1, "city-power-light", "city-water-works");
        game.RollOnto(0, "old-town-government");
        Assert.Equal(1450, game.Player(0).Cash);
        Assert.Equal(1550, game.Player(1).Cash);
    }

    [Fact]
    public void PowerPlant_LandingPaysTheBank_WhenNobodyOwnsBoth()
    {
        var game = new TestGame();
        game.SetTile("power-plant");
        game.Give(1, "city-power-light");
        game.RollOnto(0, "old-town-government");
        Assert.Equal(1450, game.Player(0).Cash);
        Assert.Equal(1500, game.Player(1).Cash);
    }

    [Fact]
    public void PowerPlant_YouDontPayYourself()
    {
        var game = new TestGame();
        game.SetTile("power-plant");
        game.Give(0, "city-power-light", "city-water-works");
        game.RollOnto(0, "old-town-government");
        Assert.Equal(1500, game.Player(0).Cash);
    }

    [Fact]
    public void TransitHub_RideToAnyRailroad_AndResolveIt()
    {
        var game = new TestGame();
        game.SetTile("transit-hub", revealed: false);
        game.RollOnto(0, "old-town-government");
        Assert.Equal(DecisionKind.TransitRide, game.DecisionFor(0)!.Kind);

        game.Do(0, new RideToRailroad("harbor-station"));
        Assert.Equal(1450, game.Player(0).Cash);
        Assert.Equal(game.State.IndexOf("harbor-station"), game.Player(0).Position);
        Assert.Contains(game.State.Log, e => e is AuctionStarted { PropertyId: "harbor-station" });
    }

    [Fact]
    public void TransitHub_RidingPastGo_CollectsIt()
    {
        var game = new TestGame();
        game.SetTile("transit-hub");
        game.Player(0).Momentum = 5; // at the cap, so GO adds no token
        game.RollOnto(0, "old-town-government");
        game.Do(0, new RideToRailroad("old-town-station")); // behind the hub: all the way round
        Assert.Equal(1650, game.Player(0).Cash);
    }

    // ----- Rent breakdown and saving -----

    [Fact]
    public void RentBreakdown_ShowsTheTile()
    {
        var game = new TestGame();
        game.SetTile("landfill");
        game.Give(1, "maple-avenue");
        var breakdown = game.Engine.ExplainRent("maple-avenue");
        Assert.Equal(3, breakdown.Total);
        Assert.Contains(breakdown.Lines, l => l.Label == "Landfill next door" && l.Change == "-50%");
        Assert.Equal(-50, game.Engine.GetView(0).Properties["maple-avenue"].RentPercent);
    }

    [Fact]
    public void GameState_SurvivesARoundTrip_OnTheTransitHub()
    {
        var game = new TestGame();
        game.SetTile("transit-hub");
        game.RollOnto(0, "old-town-government");

        GameState loaded = GameStateSerializer.FromJson(GameStateSerializer.ToJson(game.State));
        var resumed = new GameEngine(loaded, GameContent.Default, game.Dice);
        Assert.Equal(DecisionKind.TransitRide, resumed.GetPendingDecisions().Single().Kind);
        Assert.True(resumed.Submit(0, new NoThanks()).Accepted);
        Assert.True(loaded.Space("old-town-government").IsRevealed);
    }
}
