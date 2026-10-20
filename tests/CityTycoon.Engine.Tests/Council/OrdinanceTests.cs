using CityTycoon.Engine.Actions;
using CityTycoon.Engine.Content;
using CityTycoon.Engine.Events;
using CityTycoon.Engine.Modifiers;
using CityTycoon.Engine.Ordinances;
using CityTycoon.Engine.Rules;
using CityTycoon.Engine.State;
using CityTycoon.Engine.Tests.Helpers;
using CityTycoon.Engine.Views;

namespace CityTycoon.Engine.Tests.Council;

/// <summary>Section 11.4: what each of the 17 ordinance cards does.</summary>
public class OrdinanceTests
{
    private static readonly GameContent Content = GameContent.Default;

    private static TestGame Game(int players = 2)
    {
        var game = new TestGame(players);
        game.AllResidential();
        return game;
    }

    private static void Activate(TestGame game, string ordinanceId, string? district = null, int? rounds = null) =>
        game.State.ActiveOrdinances.Add(new ActiveOrdinance(ordinanceId, district, rounds));

    // ---------------------------------------------------------- the deck

    [Fact]
    public void TheDeck_Has17Cards_Of14Kinds()
    {
        Assert.Equal(14, Content.Ordinances.Count);
        Assert.Equal(17, Content.Ordinances.Sum(o => o.Copies));
        Assert.Equal(17, new TestGame().State.OrdinanceDeck.Count);
        Assert.Equal(3, Content.Ordinance("rezone-up").Copies);
        Assert.Equal(2, Content.Ordinance("downzone").Copies);
    }

    [Fact]
    public void EveryOrdinance_CanBeVotedThrough_AndDoesWhatItSays()
    {
        foreach (OrdinanceDefinition ordinance in Content.Ordinances)
        {
            var game = Game();
            game.Give(0, "mill-street", "cobble-lane");
            game.Property("mill-street").Level = 2;   // so the Property Tax Hike has something to tax
            string? district = ordinance.Target == OrdinanceTarget.District ? "old-town" : null;
            game.PassOrdinance(ordinance.Id, district);

            Assert.True(game.LastResolved().Passed, ordinance.Id);
            Assert.True(ordinance.StaysActive == game.State.ActiveOrdinances.Any(a => a.OrdinanceId == ordinance.Id), ordinance.Id);
            Assert.Contains(ordinance.Id, game.State.OrdinanceDiscard);
        }
    }

    [Fact]
    public void AFailedOrdinance_DoesNothing()
    {
        var game = Game();
        game.ConveneAndPropose("rezone-up", "old-town");
        game.EveryoneVotes(pass: false);
        Assert.Equal(ZoningLevel.Residential, game.State.Zoning["old-town"]);
        Assert.Empty(game.State.ActiveOrdinances);
    }

    [Fact]
    public void ABrokenOrdinanceFile_IsCaughtWhenTheGameLoads()
    {
        var bad = new OrdinancesFile(1, [new OrdinanceDefinition("oops", "Oops", "?", System.Text.Json.JsonDocument.Parse("""{ "type": "Nonsense" }""").RootElement)]);
        var ex = Assert.Throws<ContentException>(() => new GameContent(
            Content.Rules, Content.Board, new PropertiesFile(1, Content.Properties.Values.ToList()), new GroupsFile(1, Content.Groups.Values.ToList()),
            new GovTilesFile(1, Content.GovTilesPerGame, Content.GovTiles), ordinances: bad));
        Assert.Contains("unknown effect type", ex.Message);
    }

    // ---------------------------------------------------------- zoning

    [Fact]
    public void RezoneUp_RaisesTheChosenDistrictOneLevel()
    {
        var game = Game();
        game.PassOrdinance("rezone-up", "harbor");
        Assert.Equal(ZoningLevel.Commercial, game.State.Zoning["harbor"]);
        Assert.Equal(ZoningLevel.Residential, game.State.Zoning["old-town"]);
        Assert.Contains(game.State.Log, e => e is ZoningChanged { DistrictId: "harbor", From: ZoningLevel.Residential, To: ZoningLevel.Commercial });
    }

    [Fact]
    public void Downzone_LowersTheChosenDistrictOneLevel()
    {
        var game = Game();
        game.PassOrdinance("downzone", "harbor");
        Assert.Equal(ZoningLevel.Restricted, game.State.Zoning["harbor"]);
    }

    [Fact]
    public void Downzone_CantTargetARestrictedDistrict_AndRezoneUpCantTargetAHighRiseOne()
    {
        var game = Game();
        game.State.Zoning["old-town"] = ZoningLevel.Restricted;
        game.Convene("downzone");
        game.Do(0, new ChooseOrdinance("downzone"));
        Assert.DoesNotContain("old-town", game.DecisionFor(0)!.Districts!);
    }

    [Fact]
    public void Downzoning_LeavesExistingBuildingsStanding_ButStopsNewOnes()
    {
        var game = Game();
        game.Give(0, "mill-street", "cobble-lane");
        game.State.Zoning["old-town"] = ZoningLevel.Commercial;
        game.Property("mill-street").Level = 5;   // a hotel
        game.Property("cobble-lane").Level = 5;

        game.PassOrdinance("downzone", "old-town");

        Assert.Equal(ZoningLevel.Residential, game.State.Zoning["old-town"]);
        Assert.Equal(5, game.Property("mill-street").Level);   // grandfathered: still there, still earning
        Assert.Contains("zoning", game.Engine.WhyIllegal(0, new Build("mill-street")));

        // Sold down to 4 houses is fine; but a hotel can't be rebuilt.
        game.Do(0, new SellBuilding("mill-street"));
        game.Do(0, new SellBuilding("cobble-lane"));
        Assert.Contains("zoning", game.Engine.WhyIllegal(0, new Build("mill-street")) ?? "zoning");
    }

    [Fact]
    public void HistoricPreservation_FreezesADistrictForever()
    {
        var game = Game();
        game.PassOrdinance("historic-preservation", "old-town");
        Assert.Single(game.State.ActiveOrdinances);
        Assert.Null(game.State.ActiveOrdinances[0].RoundsLeft);

        // Many rounds later, it is still there.
        game.EndTurn(0);
        game.SkipTurn(1);
        for (int round = 1; round <= 3; round++)
            for (int id = 0; id < 2; id++)
                game.SkipTurn(id);
        Assert.Single(game.State.ActiveOrdinances);

        // Rezone Up and Downzone can no longer pick Old Town.
        Assert.True(OrdinanceRules.IsFrozen(game.State, Content, "old-town"));
        game.State.OrdinanceDeck.Clear();
    }

    [Fact]
    public void AFrozenDistrict_IsNotOfferedToRezoneOrDownzone()
    {
        var game = Game();
        Activate(game, "historic-preservation", "old-town");
        game.Convene("rezone-up");
        game.Do(0, new ChooseOrdinance("rezone-up"));
        Assert.DoesNotContain("old-town", game.DecisionFor(0)!.Districts!);
    }

    [Fact]
    public void AFrozenDistrict_CantBeRaisedByTheInfrastructureBond()
    {
        var game = Game();
        game.State.Zoning["old-town"] = ZoningLevel.Restricted;   // the lowest, but frozen
        Activate(game, "historic-preservation", "old-town");
        game.PassOrdinance("infrastructure-bond");
        Assert.Equal(ZoningLevel.Restricted, game.State.Zoning["old-town"]);
    }

    // ---------------------------------------------------------- permits

    [Fact]
    public void HotelPermitExpansion_Adds4HotelPermits()
    {
        var game = Game();
        int before = game.State.HotelPermits;
        game.PassOrdinance("hotel-permit-expansion");
        Assert.Equal(before + 4, game.State.HotelPermits);
    }

    [Fact]
    public void SkyscraperPermitExpansion_Adds2SkyscraperPermits()
    {
        var game = Game();
        int before = game.State.SkyscraperPermits;
        game.PassOrdinance("skyscraper-permit-expansion");
        Assert.Equal(before + 2, game.State.SkyscraperPermits);
    }

    // ---------------------------------------------------------- timed ordinances

    [Fact]
    public void BuildingMoratorium_StopsEveryoneBuilding_For2Rounds()
    {
        var game = Game();
        game.Give(0, "mill-street", "cobble-lane");
        game.PassOrdinance("building-moratorium");
        Assert.Contains("Moratorium", game.Engine.WhyIllegal(0, new Build("mill-street")));
        Assert.Equal(2, game.State.ActiveOrdinances.Single().RoundsLeft);
    }

    [Fact]
    public void ATimedOrdinance_CountsDownAtTheEndOfEachRound_ThenEnds()
    {
        var game = Game();
        game.Give(0, "mill-street", "cobble-lane");
        Activate(game, "building-moratorium", rounds: 2);
        Assert.NotNull(game.Engine.WhyIllegal(0, new Build("mill-street")));

        game.SkipTurn(0); game.SkipTurn(1);   // round 1 ends: 1 round left
        Assert.Equal(1, game.State.ActiveOrdinances.Single().RoundsLeft);
        Assert.NotNull(game.Engine.WhyIllegal(0, new Build("mill-street")));

        game.SkipTurn(0); game.SkipTurn(1);   // round 2 ends: it's over
        Assert.Empty(game.State.ActiveOrdinances);
        Assert.Contains(game.State.Log, e => e is OrdinanceEnded { OrdinanceId: "building-moratorium" });
        Assert.Null(game.Engine.WhyIllegal(0, new Build("mill-street")));
    }

    [Fact]
    public void AnOrdinancePassedAtTheEndOfRound4_WithDuration3_CoversRounds5To7()
    {
        var game = new TestGame(2);
        game.State.OrdinanceDeck.InsertRange(0, ["transit-investment", "stimulus-check"]);
        for (int round = 1; round <= 4; round++)
            for (int id = 0; id < 2; id++)
                game.SkipTurn(id);

        var setter = game.Engine.GetPendingDecisions().Single();
        game.Do(setter.PlayerId, new ChooseOrdinance("transit-investment"));
        game.EveryoneVotes(pass: true);
        Assert.Equal(5, game.State.Round);
        Assert.Equal(3, game.State.ActiveOrdinances.Single().RoundsLeft);   // not ticked on the round it passed

        game.SkipTurn(0); game.SkipTurn(1);   // round 5 over
        game.SkipTurn(0); game.SkipTurn(1);   // round 6 over
        Assert.Equal(1, game.State.ActiveOrdinances.Single().RoundsLeft);
        game.SkipTurn(0); game.SkipTurn(1);   // round 7 over
        Assert.Empty(game.State.ActiveOrdinances);
        Assert.Equal(8, game.State.Round);
    }

    [Fact]
    public void PassingAnActiveOrdinanceAgain_RefreshesItsTimer_InsteadOfStackingIt()
    {
        var game = Game();
        Activate(game, "transit-investment", rounds: 1);
        game.PassOrdinance("transit-investment");
        Assert.Equal(3, game.State.ActiveOrdinances.Single().RoundsLeft);
    }

    [Fact]
    public void RentControl_LowersRentInOneDistrictOnly()
    {
        var game = Game();
        game.Give(1, "mill-street", "cobble-lane", "gallery-row");
        int before = RentCalculator.Calculate(game.State, Content, "mill-street", 7);
        int otherBefore = RentCalculator.Calculate(game.State, Content, "gallery-row", 7);
        Assert.Equal(0, PlayerView.Create(game.State, Content, 0).Properties["mill-street"].RentPercent);   // fills the modifier cache

        Activate(game, "rent-control", "old-town", 3);

        Assert.Equal(-25, PlayerView.Create(game.State, Content, 0).Properties["mill-street"].RentPercent);
        Assert.True(RentCalculator.Calculate(game.State, Content, "mill-street", 7) < before);
        Assert.Equal(otherBefore, RentCalculator.Calculate(game.State, Content, "gallery-row", 7));
    }

    [Fact]
    public void TourismBoost_RaisesRentInOneDistrict()
    {
        var game = Game();
        game.Give(1, "mill-street", "cobble-lane");
        int before = RentCalculator.Calculate(game.State, Content, "mill-street", 7);
        Activate(game, "tourism-boost", "old-town", 3);
        Assert.Equal(25, PlayerView.Create(game.State, Content, 0).Properties["mill-street"].RentPercent);
        Assert.True(RentCalculator.Calculate(game.State, Content, "mill-street", 7) > before);
    }

    [Fact]
    public void RentControlAndTourismBoost_CancelEachOtherOut()
    {
        var game = Game();
        Activate(game, "rent-control", "old-town", 3);
        Activate(game, "tourism-boost", "old-town", 3);
        Assert.Equal(0, PlayerView.Create(game.State, Content, 0).Properties["mill-street"].RentPercent);
    }

    [Fact]
    public void TransitInvestment_RaisesRailroadRents50Percent_AndNothingElse()
    {
        var game = Game();
        game.Give(1, "old-town-station", "mill-street");
        Activate(game, "transit-investment", rounds: 3);
        var view = PlayerView.Create(game.State, Content, 0);
        Assert.Equal(50, view.Properties["old-town-station"].RentPercent);
        Assert.Equal(0, view.Properties["mill-street"].RentPercent);
    }

    [Fact]
    public void CasinoRegulation_CapsBetsAndDuelStakes_At200()
    {
        var game = Game();
        Activate(game, "casino-regulation", rounds: 4);
        game.RollOnto(0, "casino");
        var ask = game.DecisionFor(0)!;
        Assert.Equal(DecisionKind.CasinoBet, ask.Kind);
        Assert.Equal(200, ask.MaxBet);
        Assert.False(game.Engine.Submit(0, new BetHighLow(201, true)).Accepted);
        Assert.False(game.Engine.Submit(0, new ChallengeDuel(1, 201)).Accepted);
        game.Dice.Add(6, 5);
        Assert.True(game.Engine.Submit(0, new BetHighLow(200, true)).Accepted);
    }

    [Fact]
    public void CasinoRegulation_AlsoCapsTheDiceDuelCard()
    {
        var game = Game();
        game.GiveCard(0, "dice-duel");
        var play = new PlayCard("dice-duel", TargetPlayerId: 1, Number: 300);
        Assert.Contains(play, game.Engine.GetLegalActions(0).Concat(new[] { (GameAction)play }));   // sanity: the type exists
        Activate(game, "casino-regulation", rounds: 4);
        Assert.False(game.Engine.Submit(0, play).Accepted);
        Assert.True(game.Engine.Submit(0, play with { Number = 200 }).Accepted);
    }

    [Fact]
    public void WithoutCasinoRegulation_BetsAreNotCapped()
    {
        var game = Game();
        game.RollOnto(0, "casino");
        Assert.Equal(1500, game.DecisionFor(0)!.MaxBet);
    }

    // ---------------------------------------------------------- one-time money ordinances

    [Fact]
    public void PropertyTaxHike_Charges10PerBuildingLevel()
    {
        var game = Game();
        game.Give(0, "mill-street", "cobble-lane");
        game.Give(1, "gallery-row");
        game.Property("mill-street").Level = 2;
        game.Property("cobble-lane").Level = 5;   // a hotel is 5 levels
        game.Property("gallery-row").Level = 1;
        game.PassOrdinance("property-tax-hike");
        Assert.Equal(1500 - 70, game.Player(0).Cash);
        Assert.Equal(1500 - 10, game.Player(1).Cash);
        Assert.Contains(game.State.Log, e => e is CouncilPaymentMade { PlayerId: 0, Amount: 70 });
    }

    [Fact]
    public void PropertyTaxHike_CantBePicked_WhenNobodyHasBuilt()
    {
        var game = Game();
        game.Convene("property-tax-hike");
        Assert.Contains(game.State.Log, e => e is OrdinanceProposed { OrdinanceId: "stimulus-check" });
    }

    [Fact]
    public void InfrastructureBond_EveryonePays50_AndTheLowestZonedDistrictGoesUp()
    {
        var game = Game();
        game.State.Zoning["uptown"] = ZoningLevel.Restricted;
        game.PassOrdinance("infrastructure-bond");
        Assert.Equal(1450, game.Player(0).Cash);
        Assert.Equal(1450, game.Player(1).Cash);
        Assert.Equal(ZoningLevel.Residential, game.State.Zoning["uptown"]);
    }

    [Fact]
    public void InfrastructureBond_LetsTheAgendaSetterBreakATie()
    {
        var game = Game();   // all four districts tie for lowest
        game.ConveneAndPropose("infrastructure-bond");
        game.EveryoneVotes(pass: true);
        var ask = game.DecisionFor(0)!;
        Assert.Equal(DecisionKind.ChooseDistrict, ask.Kind);
        Assert.Equal(4, ask.Districts!.Count);
        game.Do(0, new ChooseDistrict("harbor"));
        Assert.Equal(ZoningLevel.Commercial, game.State.Zoning["harbor"]);
        Assert.Equal(ZoningLevel.Residential, game.State.Zoning["uptown"]);
        Assert.Equal(1450, game.Player(1).Cash);
    }

    [Fact]
    public void InfrastructureBond_CantBePicked_WhenEveryDistrictIsHighRise()
    {
        var game = Game();
        foreach (string d in game.State.Zoning.Keys.ToList())
            game.State.Zoning[d] = ZoningLevel.HighRise;
        game.Convene("infrastructure-bond");
        Assert.Contains(game.State.Log, e => e is OrdinanceProposed { OrdinanceId: "stimulus-check" });
    }

    [Fact]
    public void StimulusCheck_PaysEveryone100()
    {
        var game = Game(3);
        game.PassOrdinance("stimulus-check");
        Assert.All(game.State.Players, p => Assert.Equal(1600, p.Cash));
    }

    [Fact]
    public void PublicWorks_RevealsEveryHiddenGovernmentTile()
    {
        var game = Game();
        Assert.Contains(game.State.Board, s => s.Kind == SpaceKind.GovernmentTile && !s.IsRevealed);
        game.PassOrdinance("public-works");
        Assert.All(game.State.Board.Where(s => s.Kind == SpaceKind.GovernmentTile), s => Assert.True(s.IsRevealed));
        Assert.Equal(4, game.State.Log.OfType<TileRevealedByCouncil>().Count());
    }

    [Fact]
    public void PublicWorks_CantBePicked_WhenNothingIsHidden()
    {
        var game = Game();
        foreach (var s in game.State.Board.Where(s => s.Kind == SpaceKind.GovernmentTile))
            s.IsRevealed = true;
        game.Convene("public-works", other: "stimulus-check");
        Assert.Contains(game.State.Log, e => e is OrdinanceProposed { OrdinanceId: "stimulus-check" });
    }

    [Fact]
    public void AMoneyOrdinance_CanBankruptAPlayer_ThroughTheNormalRaiseMoneyFlow()
    {
        var game = Game();
        game.Give(1, "mill-street", "cobble-lane");
        game.Property("mill-street").Level = 5;
        game.Property("cobble-lane").Level = 5;
        game.Player(1).Cash = 10;
        game.PassOrdinance("property-tax-hike");
        // Player 1 owes $100 with $10 in cash, so they're asked to raise money rather than just losing it.
        Assert.Equal(DecisionKind.RaiseMoney, game.DecisionFor(1)!.Kind);
    }
}
