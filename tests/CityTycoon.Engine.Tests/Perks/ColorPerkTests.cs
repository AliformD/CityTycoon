using CityTycoon.Engine.Actions;
using CityTycoon.Engine.Content;
using CityTycoon.Engine.Events;
using CityTycoon.Engine.Rules;
using CityTycoon.Engine.State;
using CityTycoon.Engine.Tiles;
using CityTycoon.Engine.Tests.Helpers;

namespace CityTycoon.Engine.Tests.Perks;

/// <summary>Section 13.2: owning a whole color group turns on its perk.</summary>
public class ColorPerkTests
{
    private static readonly string[] Brown = ["mill-street", "cobble-lane"];
    private static readonly string[] LightBlue = ["elm-avenue", "maple-avenue", "birch-avenue"];
    private static readonly string[] Pink = ["gallery-row", "theater-way", "studio-street"];
    private static readonly string[] Orange = ["market-street", "baker-street", "fountain-square"];
    private static readonly string[] Red = ["pier-road", "anchor-street", "lighthouse-lane"];
    private static readonly string[] Yellow = ["harbor-boulevard", "wharf-street", "marina-drive"];
    private static readonly string[] Green = ["summit-avenue", "grand-plaza", "crown-street"];
    private static readonly string[] DarkBlue = ["skyline-drive", "platinum-row"];

    private static void PassGo(TestGame game, int playerId)
    {
        game.PlaceBefore(playerId, "go", 5);
        game.Roll(playerId, 1, 4);
    }

    [Fact]
    public void Brown_PassingGo_PaysFiftyExtra()
    {
        var game = new TestGame();
        game.Give(0, Brown);
        PassGo(game, 0);
        Assert.Contains(game.State.Log, e => e is PerkIncome { PlayerId: 0, Amount: 50, Perk: "Community Roots" });
        Assert.Equal(1500 + 200 + 50, game.Player(0).Cash);
    }

    [Fact]
    public void Brown_HalfAGroup_DoesNothing()
    {
        var game = new TestGame();
        game.Give(0, "mill-street");
        PassGo(game, 0);
        Assert.DoesNotContain(game.State.Log, e => e is PerkIncome);
    }

    [Fact]
    public void AMortgagedProperty_TurnsThePerkOff()
    {
        var game = new TestGame();
        game.Give(0, Brown);
        game.Property("mill-street").IsMortgaged = true;
        PassGo(game, 0);
        Assert.DoesNotContain(game.State.Log, e => e is PerkIncome);
    }

    [Fact]
    public void LightBlue_PassingGo_GivesAToken_AndRaisesTheLimit()
    {
        var game = new TestGame();
        game.Give(0, LightBlue);
        game.Player(0).Momentum = 2; // the normal limit
        PassGo(game, 0);
        Assert.Equal(3, game.Engine.GetView(0).Me.MomentumCap);
        Assert.Equal(3, game.Player(0).Momentum); // GO itself gives none, the perk gives 1
    }

    [Fact]
    public void Pink_HandLimitGoesUpByOne()
    {
        var game = new TestGame();
        Assert.Equal(3, game.Engine.GetView(0).Me.HandLimit);
        game.Give(0, Pink);
        Assert.Equal(4, game.Engine.GetView(0).Me.HandLimit);
    }

    [Fact]
    public void Pink_OpportunityDraw_AsksYouToKeepOneOfTwo()
    {
        var game = new TestGame();
        game.Give(0, Pink);
        game.StackOpportunities("tip-off", "insurance");
        game.RollOnto(0, "harbor-opportunity");

        var decision = game.DecisionFor(0)!;
        Assert.Equal(DecisionKind.KeepCard, decision.Kind);
        Assert.Equal(2, decision.Cards!.Count);

        string keep = decision.Cards[1];
        game.Do(0, new KeepCard(keep));
        Assert.Equal([keep], game.Player(0).Hand);
        Assert.Contains(game.State.Log, e => e is CardKept { PlayerId: 0 });
    }

    [Fact]
    public void Orange_OpponentInJail_PaysTheOwnerAtTheStartOfTheirTurn()
    {
        var game = new TestGame();
        game.Give(0, Orange);
        game.JailThenStartTheirTurn(1);
        Assert.Contains(game.State.Log, e => e is PerkPaid { FromId: 1, ToId: 0, Amount: 25 });
    }

    [Fact]
    public void Orange_OpponentNotInJail_PaysNothing()
    {
        var game = new TestGame();
        game.Give(0, Orange);
        game.SkipTurn(0);
        Assert.DoesNotContain(game.State.Log, e => e is PerkPaid);
    }

    [Fact]
    public void Red_HostileTakeover_IsOfferedOncePerLap()
    {
        var game = new TestGame();
        game.Give(0, Red);
        game.Give(1, "mill-street");
        Assert.True(game.Engine.GetView(0).Me.TakeoverReady);
        Assert.Contains(new HostileTakeover("mill-street"), game.Engine.GetLegalActions(0));

        game.Do(0, new HostileTakeover("mill-street"));
        Assert.Contains(game.State.Log, e => e is HostileTakeoverUsed { PlayerId: 0, PropertyId: "mill-street" });
        Assert.True(game.Player(0).TakeoverUsed);
        Assert.Equal(DecisionKind.SubmitBid, game.DecisionFor(0)!.Kind);
    }

    [Fact]
    public void Red_NoPerk_NoTakeover()
    {
        var game = new TestGame();
        game.Give(1, "mill-street");
        Assert.DoesNotContain(game.Engine.GetLegalActions(0), a => a is HostileTakeover);
    }

    [Fact]
    public void Red_TakeoverIsUsedUpUntilYouPassGoAgain()
    {
        var game = new TestGame();
        game.Give(0, Red);
        game.Give(1, "mill-street");
        game.Do(0, new HostileTakeover("mill-street"));
        game.EveryonePasses();
        Assert.DoesNotContain(game.Engine.GetLegalActions(0), a => a is HostileTakeover);

        PassGo(game, 0);
        Assert.False(game.Player(0).TakeoverUsed);
    }

    [Fact]
    public void Yellow_LoansAreCheaperToRepay_AndYouCanHoldThree()
    {
        var game = new TestGame();
        game.Give(0, Yellow);
        var me = game.Engine.GetView(0).Me;
        Assert.Equal(225, me.RepayCost(200)); // half the $50 interest
        Assert.Equal(470, me.RepayCost(400)); // half the $140 interest
        Assert.Equal(3, me.MaxLoans);

        game.Do(0, new TakeLoan(200));
        game.Do(0, new TakeLoan(200));
        game.Do(0, new TakeLoan(200));
        Assert.Equal(3, game.Player(0).Loans);
        game.Do(0, new RepayLoan(200));
        Assert.Equal(1500 + 600 - 225, game.Player(0).Cash);
    }

    [Fact]
    public void Green_Dividends_PayPerHotel()
    {
        var game = new TestGame();
        game.Give(1, Green);
        game.Property("summit-avenue").Level = 5;
        game.Property("grand-plaza").Level = 5;
        game.SkipTurn(0);
        Assert.Contains(game.State.Log, e => e is PerkIncome { PlayerId: 1, Amount: 40, Perk: "Dividends" });
    }

    [Fact]
    public void Green_NoTowers_NoDividend()
    {
        var game = new TestGame();
        game.Give(1, Green);
        game.SkipTurn(0);
        Assert.DoesNotContain(game.State.Log, e => e is PerkIncome);
    }

    /// <summary>
    /// A game where a Government tile sits right next to a Dark Blue property. Where the tiles go is
    /// random, so this tries seeds until the board lines up.
    /// </summary>
    private static TestGame GameWithTileNextToDarkBlue(string tileId, out string property)
    {
        for (ulong seed = 1; seed < 500; seed++)
        {
            var game = new TestGame(seed: seed);
            foreach (string id in DarkBlue)
            {
                var tile = game.State.Board.FirstOrDefault(s => s.Kind == SpaceKind.GovernmentTile
                    && TileRules.Neighbors(game.State, s).Contains(id));
                if (tile == null)
                    continue;
                tile.TileId = tileId;
                tile.IsRevealed = true;
                property = id;
                return game;
            }
        }
        throw new InvalidOperationException("No seed put a Government tile next to Dark Blue.");
    }

    [Fact]
    public void DarkBlue_RentIgnoresNegativeModifiers()
    {
        var game = GameWithTileNextToDarkBlue("landfill", out string id);
        game.Give(1, id);
        Assert.Equal(-50, game.Engine.GetView(0).Properties[id].RentPercent);
        int halved = RentCalculator.Calculate(game.State, GameContent.Default, id, 7);

        game.Give(1, DarkBlue); // now the whole group: the Landfill no longer counts
        Assert.Equal(0, game.Engine.GetView(0).Properties[id].RentPercent);
        int full = RentCalculator.Calculate(game.State, GameContent.Default, id, 7);
        Assert.True(full > halved * 3 / 2, $"{full} should be about double {halved}");
    }

    [Fact]
    public void DarkBlue_StillGetsPositiveModifiers()
    {
        var game = GameWithTileNextToDarkBlue("city-hall", out string id); // +25% rent next door
        game.Give(1, DarkBlue);
        Assert.Equal(25, game.Engine.GetView(0).Properties[id].RentPercent);
    }

    [Fact]
    public void DarkBlue_SkyscraperCostsTheSameAsAHotel()
    {
        var content = GameContent.Default;
        var skyline = content.Property("skyline-drive");
        Assert.Equal(skyline.HouseCost, BuildingRules.LevelCost(content, skyline, content.Rules.SkyscraperLevel));

        var elm = content.Property("summit-avenue");
        Assert.Equal(elm.HouseCost * 3, BuildingRules.LevelCost(content, elm, content.Rules.SkyscraperLevel));
    }
}
