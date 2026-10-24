using CityTycoon.Engine.Actions;
using CityTycoon.Engine.Events;
using CityTycoon.Engine.Tests.Helpers;

namespace CityTycoon.Engine.Tests.Council;

/// <summary>
/// Vote contracts (decision #162): every vote counts the same, and money only talks through the Politician.
/// Before a vote, the other players bid, like an open auction, to pay the Politician to vote their way.
/// </summary>
public class VoteContractTests
{
    /// <summary>Ana (seat 0) is the Politician; Ben (1) and Cal (2) may bid, in that order.</summary>
    private static TestGame NewGame(params string[] characters) =>
        new(3, characters: characters.Length > 0 ? characters : ["politician", "lawyer", "mogul"],
            content: TestContent.With(new() { ["startingCash"] = 1500, ["goSalary"] = 200, ["voteContracts"] = true }));

    private static void Pass(TestGame game, int playerId) => Assert.True(game.Do(playerId, new NoThanks()).Accepted);

    private static void Offer(TestGame game, int from, int amount, VoteChoice choice) =>
        Assert.True(game.Do(from, new OfferVoteDeal(new VoteDeal(from, 0, amount, choice))).Accepted);

    [Fact]
    public void WithoutAPolitician_ItIsAPlainVote()
    {
        var game = NewGame("lawyer", "mogul", "medic");
        game.ConveneAndPropose("public-works");
        Assert.All(game.Engine.GetPendingDecisions(), d => Assert.Equal(DecisionKind.CouncilVote, d.Kind));
    }

    [Fact]
    public void TheOtherPlayers_AreAskedOneAtATime_StartingAfterThePolitician()
    {
        var game = NewGame();
        game.ConveneAndPropose("public-works");

        var asked = Assert.Single(game.Engine.GetPendingDecisions());
        Assert.Equal((1, DecisionKind.OfferVoteDeal), (asked.PlayerId, asked.Kind));
        Assert.Equal(0, asked.OtherPlayerId);
        Assert.Equal(10, asked.MinBid);
        Pass(game, 1);
        Assert.Equal(2, Assert.Single(game.Engine.GetPendingDecisions()).PlayerId);
        Pass(game, 2);

        // Nobody bid: a plain vote.
        Assert.All(game.Engine.GetPendingDecisions(), d => Assert.Equal(DecisionKind.CouncilVote, d.Kind));
    }

    [Fact]
    public void ConflictingOffers_BecomeABid_AndTheHighestOfferWins()
    {
        var game = NewGame();
        game.ConveneAndPropose("public-works");
        Offer(game, 1, 50, VoteChoice.Yes);

        // Cal sees Ben's offer and must beat it by $10.
        var cal = game.DecisionFor(2)!;
        Assert.Equal(new VoteDeal(1, 0, 50, VoteChoice.Yes), cal.Deal);
        Assert.Equal(60, cal.MinBid);
        Assert.False(game.Engine.Submit(2, new OfferVoteDeal(new VoteDeal(2, 0, 50, VoteChoice.No))).Accepted);
        Offer(game, 2, 80, VoteChoice.No);

        // Back to Ben, who passes and is out. Cal holds the top offer, so the bidding is over.
        Assert.Equal(90, game.DecisionFor(1)!.MinBid);
        Pass(game, 1);

        var answer = game.DecisionFor(0)!;
        Assert.Equal(DecisionKind.AnswerVoteDeal, answer.Kind);
        Assert.Equal(new VoteDeal(2, 0, 80, VoteChoice.No), answer.Deal);
        Assert.Equal(2, game.State.Log.OfType<VoteContractBid>().Count());
        Assert.Equal(1500, game.Player(1).Cash);   // only the winner pays
    }

    [Fact]
    public void TheSectionExample_GivesYes200_No500()
    {
        // Ben offers $50 for Yes, Cal $80 for No, Ben passes. Ana takes Cal's $80, votes No and lobbies $20.
        var game = NewGame();
        game.ConveneAndPropose("public-works");
        Offer(game, 1, 50, VoteChoice.Yes);
        Offer(game, 2, 80, VoteChoice.No);
        Pass(game, 1);
        Assert.True(game.Do(0, new AcceptVoteDeal()).Accepted);
        Assert.Equal(1580, game.Player(0).Cash);
        Assert.Equal(1420, game.Player(2).Cash);

        // Held to the contract: only No is allowed.
        Assert.NotNull(game.DecisionFor(0)!.Deal);
        Assert.False(game.Engine.Submit(0, new CastVote(VoteChoice.Yes)).Accepted);
        Assert.False(game.Engine.Submit(0, new CastVote(VoteChoice.Abstain)).Accepted);

        game.Dice.AddCouncilRoll(0);
        game.Vote(0, VoteChoice.No, lobby: 20);
        game.Vote(1, VoteChoice.Yes);
        game.Vote(2, VoteChoice.No);
        CouncilResolved result = game.LastResolved();
        Assert.Equal(200, result.YesInfluence);   // Ben 100 + public opinion 100
        Assert.Equal(500, result.NoInfluence);    // Ana 100 + 160 contract + 40 lobbying, Cal 100, public opinion 100
        Assert.Equal(80, result.Votes.First(v => v.PlayerId == 0).Contract);
        Assert.Equal(1560, game.Player(0).Cash);  // the $20 lobbying goes to the bank
    }

    [Fact]
    public void ADeclinedContract_MovesNoMoney_AndThePoliticianVotesFreely()
    {
        var game = NewGame();
        game.ConveneAndPropose("public-works");
        Offer(game, 1, 100, VoteChoice.Yes);
        Pass(game, 2);
        Assert.True(game.Do(0, new DeclineVoteDeal()).Accepted);

        Assert.Equal(1500, game.Player(0).Cash);
        Assert.Equal(1500, game.Player(1).Cash);
        Assert.Single(game.State.Log.OfType<VoteDealDeclined>());
        Assert.Null(game.DecisionFor(0)!.Deal);
        Assert.True(game.Vote(0, VoteChoice.No).Accepted);
    }

    [Fact]
    public void PassingMeansOut_EvenWhenTheTopOfferChanges()
    {
        var game = NewGame();
        game.ConveneAndPropose("public-works");
        Pass(game, 1);
        Offer(game, 2, 50, VoteChoice.Yes);
        // Ben passed, so he isn't asked again: the Politician answers Cal's offer.
        Assert.Equal(DecisionKind.AnswerVoteDeal, game.DecisionFor(0)!.Kind);
    }

    [Fact]
    public void APlayerWhoCantBeatTheTopOffer_IsSkipped()
    {
        var game = NewGame();
        game.Player(1).Cash = 55;
        game.ConveneAndPropose("public-works");
        Assert.True(game.Do(1, new OfferVoteDeal(new VoteDeal(1, 0, 50, VoteChoice.Yes))).Accepted);
        Offer(game, 2, 60, VoteChoice.No);
        // Ben has $55, less than the $70 needed now.
        Assert.Equal(DecisionKind.AnswerVoteDeal, game.DecisionFor(0)!.Kind);
    }

    [Theory]
    [InlineData(1, 0, 5, VoteChoice.Yes)]        // under $10
    [InlineData(1, 0, 15, VoteChoice.Yes)]       // not a step of $10
    [InlineData(1, 0, 1600, VoteChoice.Yes)]     // more than you have
    [InlineData(1, 0, 100, VoteChoice.Abstain)]  // paying for an abstention
    [InlineData(1, 2, 100, VoteChoice.Yes)]      // paying someone who isn't the Politician
    [InlineData(2, 0, 100, VoteChoice.Yes)]      // an offer in someone else's name
    public void BadOffers_AreRefused(int from, int to, int amount, VoteChoice choice)
    {
        var game = NewGame();
        game.ConveneAndPropose("public-works");
        Assert.False(game.Engine.Submit(1, new OfferVoteDeal(new VoteDeal(from, to, amount, choice))).Accepted);
    }

    [Fact]
    public void TheContractMoney_IsInTheLedger()
    {
        var game = NewGame();
        game.ConveneAndPropose("public-works");
        Offer(game, 1, 100, VoteChoice.Yes);
        Pass(game, 2);
        game.Do(0, new AcceptVoteDeal());

        var ledger = GameHistory.Ledger(game.Engine.GetView(0), game.Engine.Content);
        Assert.Contains(ledger, e => e.PlayerId == 1 && e.Amount == -100 && e.OtherPlayerId == 0);
        Assert.Contains(ledger, e => e.PlayerId == 0 && e.Amount == 100 && e.OtherPlayerId == 1);
    }

    [Fact]
    public void ASessionMidBidding_SurvivesBeingSavedAndLoaded()
    {
        var game = NewGame();
        game.ConveneAndPropose("public-works");
        Offer(game, 1, 50, VoteChoice.Yes);

        var loaded = Engine.State.GameStateSerializer.FromJson(Engine.State.GameStateSerializer.ToJson(game.State));
        var engine = new GameEngine(loaded, game.Engine.Content, new Engine.Randomness.ScriptedDice());
        var cal = Assert.Single(engine.GetPendingDecisions());
        Assert.Equal((2, 60), (cal.PlayerId, cal.MinBid));
    }

    // ----- City Hall -----

    [Fact]
    public void ThePolitician_RefillsMomentumAtCityHall()
    {
        var game = NewGame();
        game.SetTile("city-hall");
        game.Player(0).Momentum = 0;
        game.RollOnto(0, "old-town-government");
        Assert.Equal(2, game.Player(0).Momentum);
        Assert.Contains(game.State.Log, e => e is MomentumGained { PlayerId: 0, Amount: 2, Source: "The Politician" });
    }

    [Fact]
    public void OtherPlayers_GetNoMomentumAtCityHall()
    {
        var game = NewGame("lawyer", "politician", "mogul");
        game.SetTile("city-hall");
        game.Player(0).Momentum = 0;
        game.RollOnto(0, "old-town-government");
        Assert.Equal(0, game.Player(0).Momentum);
    }
}
