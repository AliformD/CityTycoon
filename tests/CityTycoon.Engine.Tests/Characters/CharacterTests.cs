using CityTycoon.Engine.Actions;
using CityTycoon.Engine.Content;
using CityTycoon.Engine.Events;
using CityTycoon.Engine.Modifiers;
using CityTycoon.Engine.Rules;
using CityTycoon.Engine.State;
using CityTycoon.Engine.Steps;
using CityTycoon.Engine.Tests.Helpers;

namespace CityTycoon.Engine.Tests.Characters;

/// <summary>Section 5: the eight characters, one ability each.</summary>
public class CharacterTests
{
    private static TestGame With(string character, int players = 2, int seat = 0)
    {
        var characters = new string[players];
        // A character in one seat; the others get plain stand-ins that do nothing in these tests.
        string[] fillers = ["mogul", "hustler", "commuter", "politician", "medic", "contractor"];
        int next = 0;
        for (int i = 0; i < players; i++)
            characters[i] = i == seat ? character : fillers.First(f => f != character && !characters.Contains(f) && next++ >= 0);
        return new TestGame(players, characters: characters);
    }

    // ----- The Mogul -----

    [Fact]
    public void Mogul_WinsATieAgainstABidTenDollarsHigher()
    {
        var game = With("mogul", seat: 0);
        game.RollOnto(0, "market-street");
        game.Do(0, new PlaceBid(100));
        game.Do(1, new PlaceBid(110)); // 100 + the Mogul's 10 (decision #153) ties it, and the Mogul wins ties
        Assert.Equal(0, game.Property("market-street").OwnerId);
        Assert.Contains(game.State.Log, e => e is BidEdgeWon { PlayerId: 0, Bid: 100, TopBid: 110 });
    }

    [Fact]
    public void Mogul_StillLosesToABidMoreThanTenHigher()
    {
        var game = With("mogul", seat: 0);
        game.RollOnto(0, "market-street");
        game.Do(0, new PlaceBid(100));
        game.Do(1, new PlaceBid(120));
        Assert.Equal(1, game.Property("market-street").OwnerId);
    }

    // ----- The Hustler -----

    private static TestGame HustlerAtTheCasino()
    {
        var game = With("hustler", seat: 0);
        game.Player(0).Momentum = 0;
        game.PlaceBefore(0, "casino", 5);
        game.Roll(0, 1, 4);
        return game;
    }

    [Fact]
    public void Hustler_HasNoCasinoReroll_ALosingBetJustLoses()
    {
        // Decision #153: a reroll made Casino bets win money on average, so the Hustler lost it.
        var game = HustlerAtTheCasino();
        game.Dice.Add(1, 2);
        game.Do(0, new BetHighLow(300, High: true));
        Assert.Equal(1200, game.Player(0).Cash);
        Assert.DoesNotContain(game.State.Log, e => e is CasinoRerolled);
    }

    [Fact]
    public void Hustler_LosingADiceDuel_IsNotOfferedAReroll()
    {
        var game = With("hustler", seat: 1);
        game.GiveCard(0, "dice-duel");
        game.Do(0, new PlayCard("dice-duel", TargetPlayerId: 1, Number: 100));
        game.Dice.Add(6, 5, 1, 2); // challenger 11, Hustler 3
        game.Do(1, new AcceptDuel());
        Assert.Equal(1400, game.Player(1).Cash);
        Assert.Equal(1600, game.Player(0).Cash);
    }

    [Fact]
    public void Mogul_WinsATieInADiceDuel_InsteadOfRollingAgain()
    {
        var game = With("mogul", seat: 1);
        game.GiveCard(0, "dice-duel");
        game.Do(0, new PlayCard("dice-duel", TargetPlayerId: 1, Number: 100));
        game.Dice.Add(3, 4, 5, 2); // 7 against 7: a tie
        game.Do(1, new AcceptDuel());

        Assert.Equal(1600, game.Player(1).Cash);
        Assert.Equal(1400, game.Player(0).Cash);
        Assert.Contains(game.State.Log, e => e is DiceTieWon { PlayerId: 1 });
        Assert.Single(game.State.Log.OfType<DuelRolled>());
    }

    [Fact]
    public void Mogul_ChallengingAndTying_AlsoWins()
    {
        var game = With("mogul", seat: 0);
        game.GiveCard(0, "dice-duel");
        game.Do(0, new PlayCard("dice-duel", TargetPlayerId: 1, Number: 100));
        game.Dice.Add(6, 1, 3, 4);
        game.Do(1, new AcceptDuel());
        Assert.Equal(1600, game.Player(0).Cash);
    }

    [Fact]
    public void Mogul_DoesNotWinADiceDuel_TheyActuallyLose()
    {
        var game = With("mogul", seat: 1);
        game.GiveCard(0, "dice-duel");
        game.Do(0, new PlayCard("dice-duel", TargetPlayerId: 1, Number: 100));
        game.Dice.Add(6, 6, 1, 2);
        game.Do(1, new AcceptDuel());
        Assert.Equal(1400, game.Player(1).Cash);
        Assert.DoesNotContain(game.State.Log, e => e is DiceTieWon);
    }

    // ----- The Commuter -----

    [Fact]
    public void Commuter_StartsWithThreeTokens_AndCanHoldFour()
    {
        var game = With("commuter");
        Assert.Equal(3, game.Player(0).Momentum);
        Assert.Equal(4, game.Engine.GetView(0).Me.MomentumCap);
        Assert.Equal(2, game.Player(1).Momentum);
        Assert.Equal(2, game.Engine.GetView(1).Me.MomentumCap);
    }

    // ----- The Lawyer -----

    [Fact]
    public void Lawyer_LeavesJailForFree()
    {
        var game = With("lawyer", seat: 1);
        game.JailThenStartTheirTurn(1);
        Assert.Equal(0, game.Engine.GetView(1).Me.Bail);
        game.Do(1, new PayBail());
        Assert.Equal(1500, game.Player(1).Cash);
        Assert.False(game.Player(1).InJail);
    }

    [Fact]
    public void Lawyer_CannotBeAudited()
    {
        var game = With("lawyer", seat: 1);
        game.GiveCard(0, "audit");
        Assert.DoesNotContain(game.Engine.GetLegalActions(0), a => a is PlayCard { CardId: "audit" });
    }

    [Fact]
    public void Lawyer_CanStillBeTargetedByOtherCards()
    {
        var game = With("lawyer", seat: 1);
        game.GiveCard(0, "dice-duel");
        Assert.Contains(game.Engine.GetLegalActions(0), a => a is PlayCard { CardId: "dice-duel", TargetPlayerId: 1 });
    }

    // ----- The Contractor -----

    [Fact]
    public void Contractor_BuildsAQuarterCheaper()
    {
        var game = With("contractor");
        game.Give(0, "mill-street", "cobble-lane");
        Assert.Equal(35, game.Engine.GetView(0).Properties["mill-street"].NextLevelCost); // $50 less 25% is $37.50, rounded down to $35
        game.Do(0, new Build("mill-street"));
        Assert.Equal(1500 - 35, game.Player(0).Cash);
    }

    // ----- The Politician -----

    [Fact]
    public void Politician_HasABiggerVote()
    {
        var game = With("politician");
        Assert.Equal(250, RuleModifiers.VoteInfluence(game.State, game.Engine.Content, 0));
        Assert.Equal(100, RuleModifiers.VoteInfluence(game.State, game.Engine.Content, 1));
    }

    // ----- The City Planner -----

    [Fact]
    public void CityPlanner_PeeksAtOneHiddenTileBeforeTheGameStarts()
    {
        var game = With("city-planner");
        var ask = game.DecisionFor(0)!;
        Assert.Equal(DecisionKind.PeekTile, ask.Kind);

        var tile = game.State.Board.First(s => s.Kind == SpaceKind.GovernmentTile && !s.IsRevealed);
        Assert.Contains(new PeekAtTile(tile.Id), game.Engine.GetLegalActions(0));
        game.Do(0, new PeekAtTile(tile.Id));
        Assert.Equal(DecisionKind.SwapTile, game.DecisionFor(0)!.Kind); // decision #158
        game.Do(0, new NoThanks());

        Assert.Equal(tile.TileId, game.Engine.GetView(0).Spaces.First(s => s.Id == tile.Id).TileId);
        Assert.Null(game.Engine.GetView(1).Spaces.First(s => s.Id == tile.Id).TileId); // secret from everyone else
        Assert.Null(game.Engine.GetView(-1).Spaces.First(s => s.Id == tile.Id).TileId);
        Assert.Equal(DecisionKind.ChooseMoveMethod, game.DecisionFor(0)!.Kind);
    }

    [Fact]
    public void CityPlanner_PeeksAgainEveryTimeTheyPassGo()
    {
        var game = With("city-planner");
        var first = game.State.Board.First(s => s.Kind == SpaceKind.GovernmentTile && !s.IsRevealed);
        game.Do(0, new PeekAtTile(first.Id));
        game.Do(0, new NoThanks());

        game.PlaceBefore(0, "go", 5);
        game.Roll(0, 1, 4);
        var ask = game.DecisionFor(0)!;
        Assert.Equal(DecisionKind.PeekTile, ask.Kind);
        Assert.DoesNotContain(game.Engine.GetLegalActions(0), a => a is PeekAtTile p && p.SpaceId == first.Id);
    }

    [Fact]
    public void NoOneElse_GetsPeeks()
    {
        var game = With("mogul");
        Assert.NotEqual(DecisionKind.PeekTile, game.DecisionFor(0)!.Kind);
    }

    // ----- The Medic -----

    [Fact]
    public void Medic_IsNeverBilledByTheHospital()
    {
        var game = With("medic");
        var ctx = new EngineContext(game.State, game.Engine.Content, game.Dice);
        int stackBefore = game.State.Stack.Count;

        Movement.SendToHospital(ctx, game.Player(0), "a test");

        Assert.Equal(game.State.IndexOf("hospital"), game.Player(0).Position);
        Assert.False(game.Player(0).IsRecovering);
        Assert.Equal(stackBefore, game.State.Stack.Count);
        Assert.Contains(game.State.Log, e => e is HospitalShrugged { PlayerId: 0 });
    }

    [Fact]
    public void Medic_InsuredMarkersAreFree()
    {
        var game = With("medic");
        game.Player(0).Momentum = 0;
        game.RollOnto(0, "hospital");
        var ask = game.DecisionFor(0)!;
        Assert.Equal(DecisionKind.HospitalVisit, ask.Kind);
        Assert.Equal(0, ask.AmountOwed);
        Assert.Equal(0, game.Engine.GetView(0).Me.InsuredMarkerCost);
        game.Do(0, new BuyInsurance());
        Assert.Equal(1500, game.Player(0).Cash);
        Assert.True(game.Player(0).IsInsured);
    }

    [Fact]
    public void OtherPlayers_StillPayFullPriceForInsuredMarkers()
    {
        var game = With("medic", seat: 1);
        game.Player(0).Momentum = 0;
        game.RollOnto(0, "hospital");
        game.Do(0, new BuyInsurance());
        Assert.Equal(1500 - 50, game.Player(0).Cash);
    }

    // ----- Picking characters (Section 5) -----

    private static GameEngine PickGame(int players, CharacterMode mode)
    {
        var setup = new GameSetup
        {
            Players = Enumerable.Range(0, players).Select(i => new PlayerSetup(((char)('A' + i)).ToString())).ToList(),
            Seed = 99,
            ShuffleTurnOrder = false,
            Characters = mode,
        };
        return GameEngine.NewGame(setup, dice: new CityTycoon.Engine.Randomness.ScriptedDice());
    }

    [Fact]
    public void Pick_TheLastPlayerPicksFirst_AndTheFirstPlayerPicksLast()
    {
        var engine = PickGame(3, CharacterMode.Pick);
        Assert.Equal([2], engine.GetPendingDecisions().Select(d => d.PlayerId));
        Assert.Equal(DecisionKind.ChooseCharacter, engine.GetPendingDecisions()[0].Kind);

        Assert.True(engine.Submit(2, new PickCharacter("mogul")).Accepted);
        Assert.Equal([1], engine.GetPendingDecisions().Select(d => d.PlayerId));
        Assert.True(engine.Submit(1, new PickCharacter("medic")).Accepted);
        Assert.Equal([0], engine.GetPendingDecisions().Select(d => d.PlayerId));
        Assert.True(engine.Submit(0, new PickCharacter("lawyer")).Accepted);

        Assert.Equal("lawyer", engine.State.Player(0).CharacterId);
        Assert.Equal("medic", engine.State.Player(1).CharacterId);
        Assert.Equal("mogul", engine.State.Player(2).CharacterId);
        Assert.Equal(3, engine.State.Log.OfType<CharacterChosen>().Count());
        // Then the game starts with player 0's turn.
        Assert.Equal(DecisionKind.ChooseMoveMethod, engine.GetPendingDecisions()[0].Kind);
        Assert.Equal(0, engine.GetPendingDecisions()[0].PlayerId);
    }

    [Fact]
    public void Pick_ACharacterThatIsTaken_CannotBePickedAgain()
    {
        var engine = PickGame(2, CharacterMode.Pick);
        engine.Submit(1, new PickCharacter("mogul"));
        Assert.DoesNotContain(new PickCharacter("mogul"), engine.GetLegalActions(0));
        Assert.False(engine.Submit(0, new PickCharacter("mogul")).Accepted);
        Assert.Equal(7, engine.GetLegalActions(0).Count(a => a is PickCharacter));
    }

    [Fact]
    public void Pick_TheCityPlannerPeeksOnlyAfterEveryoneHasPicked()
    {
        var engine = PickGame(2, CharacterMode.Pick);
        engine.Submit(1, new PickCharacter("city-planner"));
        Assert.Equal(DecisionKind.ChooseCharacter, engine.GetPendingDecisions()[0].Kind);
        engine.Submit(0, new PickCharacter("mogul"));
        Assert.Equal(DecisionKind.PeekTile, engine.GetPendingDecisions()[0].Kind);
        Assert.Equal(1, engine.GetPendingDecisions()[0].PlayerId);
    }

    [Fact]
    public void Pick_TheCommuterStartsWithThreeTokens()
    {
        var engine = PickGame(2, CharacterMode.Pick);
        engine.Submit(1, new PickCharacter("commuter"));
        engine.Submit(0, new PickCharacter("mogul"));
        Assert.Equal(3, engine.State.Player(1).Momentum);
        Assert.Equal(2, engine.State.Player(0).Momentum);
    }

    [Fact]
    public void Random_EveryoneGetsADifferentCharacter_AndTheSameSeedDealsTheSame()
    {
        var a = PickGame(6, CharacterMode.Random);
        var b = PickGame(6, CharacterMode.Random);
        var dealt = a.State.Players.Select(p => p.CharacterId).ToList();
        Assert.All(dealt, id => Assert.NotNull(id));
        Assert.Equal(6, dealt.Distinct().Count());
        Assert.Equal(dealt, b.State.Players.Select(p => p.CharacterId));
    }

    [Fact]
    public void NoCharacters_MeansPlainRules()
    {
        var engine = PickGame(2, CharacterMode.None);
        Assert.All(engine.State.Players, p => Assert.Null(p.CharacterId));
        Assert.Equal(DecisionKind.ChooseMoveMethod, engine.GetPendingDecisions()[0].Kind);
    }

    [Fact]
    public void PresetCharacters_AreKept_AndTheRestArePickedLater()
    {
        var setup = new GameSetup
        {
            Players = [new PlayerSetup("A", "lawyer"), new PlayerSetup("B"), new PlayerSetup("C")],
            Seed = 5,
            ShuffleTurnOrder = false,
            Characters = CharacterMode.Pick,
        };
        var engine = GameEngine.NewGame(setup);
        Assert.Equal("lawyer", engine.State.Player(0).CharacterId);
        Assert.Equal([2], engine.GetPendingDecisions().Select(d => d.PlayerId));
        Assert.DoesNotContain(new PickCharacter("lawyer"), engine.GetLegalActions(2));
    }

    [Fact]
    public void PresetCharacters_MustBeRealAndDifferent()
    {
        GameSetup Make(string a, string b) => new() { Players = [new PlayerSetup("A", a), new PlayerSetup("B", b)] };
        Assert.Throws<ArgumentException>(() => GameEngine.NewGame(Make("mogul", "mogul")));
        Assert.ThrowsAny<Exception>(() => GameEngine.NewGame(Make("wizard", "mogul")));
    }

    [Fact]
    public void EveryCharacter_HasAtLeastOneRuleChange()
    {
        var content = GameContent.Default;
        Assert.True(content.Characters.Count >= content.Rules.MaxPlayers);
        Assert.All(content.Characters, c => Assert.NotEmpty(c.Modifiers!));
    }
}
