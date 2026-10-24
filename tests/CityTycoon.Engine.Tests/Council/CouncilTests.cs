using CityTycoon.Engine.Actions;
using CityTycoon.Engine.Content;
using CityTycoon.Engine.Events;
using CityTycoon.Engine.Ordinances;
using CityTycoon.Engine.State;
using CityTycoon.Engine.Steps;
using CityTycoon.Engine.Tests.Helpers;
using CityTycoon.Engine.Views;

namespace CityTycoon.Engine.Tests.Council;

/// <summary>Section 11: sessions, the agenda, secret votes, lobbying and the influence math.</summary>
public class CouncilTests
{
    // ---------------------------------------------------------- the influence math (Section 11.3)

    /// <summary>Player 0 is the Politician, the only one who may lobby (decision #162).</summary>
    private static TestGame WithPolitician() => new(3, characters: ["politician", "lawyer", "mogul"]);

    [Fact]
    public void ThePoliticiansLobbying_CountsTwice_AndEveryVoteIsEqual()
    {
        // The Politician votes Yes and lobbies $100. Ben votes No. Cal abstains.
        var game = WithPolitician();
        game.ConveneAndPropose("public-works");
        game.Dice.AddCouncilRoll(399);
        game.Vote(0, VoteChoice.Yes, lobby: 100);
        game.Vote(1, VoteChoice.No);
        game.Vote(2, VoteChoice.Abstain);

        CouncilResolved result = game.LastResolved();
        Assert.Equal(400, result.YesInfluence);   // 100 vote + 200 lobbying + 100 public opinion
        Assert.Equal(200, result.NoInfluence);    // 100 vote + 100 public opinion
        Assert.True(result.Passed);   // roll 399 is inside the Yes part (0-399)
    }

    [Fact]
    public void ARollJustOutsideTheYesPart_Fails()
    {
        var game = WithPolitician();
        game.ConveneAndPropose("public-works");
        game.Dice.AddCouncilRoll(400);
        game.Vote(0, VoteChoice.Yes, lobby: 100);
        game.Vote(1, VoteChoice.No);
        game.Vote(2, VoteChoice.Abstain);
        Assert.False(game.LastResolved().Passed);
    }

    [Fact]
    public void AllLobbyingMoneyGoesToTheBank_WhetherItPassesOrFails()
    {
        foreach (bool pass in new[] { true, false })
        {
            var game = WithPolitician();
            game.ConveneAndPropose("public-works");
            game.Dice.AddCouncilRoll(pass ? 0 : 599);
            game.Vote(0, VoteChoice.Yes, lobby: 300);
            game.Vote(1, VoteChoice.No);
            game.Vote(2, VoteChoice.Abstain);
            Assert.Equal(1200, game.Player(0).Cash);
            Assert.Equal(1500, game.Player(1).Cash);
        }
    }

    [Fact]
    public void OnlyThePolitician_MayLobby()
    {
        var game = WithPolitician();
        game.ConveneAndPropose("public-works");
        Assert.Equal(0, game.DecisionFor(1)!.MaxLobby);
        Assert.False(game.Engine.Submit(1, new CastVote(VoteChoice.No, 50)).Accepted);
        Assert.True(game.Engine.Submit(1, new CastVote(VoteChoice.No)).Accepted);
        Assert.Equal(1500, game.DecisionFor(0)!.MaxLobby);
    }

    [Fact]
    public void WithoutAPolitician_NobodyMayLobby()
    {
        var game = new TestGame(2);
        game.ConveneAndPropose("public-works");
        Assert.False(game.Engine.Submit(0, new CastVote(VoteChoice.Yes, 50)).Accepted);
        Assert.True(game.Engine.Submit(0, new CastVote(VoteChoice.Yes)).Accepted);
    }

    [Fact]
    public void PublicOpinion_AlwaysAdds100ToEachSide_SoNothingIsCertain()
    {
        var game = new TestGame(2);
        game.ConveneAndPropose("public-works");
        game.Dice.AddCouncilRoll(0);
        game.Vote(0, VoteChoice.Yes);
        game.Vote(1, VoteChoice.Yes);
        CouncilResolved result = game.LastResolved();
        Assert.Equal(300, result.YesInfluence);
        Assert.Equal(100, result.NoInfluence);
    }

    [Fact]
    public void ThePoliticiansVote_IsWorthTheSameAsEveryoneElses()
    {
        var game = new TestGame(2, characters: ["politician", "lawyer"]);
        game.ConveneAndPropose("public-works");
        game.Dice.AddCouncilRoll(0);
        game.Vote(0, VoteChoice.Yes);
        game.Vote(1, VoteChoice.No);
        CouncilResolved result = game.LastResolved();
        Assert.Equal(200, result.YesInfluence);   // 100 + 100 public opinion
        Assert.Equal(200, result.NoInfluence);    // 100 + 100 public opinion
    }

    [Fact]
    public void TheLobbyist_Adds200FreeInfluence_AndIsUsedUp()
    {
        var game = new TestGame(2);
        game.GiveCard(0, "lobbyist");
        game.ConveneAndPropose("public-works");
        Assert.True(game.DecisionFor(0)!.HasLobbyist);
        game.Dice.AddCouncilRoll(0);
        game.Vote(0, VoteChoice.Yes, lobbyist: true);
        game.Vote(1, VoteChoice.No);
        CouncilResolved result = game.LastResolved();
        Assert.Equal(400, result.YesInfluence);   // 100 vote + 200 Lobbyist + 100 public opinion
        Assert.DoesNotContain("lobbyist", game.Player(0).Hand);
        Assert.Equal("lobbyist", game.State.OpportunityDeck[^1]);
        Assert.True(result.Votes.First(v => v.PlayerId == 0).UsedLobbyist);
    }

    [Fact]
    public void AnAbstainer_CannotLobbyOrPlayTheLobbyist()
    {
        var game = new TestGame(2);
        game.GiveCard(0, "lobbyist");
        game.ConveneAndPropose("public-works");
        Assert.False(game.Engine.Submit(0, new CastVote(VoteChoice.Abstain, 50)).Accepted);
        Assert.False(game.Engine.Submit(0, new CastVote(VoteChoice.Abstain, 0, true)).Accepted);
        Assert.True(game.Engine.Submit(0, new CastVote(VoteChoice.Abstain)).Accepted);
    }

    [Fact]
    public void YouCannotLobbyMoreThanYourCash_OrPlayALobbyistYouDontHold()
    {
        var game = new TestGame(2, characters: ["politician", "lawyer"]);
        game.ConveneAndPropose("public-works");
        Assert.False(game.Engine.Submit(0, new CastVote(VoteChoice.Yes, 1501)).Accepted);
        Assert.False(game.Engine.Submit(0, new CastVote(VoteChoice.Yes, -5)).Accepted);
        Assert.False(game.Engine.Submit(0, new CastVote(VoteChoice.Yes, 0, true)).Accepted);
        Assert.True(game.Engine.Submit(0, new CastVote(VoteChoice.Yes, 1500)).Accepted);
    }

    [Fact]
    public void VotesAreSecret_UntilEveryoneHasVoted()
    {
        var game = WithPolitician();
        game.ConveneAndPropose("public-works");
        game.Vote(0, VoteChoice.Yes, 100);
        game.Vote(1, VoteChoice.No);
        Assert.DoesNotContain(game.State.Log, e => e is CouncilResolved);
        Assert.NotNull(game.DecisionFor(2));
        Assert.Equal(1500, game.Player(0).Cash);   // the money is only taken at the reveal
    }

    [Fact]
    public void EveryoneWhoIsntBankruptVotes_IncludingPlayersInJail()
    {
        var game = new TestGame(3);
        game.Player(1).InJail = true;
        game.Player(2).IsBankrupt = true;
        game.ConveneAndPropose("public-works");
        var voters = game.Engine.GetPendingDecisions().Where(d => d.Kind == DecisionKind.CouncilVote).Select(d => d.PlayerId).Order().ToList();
        Assert.Equal([0, 1], voters);
    }

    [Fact]
    public void ThePassedOrFailedCard_GoesToTheDiscardPile()
    {
        var game = new TestGame(2);
        game.ConveneAndPropose("public-works");
        game.EveryoneVotes(pass: false);
        Assert.Contains("public-works", game.State.OrdinanceDiscard);
    }

    // ---------------------------------------------------------- the agenda (Sections 11.1 and 11.2)

    [Fact]
    public void TheOrdinanceNotPicked_GoesToTheBottomOfTheDeck()
    {
        var game = new TestGame(2);
        game.Convene("public-works", other: "stimulus-check");
        game.Do(0, new ChooseOrdinance("public-works"));
        Assert.Equal("stimulus-check", game.State.OrdinanceDeck[^1]);
    }

    [Fact]
    public void AnOrdinanceThatCantDoAnything_CantBePicked()
    {
        var game = new TestGame(2);
        game.AllResidential();
        foreach (string d in game.State.Zoning.Keys.ToList())
            game.State.Zoning[d] = ZoningLevel.HighRise;
        game.Convene("rezone-up", other: "stimulus-check");

        // Only the Stimulus Check could be picked, so it goes straight to the vote.
        Assert.Contains(game.State.Log, e => e is OrdinanceProposed { OrdinanceId: "stimulus-check" });
        Assert.Equal(DecisionKind.CouncilVote, game.DecisionFor(0)!.Kind);
        Assert.Equal("rezone-up", game.State.OrdinanceDeck[^1]);
    }

    [Fact]
    public void IfNeitherCardCanBePicked_TheSetterDrawsAgain()
    {
        var game = new TestGame(2);
        foreach (string d in game.State.Zoning.Keys.ToList())
            game.State.Zoning[d] = ZoningLevel.HighRise;
        game.State.OrdinanceDeck.InsertRange(0, ["rezone-up", "rezone-up", "stimulus-check", "public-works"]);
        game.DrawEvent(0, "emergency-session");
        Assert.Equal(DecisionKind.ChooseOrdinance, game.DecisionFor(0)!.Kind);
        Assert.Equal(["stimulus-check", "public-works"], game.DecisionFor(0)!.Ordinances);
    }

    [Fact]
    public void ADistrictOrdinance_AsksTheSetterForADistrict_ThatCanTakeIt()
    {
        var game = new TestGame(2);
        game.AllResidential();
        game.State.Zoning["old-town"] = ZoningLevel.HighRise;
        game.Convene("rezone-up");
        game.Do(0, new ChooseOrdinance("rezone-up"));
        var decision = game.DecisionFor(0)!;
        Assert.Equal(DecisionKind.ChooseDistrict, decision.Kind);
        Assert.DoesNotContain("old-town", decision.Districts!);
        Assert.Equal(3, decision.Districts!.Count);
        Assert.False(game.Engine.Submit(0, new ChooseDistrict("old-town")).Accepted);
    }

    [Fact]
    public void WhenOnlyOneDistrictFits_ItIsPickedForTheSetter()
    {
        var game = new TestGame(2);
        foreach (string d in game.State.Zoning.Keys.ToList())
            game.State.Zoning[d] = ZoningLevel.HighRise;
        game.State.Zoning["harbor"] = ZoningLevel.Residential;
        game.Convene("rezone-up");
        game.Do(0, new ChooseOrdinance("rezone-up"));
        Assert.Contains(game.State.Log, e => e is OrdinanceProposed { OrdinanceId: "rezone-up", DistrictId: "harbor" });
        Assert.Equal(DecisionKind.CouncilVote, game.DecisionFor(0)!.Kind);
    }

    [Fact]
    public void ARegularSession_HappensAtTheEndOfRound4_AndTheLowestNetWorthSetsTheAgenda()
    {
        var game = new TestGame(3);
        game.Player(2).Cash = 1000;
        for (int round = 1; round <= 4; round++)
        {
            Assert.DoesNotContain(game.State.Log, e => e is CouncilStarted);
            for (int id = 0; id < 3; id++)
                game.SkipTurn(id);
        }
        Assert.Contains(game.State.Log, e => e is CouncilStarted { Emergency: false, SetterId: 2 });
        Assert.Equal(DecisionKind.ChooseOrdinance, game.DecisionFor(2)!.Kind);
        Assert.Equal(4, game.State.Round);   // the round only moves on once the session is over
    }

    [Fact]
    public void ANetWorthTie_GoesToTheNextPlayerAfterTheCurrentOne()
    {
        var game = new TestGame(3);
        for (int round = 1; round <= 4; round++)
            for (int id = 0; id < 3; id++)
                game.SkipTurn(id);
        // Everyone is level, and player 2 just finished the round, so player 0 is next in turn order.
        Assert.Contains(game.State.Log, e => e is CouncilStarted { Emergency: false, SetterId: 0 });
    }

    [Fact]
    public void AfterTheSession_TheNextRoundStarts()
    {
        var game = new TestGame(2);
        for (int round = 1; round <= 4; round++)
            for (int id = 0; id < 2; id++)
                game.SkipTurn(id);
        game.Do(game.Engine.GetPendingDecisions().First().PlayerId, new ChooseOrdinance(game.Engine.GetPendingDecisions().First().Ordinances![0]));
        string? district = game.Engine.GetPendingDecisions().FirstOrDefault(d => d.Kind == DecisionKind.ChooseDistrict)?.Districts?[0];
        if (district != null)
            game.Do(game.Engine.GetPendingDecisions().First().PlayerId, new ChooseDistrict(district));
        game.EveryoneVotes(pass: false);
        Assert.Equal(5, game.State.Round);
        Assert.Equal(DecisionKind.ChooseMoveMethod, game.DecisionFor(0)!.Kind);
    }

    [Fact]
    public void TheLastRoundOfATimedGame_HasNoSession()
    {
        var game = new TestGame(2, maxRounds: 4);
        for (int round = 1; round <= 4; round++)
            for (int id = 0; id < 2; id++)
                game.SkipTurn(id);
        Assert.DoesNotContain(game.State.Log, e => e is CouncilStarted);
        Assert.True(game.State.IsOver);
    }

    [Fact]
    public void AnEmptyDeck_CancelsTheSession_AndTheGameCarriesOn()
    {
        var game = new TestGame(2);
        game.State.OrdinanceDeck.Clear();
        game.State.OrdinanceDiscard.Clear();
        game.DrawEvent(0, "emergency-session");
        Assert.Contains(game.State.Log, e => e is CouncilCancelled);
        Assert.Equal(DecisionKind.PostMoveActions, game.DecisionFor(0)!.Kind);
    }

    [Fact]
    public void WhenTheDeckRunsLow_TheDiscardPileIsShuffledUnderneath()
    {
        var game = new TestGame(2);
        game.State.OrdinanceDeck.Clear();
        game.State.OrdinanceDeck.Add("stimulus-check");
        game.State.OrdinanceDiscard.AddRange(["public-works", "rezone-up"]);
        game.DrawEvent(0, "emergency-session");
        Assert.Equal(2, game.DecisionFor(0)!.Ordinances!.Count);
        Assert.Empty(game.State.OrdinanceDiscard);
    }

    [Fact]
    public void ACityHallVisit_CallsAnEmergencySession_AfterThePaymentIsSettled()
    {
        var game = new TestGame(2);
        game.SetTile("city-hall");
        game.RollOnto(0, "old-town-government");
        Assert.Contains(game.State.Log, e => e is CouncilStarted { Emergency: true, SetterId: 0 });
    }

    [Fact]
    public void ASessionSurvivesBeingSavedAndLoaded()
    {
        var game = new TestGame(2, characters: ["politician", "lawyer"]);
        game.ConveneAndPropose("stimulus-check");
        game.Vote(0, VoteChoice.Yes, 50);

        string json = GameStateSerializer.ToJson(game.State);
        GameState loaded = GameStateSerializer.FromJson(json);
        var dice = new Engine.Randomness.ScriptedDice();
        dice.AddCouncilRoll(0);
        var engine = new GameEngine(loaded, GameContent.Default, dice);

        Assert.True(engine.Submit(1, new CastVote(VoteChoice.No)).Accepted);
        CouncilResolved result = loaded.Log.OfType<CouncilResolved>().Last();
        Assert.Equal(300, result.YesInfluence);   // 100 vote + 100 for the Politician's $50 lobbying + 100 public opinion
        Assert.True(result.Passed);
    }
}
