using System.Text.Json.Nodes;
using CityTycoon.Engine.Actions;
using CityTycoon.Engine.Events;
using CityTycoon.Engine.Tests.Helpers;

namespace CityTycoon.Engine.Tests.Characters;

/// <summary>Decision #153: the balance pass after the Hard bot report (Medic, Politician, Casino coin flip).</summary>
public class BalancePassTests
{
    // ----- The Medic is paid what others spend on the Hospital -----

    [Fact]
    public void HospitalBill_GoesToTheMedic_NotTheBank()
    {
        var game = new TestGame(2, characters: ["lawyer", "medic"]);
        game.DrawEvent(0, "sprained-ankle");

        int bill = game.Engine.Content.Rules.HospitalBill;
        Assert.Equal(1500 - bill, game.Player(0).Cash);
        Assert.Equal(1500 + bill, game.Player(1).Cash);
        Assert.Contains(game.State.Log, e => e is HospitalBillPaid { PlayerId: 0, ToId: 1 });
    }

    [Fact]
    public void HospitalBill_GoesToTheBank_WithoutAMedic()
    {
        var game = new TestGame(2, characters: ["lawyer", "mogul"]);
        game.DrawEvent(0, "sprained-ankle");
        Assert.Equal(1500, game.Player(1).Cash);
        Assert.Contains(game.State.Log, e => e is HospitalBillPaid { PlayerId: 0, ToId: null });
    }

    [Fact]
    public void InsuredMarker_BoughtByAnotherPlayer_PaysTheMedic()
    {
        var game = new TestGame(2, characters: ["lawyer", "medic"]);
        game.Player(0).Momentum = 0;
        game.RollOnto(0, "hospital");
        game.Do(0, new BuyInsurance());

        int cost = game.Engine.Content.Rules.InsuredMarkerCost;
        Assert.Equal(1500 - cost, game.Player(0).Cash);
        Assert.Equal(1500 + cost, game.Player(1).Cash);
        Assert.Contains(game.State.Log, e => e is InsuranceBought { PlayerId: 0, ToId: 1 });
    }

    [Fact]
    public void Medic_GoingBankrupt_SendsHospitalMoneyBackToTheBank()
    {
        var game = new TestGame(3, characters: ["lawyer", "medic", "mogul"]);
        game.Player(1).IsBankrupt = true;
        game.DrawEvent(0, "sprained-ankle");
        Assert.Contains(game.State.Log, e => e is HospitalBillPaid { PlayerId: 0, ToId: null });
    }

    // ----- The Politician's Fundraising -----

    [Fact]
    public void Fundraising_DoublesCashFromTheBank()
    {
        var game = new TestGame(2, characters: ["politician", "lawyer"]);
        game.DrawEvent(0, "tax-refund"); // $100 from the bank
        Assert.Equal(1700, game.Player(0).Cash);
        Assert.Contains(game.State.Log, e => e is PerkIncome { PlayerId: 0, Amount: 100, Perk: "The Politician" });
    }

    [Fact]
    public void Fundraising_OnlyHelpsThePolitician()
    {
        var game = new TestGame(2, characters: ["lawyer", "politician"]);
        game.DrawEvent(0, "city-festival"); // $100 for the drawer, $50 for everyone else
        Assert.Equal(1600, game.Player(0).Cash);
        Assert.Equal(1600, game.Player(1).Cash); // $50, doubled by the bank
    }

    [Fact]
    public void Fundraising_MatchesCashPaidByOtherPlayers_FromTheBank()
    {
        var game = new TestGame(3, characters: ["politician", "lawyer", "mogul"]);
        game.DrawEvent(0, "block-party"); // each other player pays $25
        Assert.Equal(1500 + 50 + 50, game.Player(0).Cash);
        Assert.Equal(1475, game.Player(1).Cash); // the others still pay only $25
        Assert.Equal(1475, game.Player(2).Cash);
    }

    // ----- The Casino coin flip -----

    private static TestGame WithCoinFlip() =>
        new(2, content: TestContent.With(new() { ["startingCash"] = 1500, ["goSalary"] = 200, ["casinoLandingFlip"] = 20 }));

    [Theory]
    [InlineData(2, 20)]   // an even die wins
    [InlineData(5, -20)]  // an odd die loses
    public void LandingOnTheCasino_FlipsACoin(int die, int net)
    {
        var game = WithCoinFlip();
        game.Player(0).Momentum = 0;
        game.PlaceBefore(0, "casino", 5);
        game.Dice.Add(1, 4, die);
        game.Do(0, new RollDice());

        Assert.Equal(1500 + net, game.Player(0).Cash);
        Assert.Contains(game.State.Log, e => e is CasinoCoinFlip { PlayerId: 0 } f && f.Net == net);
        Assert.Equal(DecisionKind.CasinoBet, game.DecisionFor(0)!.Kind); // then the bet, as before
    }

    [Fact]
    public void CasinoCoinFlip_NeverTakesMoreThanThePlayerHas()
    {
        var game = WithCoinFlip();
        game.Player(0).Momentum = 0;
        game.Player(0).Cash = 15;
        game.PlaceBefore(0, "casino", 5);
        game.Dice.Add(1, 4, 3);
        game.Do(0, new RollDice());
        Assert.Equal(0, game.Player(0).Cash);
        Assert.Contains(game.State.Log, e => e is CasinoCoinFlip { Net: -15 });
    }
}
