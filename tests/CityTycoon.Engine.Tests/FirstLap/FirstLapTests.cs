using System.Text.Json.Nodes;
using CityTycoon.Engine.Actions;
using CityTycoon.Engine.Events;
using CityTycoon.Engine.Tests.Helpers;

namespace CityTycoon.Engine.Tests.FirstLap;

/// <summary>
/// Decision #165: on your first lap you may buy what you land on at its listed price, and nobody uses
/// Momentum; afterwards auctions are open, except the last unsold piece of a set, which is silent and pay in full.
/// </summary>
public class FirstLapTests
{
    /// <summary>The real first-lap rules from rules.json, with the old money the other tests use.</summary>
    private static TestGame Game(int players = 3, Dictionary<string, JsonNode?>? changes = null)
    {
        var all = new Dictionary<string, JsonNode?> { ["startingCash"] = 1500, ["goSalary"] = 200, ["catchUpBonus"] = 100, ["voteContracts"] = false };
        foreach (var (name, value) in changes ?? [])
            all[name] = value;
        return new TestGame(players: players, content: TestContent.With(all));
    }

    [Fact]
    public void OnTheFirstLap_OnlyTheLanderIsAsked_AndCanBuyAtTheListedPrice()
    {
        var game = Game();
        game.RollOnto(0, "baker-street");

        var asked = game.Engine.GetPendingDecisions();
        Assert.Equal([0], asked.Select(d => d.PlayerId));
        Assert.Equal(DecisionKind.BuyAtListedPrice, asked[0].Kind);
        Assert.Equal(180, asked[0].MinBid);
        Assert.Equal([new BuyAtListedPrice(), new NoThanks()], game.MainActions(0));

        game.Do(0, new BuyAtListedPrice());
        Assert.Equal(0, game.Property("baker-street").OwnerId);
        Assert.False(game.Property("baker-street").IsPending);
        Assert.Equal(1500 - 180, game.Player(0).Cash);
        Assert.Contains(game.State.Log, e => e is AuctionWon { ListedPrice: true, Price: 180, PlayerId: 0 });
    }

    [Fact]
    public void SayingNo_SendsItToAnOpenAuction()
    {
        var game = Game();
        game.RollOnto(0, "baker-street");
        game.Do(0, new NoThanks());

        var decision = game.DecisionFor(0)!;
        Assert.Equal(DecisionKind.SubmitBid, decision.Kind);
        Assert.True(decision.OpenAuction);
    }

    [Fact]
    public void APlayerWhoCannotPay_MayOnlySayNo()
    {
        var game = Game();
        game.Player(0).Cash = 100;
        game.RollOnto(0, "baker-street");
        Assert.Equal([new NoThanks()], game.MainActions(0));
        Assert.False(game.Engine.Submit(0, new BuyAtListedPrice()).Accepted);
    }

    [Fact]
    public void AfterTheFirstLap_ThereIsNoOffer_AndTheFirstPieceIsAnOpenAuction()
    {
        var game = Game();
        game.Player(0).GoPasses = 1;
        game.RollOnto(0, "baker-street");

        var decision = game.DecisionFor(0)!;
        Assert.Equal(DecisionKind.SubmitBid, decision.Kind);
        Assert.True(decision.OpenAuction);
        Assert.DoesNotContain(game.State.Log, e => e is AuctionStarted { FixedPrice: true });
    }

    [Fact]
    public void TheLastPieceOfASet_IsASilentAuction_EvenOnTheFirstLap_AndPayInFull()
    {
        var game = Game();
        game.Give(1, "market-street");
        game.Give(2, "fountain-square");
        game.RollOnto(0, "baker-street");

        var asked = game.Engine.GetPendingDecisions();
        Assert.Equal(3, asked.Count);
        Assert.All(asked, d => Assert.Equal(DecisionKind.SubmitBid, d.Kind));
        Assert.All(asked, d => Assert.False(d.OpenAuction));
        Assert.All(asked, d => Assert.Equal(0, d.MaxDownPaymentBid));
        Assert.False(game.Engine.Submit(0, new PlaceBid(200, DownPayment: true)).Accepted);
    }

    [Fact]
    public void WithTheHouseRules_TheLastPieceCanBeBoughtAtTheListedPrice_OrWonWithADownPayment()
    {
        var game = Game(changes: new() { ["fixedPriceLastPiece"] = true });
        game.Give(1, "market-street", "fountain-square");
        game.RollOnto(0, "baker-street");
        Assert.Equal(DecisionKind.BuyAtListedPrice, game.DecisionFor(0)!.Kind);

        var silent = Game(changes: new() { ["lastPieceDownPayments"] = true });
        silent.Give(1, "market-street", "fountain-square");
        silent.Player(0).GoPasses = 1;
        silent.RollOnto(0, "baker-street");
        Assert.True(silent.DecisionFor(0)!.MaxDownPaymentBid > 0);
    }

    [Fact]
    public void TheMiddlePieceOfAThreePieceSet_IsOpen()
    {
        var game = Game();
        game.Give(1, "market-street");
        game.Player(0).GoPasses = 1;
        game.RollOnto(0, "baker-street");
        Assert.True(game.DecisionFor(0)!.OpenAuction);
    }

    [Fact]
    public void PassingGo_EndsYourFirstLap()
    {
        var game = Game();
        game.PlaceBefore(0, "go", 5);
        game.Roll(0, 1, 4);
        Assert.Equal(1, game.Player(0).GoPasses);
        Assert.Equal(0, game.Player(1).GoPasses);
    }

    [Fact]
    public void OnTheFirstLap_ThereIsNoNudge_NoPrecisionRoll_AndNoHustlerReroll()
    {
        var game = Game();
        game.SetCharacter(0, "hustler");
        Assert.DoesNotContain(new PrecisionRoll(), game.MainActions(0));

        game.PlaceBefore(0, "jail", 7);
        game.Dice.Add(3, 4);
        game.Do(0, new RollDice());
        // No Nudge or reroll question: the move happened straight away.
        Assert.Equal(DecisionKind.PostMoveActions, game.DecisionFor(0)!.Kind);
        Assert.Equal(game.State.IndexOf("jail"), game.Player(0).Position);
        Assert.Equal(2, game.Player(0).Momentum); // the tokens wait for the second lap
    }

    [Fact]
    public void AfterTheFirstLap_MomentumWorksAgain()
    {
        var game = Game();
        game.Player(0).GoPasses = 1;
        Assert.Contains(new PrecisionRoll(), game.MainActions(0));

        game.PlaceBefore(0, "jail", 7);
        game.Dice.Add(3, 4);
        game.Do(0, new RollDice());
        Assert.Equal(DecisionKind.ChooseNudge, game.DecisionFor(0)!.Kind);
    }

    [Fact]
    public void FirstLapEnds_First_EndsItForEveryone_WhenAnyonePassesGo()
    {
        var game = Game(changes: new() { ["firstLapEnds"] = "first" });
        game.Player(1).GoPasses = 1;
        game.RollOnto(0, "baker-street");
        Assert.Equal(DecisionKind.SubmitBid, game.DecisionFor(0)!.Kind);
    }

    [Fact]
    public void FirstLapEnds_Last_KeepsItGoing_UntilEveryoneHasPassedGo()
    {
        var game = Game(changes: new() { ["firstLapEnds"] = "last" });
        game.Player(0).GoPasses = 1;
        game.RollOnto(0, "baker-street");
        Assert.Equal(DecisionKind.BuyAtListedPrice, game.DecisionFor(0)!.Kind);
    }

    [Fact]
    public void TheOldLandingRules_StillWork_AsAHouseRule()
    {
        var game = Game(changes: new() { ["firstLapFixedPrice"] = false, ["silentAuctionFor"] = "firstOfSet" });
        game.RollOnto(0, "baker-street");
        Assert.All(game.Engine.GetPendingDecisions(), d => Assert.False(d.OpenAuction));
        Assert.Equal(3, game.Engine.GetPendingDecisions().Count);
    }
}
