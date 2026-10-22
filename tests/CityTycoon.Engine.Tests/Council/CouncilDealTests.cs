using CityTycoon.Engine.Actions;
using CityTycoon.Engine.Events;
using CityTycoon.Engine.Tests.Helpers;

namespace CityTycoon.Engine.Tests.Council;

/// <summary>Vote deals (decision #159): before a vote, players may pay each other to vote a certain way.</summary>
public class CouncilDealTests
{
    private static TestGame NewGame(int players = 3) =>
        new(players, content: TestContent.With(new() { ["startingCash"] = 1500, ["goSalary"] = 200, ["casinoLandingFlip"] = 0, ["voteDeals"] = true }));

    private static void NoDeal(TestGame game, params int[] players)
    {
        foreach (int id in players)
            Assert.True(game.Do(id, new NoThanks()).Accepted);
    }

    [Fact]
    public void BeforeTheVote_EveryoneIsAskedForADeal_AndNoDealsMeansAPlainVote()
    {
        var game = NewGame();
        game.ConveneAndPropose("public-works");

        var asked = game.Engine.GetPendingDecisions();
        Assert.Equal([0, 1, 2], asked.Select(d => d.PlayerId).Order());
        Assert.All(asked, d => Assert.Equal(DecisionKind.OfferVoteDeal, d.Kind));

        NoDeal(game, 0, 1, 2);
        Assert.All(game.Engine.GetPendingDecisions(), d => Assert.Equal(DecisionKind.CouncilVote, d.Kind));
    }

    [Fact]
    public void AnAcceptedDeal_PaysAtOnce_AndHoldsThemToThatVote()
    {
        var game = NewGame();
        game.ConveneAndPropose("public-works");
        Assert.True(game.Do(0, new OfferVoteDeal(new VoteDeal(0, 1, 100, VoteChoice.Yes))).Accepted);
        NoDeal(game, 1, 2);

        var answer = game.DecisionFor(1)!;
        Assert.Equal(DecisionKind.AnswerVoteDeal, answer.Kind);
        Assert.Equal(new VoteDeal(0, 1, 100, VoteChoice.Yes), answer.Deal);
        Assert.True(game.Do(1, new AcceptVoteDeal()).Accepted);

        Assert.Equal(1400, game.Player(0).Cash);
        Assert.Equal(1600, game.Player(1).Cash);
        Assert.Single(game.State.Log.OfType<VoteDealMade>());

        var vote = game.DecisionFor(1)!;
        Assert.Equal(DecisionKind.CouncilVote, vote.Kind);
        Assert.NotNull(vote.Deal);
        Assert.False(game.Engine.Submit(1, new CastVote(VoteChoice.No)).Accepted);
        Assert.False(game.Engine.Submit(1, new CastVote(VoteChoice.Abstain)).Accepted);
        Assert.True(game.Vote(1, VoteChoice.Yes, lobby: 50).Accepted);
    }

    [Fact]
    public void ADeclinedDeal_MovesNoMoney_AndTheVoteIsFree()
    {
        var game = NewGame();
        game.ConveneAndPropose("public-works");
        game.Do(0, new OfferVoteDeal(new VoteDeal(0, 1, 100, VoteChoice.Yes)));
        NoDeal(game, 1, 2);
        Assert.True(game.Do(1, new DeclineVoteDeal()).Accepted);

        Assert.Equal(1500, game.Player(0).Cash);
        Assert.Equal(1500, game.Player(1).Cash);
        Assert.Single(game.State.Log.OfType<VoteDealDeclined>());
        Assert.Null(game.DecisionFor(1)!.Deal);
        Assert.True(game.Vote(1, VoteChoice.No).Accepted);
    }

    [Theory]
    [InlineData(0, 0, 100, VoteChoice.Yes)]       // to yourself
    [InlineData(0, 1, 5, VoteChoice.Yes)]         // under $10
    [InlineData(0, 1, 15, VoteChoice.Yes)]        // not a step of $10
    [InlineData(0, 1, 1600, VoteChoice.Yes)]      // more than you have
    [InlineData(0, 1, 100, VoteChoice.Abstain)]   // paying someone to abstain
    [InlineData(1, 2, 100, VoteChoice.Yes)]       // a deal in someone else's name
    [InlineData(0, 7, 100, VoteChoice.Yes)]       // nobody in that seat
    public void BadOffers_AreRefused(int from, int to, int amount, VoteChoice choice)
    {
        var game = NewGame();
        game.ConveneAndPropose("public-works");
        Assert.False(game.Engine.Submit(0, new OfferVoteDeal(new VoteDeal(from, to, amount, choice))).Accepted);
    }

    [Fact]
    public void TwoOffersToOnePlayer_TheBiggestIsAskedFirst_AndOnlyOneCanBeTaken()
    {
        var game = NewGame();
        game.ConveneAndPropose("public-works");
        game.Do(0, new OfferVoteDeal(new VoteDeal(0, 1, 50, VoteChoice.Yes)));
        game.Do(2, new OfferVoteDeal(new VoteDeal(2, 1, 80, VoteChoice.No)));
        NoDeal(game, 1);

        Assert.Equal(80, game.DecisionFor(1)!.Deal!.Amount);
        game.Do(1, new AcceptVoteDeal());

        // The $50 offer is dropped: player 1 is already held to a deal.
        Assert.All(game.Engine.GetPendingDecisions(), d => Assert.Equal(DecisionKind.CouncilVote, d.Kind));
        Assert.Equal(1500, game.Player(0).Cash);
        Assert.Equal(1420, game.Player(2).Cash);
    }

    [Fact]
    public void TheDealMoney_IsInTheLedger()
    {
        var game = NewGame();
        game.ConveneAndPropose("public-works");
        game.Do(0, new OfferVoteDeal(new VoteDeal(0, 1, 100, VoteChoice.Yes)));
        NoDeal(game, 1, 2);
        game.Do(1, new AcceptVoteDeal());

        var ledger = GameHistory.Ledger(game.Engine.GetView(0), game.Engine.Content);
        Assert.Contains(ledger, e => e.PlayerId == 0 && e.Amount == -100 && e.OtherPlayerId == 1);
        Assert.Contains(ledger, e => e.PlayerId == 1 && e.Amount == 100 && e.OtherPlayerId == 0);
    }
}
