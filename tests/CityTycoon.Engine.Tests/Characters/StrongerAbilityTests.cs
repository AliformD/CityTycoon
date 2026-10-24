using CityTycoon.Engine.Actions;
using CityTycoon.Engine.Content;
using CityTycoon.Engine.Events;
using CityTycoon.Engine.Tests.Helpers;

namespace CityTycoon.Engine.Tests.Characters;

/// <summary>Decision #152: the stronger character abilities (Hustler, Lawyer, Politician and City Planner).</summary>
public class StrongerAbilityTests
{
    // ----- The Hustler: a movement reroll once per lap -----

    private static TestGame Hustler()
    {
        var game = new TestGame(2, characters: ["hustler", "lawyer"]);
        game.Player(0).Momentum = 0;
        return game;
    }

    [Fact]
    public void Hustler_IsAskedAfterRolling_AndCanRollAgain()
    {
        var game = Hustler();
        game.PlaceBefore(0, "jail", 5);
        game.Dice.Add(3, 4);
        game.Do(0, new RollDice());

        var ask = game.DecisionFor(0)!;
        Assert.Equal(DecisionKind.RerollMove, ask.Kind);
        Assert.Equal([3, 4], ask.Dice!);

        Assert.Contains(new RerollDice(0), game.Engine.GetLegalActions(0));
        Assert.Contains(new RerollDice(1), game.Engine.GetLegalActions(0));

        // Only one die is rolled again (decision #153): the 4 becomes a 2, and the 3 stays.
        game.Dice.Add(2);
        game.Do(0, new RerollDice(1));
        Assert.Equal(game.State.IndexOf("jail"), game.Player(0).Position); // moved 3 + 2 = 5, not 7
        Assert.Contains(game.State.Log, e => e is MoveRerolled { PlayerId: 0, Die1: 3, Die2: 4, Die: 1 });
        Assert.False(game.Engine.GetView(0).Me.MoveRerollReady);
    }

    [Fact]
    public void Hustler_CanKeepTheRoll_AndStillHasTheReroll()
    {
        var game = Hustler();
        game.PlaceBefore(0, "jail", 7);
        game.Dice.Add(3, 4);
        game.Do(0, new RollDice());
        game.Do(0, new NoThanks());
        Assert.Equal(game.State.IndexOf("jail"), game.Player(0).Position);
        Assert.True(game.Engine.GetView(0).Me.MoveRerollReady);
    }

    [Fact]
    public void Hustler_GetsOneRerollPerLap_UntilPassingGo()
    {
        var game = Hustler();
        game.PlaceBefore(0, "jail", 5);
        game.Dice.Add(3, 4, 2);
        game.Do(0, new RollDice());
        game.Do(0, new RerollDice(1)); // 3 + 2
        game.EndTurn(0);
        game.SkipTurn(1);

        // Not asked again this lap.
        game.PlaceBefore(0, "jail", 3);
        game.Dice.Add(1, 2);
        game.Do(0, new RollDice());
        Assert.NotEqual(DecisionKind.RerollMove, game.DecisionFor(0)?.Kind);
        game.EndTurn(0);
        game.SkipTurn(1);

        // Passing GO makes it ready again.
        game.PlaceBefore(0, "go", 2);
        game.Roll(0, 1, 2);
        Assert.True(game.Engine.GetView(0).Me.MoveRerollReady);
    }

    [Fact]
    public void Hustler_RerolledDoubles_DontCount()
    {
        var game = Hustler();
        game.PlaceBefore(0, "jail", 5);
        game.Dice.Add(3, 3, 2);
        game.Do(0, new RollDice());
        // With doubles both dice are the same, so there is only one die to choose.
        Assert.DoesNotContain(new RerollDice(1), game.Engine.GetLegalActions(0));
        game.Do(0, new RerollDice(0));
        // Only the new roll counts: 2 + 3 is not doubles, so no extra turn.
        Assert.Equal(game.State.IndexOf("jail"), game.Player(0).Position);
        Assert.Equal(DecisionKind.PostMoveActions, game.DecisionFor(0)!.Kind);
    }

    [Fact]
    public void Hustler_IsNotAskedAfterAPrecisionRoll()
    {
        var game = Hustler();
        game.Player(0).Momentum = 2;
        game.PlaceBefore(0, "jail", 9);
        game.Dice.Add(2, 6, 3);
        game.Do(0, new PrecisionRoll());
        game.Do(0, new ChooseDice(1, 2));
        Assert.Equal(game.State.IndexOf("jail"), game.Player(0).Position);
        Assert.DoesNotContain(game.State.Log, e => e is MoveRerolled);
    }

    [Fact]
    public void OtherCharacters_AreNeverAskedToReroll()
    {
        var game = new TestGame();
        game.Player(0).Momentum = 0;
        game.PlaceBefore(0, "jail", 7);
        game.Dice.Add(3, 4);
        game.Do(0, new RollDice());
        Assert.Equal(DecisionKind.PostMoveActions, game.DecisionFor(0)!.Kind);
    }

    // ----- The Hustler: the Stadium triples rent -----

    [Fact]
    public void Hustler_StadiumPick_TriplesTheRent()
    {
        var game = new TestGame(3, characters: ["hustler", "lawyer", "medic"]);
        game.Give(2, "market-street");
        game.RollOnto(0, "stadium");
        Assert.Contains("triples", game.DecisionFor(0)!.Prompt);
        game.Do(0, new PickStadiumGroup("orange"));
        game.EndTurn(0);

        game.RollOnto(1, "market-street");
        Assert.Equal(1500 - 42, game.Player(1).Cash); // $14 tripled
    }

    // ----- The Lawyer: half the GO salary on the way to Jail -----

    [Fact]
    public void Lawyer_CollectsHalfTheGoSalary_WhenSentToJail()
    {
        var game = new TestGame(2, characters: ["lawyer", "medic"]);
        game.Player(0).Momentum = 0;
        game.PlaceBefore(0, "go-to-jail", 5);
        game.Roll(0, 1, 4);
        Assert.True(game.Player(0).InJail);
        Assert.Equal(1500 + 100, game.Player(0).Cash); // half of the $200 test salary
        Assert.Contains(game.State.Log, e => e is PerkIncome { PlayerId: 0, Amount: 100, Perk: "The Lawyer" });
    }

    [Fact]
    public void OtherPlayers_GetNothing_WhenSentToJail()
    {
        var game = new TestGame(2, characters: ["medic", "lawyer"]);
        game.Player(0).Momentum = 0;
        game.PlaceBefore(0, "go-to-jail", 5);
        game.Roll(0, 1, 4);
        Assert.Equal(1500, game.Player(0).Cash);
    }

    // ----- The Politician: lobbying buys twice the influence (decision #162) -----

    [Fact]
    public void Politician_LobbyingDollarsCountDouble()
    {
        var game = new TestGame(2, characters: ["politician", "lawyer"]);
        game.ConveneAndPropose("public-works");
        game.Dice.AddCouncilRoll(0);
        game.Vote(0, VoteChoice.Yes, lobby: 100);
        game.Vote(1, VoteChoice.No);
        CouncilResolved result = game.LastResolved();
        Assert.Equal(100 + 200 + 100, result.YesInfluence);
        Assert.Equal(100 + 100, result.NoInfluence);
        Assert.Equal(1400, game.Player(0).Cash); // still pays the real $100
    }

    // ----- The City Planner: reverse one Council result per game -----

    private static TestGame Planner()
    {
        var game = new TestGame(2, characters: ["city-planner", "lawyer"]);
        // The City Planner's first peek happens before anything else.
        game.Do(0, (PeekAtTile)game.Engine.GetLegalActions(0)[0]);
        game.Do(0, new NoThanks()); // keep the tile (decision #158)
        return game;
    }

    private static bool AnyHiddenTile(TestGame game) => game.State.Board.Any(s => s.Kind == SpaceKind.GovernmentTile && !s.IsRevealed);

    [Fact]
    public void CityPlanner_CanReverseAPassedOrdinance()
    {
        var game = Planner();
        game.ConveneAndPropose("public-works");
        game.EveryoneVotes(pass: true);

        var ask = game.DecisionFor(0)!;
        Assert.Equal(DecisionKind.ReverseCouncilVote, ask.Kind);
        Assert.True(ask.Passed);
        Assert.Null(game.DecisionFor(1));

        game.Do(0, new ReverseVote());
        Assert.True(AnyHiddenTile(game)); // Public Works did not happen
        Assert.Contains(game.State.Log, e => e is CouncilReversed { PlayerId: 0, NowPassed: false });
        Assert.False(game.Engine.GetView(0).Me.VoteReversalReady);
    }

    [Fact]
    public void CityPlanner_CanReverseAFailedOrdinance()
    {
        var game = Planner();
        game.ConveneAndPropose("public-works");
        game.EveryoneVotes(pass: false);
        Assert.False(game.DecisionFor(0)!.Passed);
        game.Do(0, new ReverseVote());
        Assert.False(AnyHiddenTile(game)); // it passes after all
    }

    [Fact]
    public void CityPlanner_CanLetTheResultStand_AndKeepTheReversal()
    {
        var game = Planner();
        game.ConveneAndPropose("public-works");
        game.EveryoneVotes(pass: true);
        game.Do(0, new NoThanks());
        Assert.False(AnyHiddenTile(game));
        Assert.True(game.Engine.GetView(0).Me.VoteReversalReady);
    }

    [Fact]
    public void WithoutACityPlanner_NobodyIsAsked()
    {
        var game = new TestGame(2, characters: ["politician", "lawyer"]);
        game.ConveneAndPropose("public-works");
        game.EveryoneVotes(pass: true);
        Assert.False(AnyHiddenTile(game));
        Assert.DoesNotContain(game.Engine.GetPendingDecisions(), d => d.Kind == DecisionKind.ReverseCouncilVote);
    }
}
