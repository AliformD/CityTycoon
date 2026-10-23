using CityTycoon.Engine;
using CityTycoon.Engine.Actions;
using CityTycoon.Engine.Hosting;

namespace CityTycoon.AI.Tests;

/// <summary>
/// Decisions #159 and #162: bots weigh ordinances in dollars, the Politician lobbies when it pays, and the others bid
/// for the Politician's vote, which the Politician takes or turns down.
/// </summary>
public class VoteContractBotTests
{
    /// <summary>Player 0 owns Dark Blue in Uptown, built to hotels. The other three own nothing.</summary>
    private static GameEngine DarkBlueOwner(string? character = null, int level = 5, string second = "lawyer")
    {
        var engine = GameEngine.NewGame(new GameSetup
        {
            Players = [new("Bot", CharacterId: character), new("B", CharacterId: second), new("C", CharacterId: "medic"), new("D", CharacterId: "commuter")],
            Seed = 3,
            ShuffleTurnOrder = false,
        });
        foreach (string id in new[] { "skyline-drive", "platinum-row" })
        {
            engine.State.Properties[id].OwnerId = 0;
            engine.State.Properties[id].Level = level;
        }
        return engine;
    }

    private static PendingDecision VoteOn(int playerId, string ordinanceId, GameEngine engine, VoteDeal? deal = null) =>
        new(playerId, DecisionKind.CouncilVote, "", OrdinanceId: ordinanceId, DistrictId: "uptown",
            MaxLobby: engine.State.Player(playerId).Cash, Deal: deal);

    private static async Task<CastVote> VoteAsync(EasyBot bot, GameEngine engine, int playerId, string ordinanceId, VoteDeal? deal = null) =>
        Assert.IsType<CastVote>(await bot.DecideAsync(engine.GetView(playerId), VoteOn(playerId, ordinanceId, engine, deal), [new CastVote(VoteChoice.Yes)]));

    [Fact]
    public async Task HardBot_BacksARentBoostOnItsOwnSet_ButOnlyLobbiesWhenItPays()
    {
        var engine = DarkBlueOwner("politician");
        var vote = await VoteAsync(new HardBot(1), engine, 0, "tourism-boost");
        Assert.Equal(VoteChoice.Yes, vote.Choice);
        // Three rounds of extra rent is worth about $200 against the table; a lobbying dollar buys far less than a dollar of that.
        Assert.Equal(0, vote.Lobby);
    }

    [Fact]
    public async Task HardBot_FightsRentControlOnItsOwnSet()
    {
        var engine = DarkBlueOwner();
        var vote = await VoteAsync(new HardBot(1), engine, 0, "rent-control");
        Assert.Equal(VoteChoice.No, vote.Choice);
    }

    [Fact]
    public async Task HardBot_DoesNotLobby_OnAVoteThatHardlyTouchesIt()
    {
        var engine = DarkBlueOwner();
        // Rent Control in Old Town: nobody owns anything there.
        var decision = VoteOn(0, "rent-control", engine) with { DistrictId = "old-town" };
        var vote = Assert.IsType<CastVote>(await new HardBot(1).DecideAsync(engine.GetView(0), decision, [new CastVote(VoteChoice.Abstain)]));
        Assert.Equal(0, vote.Lobby);
    }

    [Fact]
    public async Task ABotHeldToADeal_VotesTheWayItWasPaidTo()
    {
        var engine = DarkBlueOwner();
        var deal = new VoteDeal(1, 0, 500, VoteChoice.No);
        foreach (EasyBot bot in new EasyBot[] { new EasyBot(1), new NormalBot(1), new HardBot(1) })
            Assert.Equal(VoteChoice.No, (await VoteAsync(bot, engine, 0, "tourism-boost", deal)).Choice);
    }

    [Fact]
    public async Task OnlyThePolitician_Lobbies()
    {
        var engine = DarkBlueOwner(level: 6);
        // Not the Politician: the vote panel offers no lobbying, and the bot asks for none.
        var decision = VoteOn(0, "tourism-boost", engine) with { MaxLobby = 0 };
        var vote = Assert.IsType<CastVote>(await new HardBot(1).DecideAsync(engine.GetView(0), decision, [new CastVote(VoteChoice.Yes)]));
        Assert.Equal(0, vote.Lobby);
    }

    [Fact]
    public async Task HardBot_TakesFreeMoney_ToVoteItsOwnWay()
    {
        var engine = DarkBlueOwner("politician");
        var decision = new PendingDecision(0, DecisionKind.AnswerVoteDeal, "", OrdinanceId: "tourism-boost", DistrictId: "uptown",
            Deal: new VoteDeal(1, 0, 10, VoteChoice.Yes));
        Assert.IsType<AcceptVoteDeal>(await new HardBot(1).DecideAsync(engine.GetView(0), decision, [new AcceptVoteDeal(), new DeclineVoteDeal()]));
    }

    [Fact]
    public async Task HardBot_WillNotSellAVoteThatMattersToIt_Cheaply_ButWillForEnough()
    {
        var engine = DarkBlueOwner("politician");
        var cheap = new PendingDecision(0, DecisionKind.AnswerVoteDeal, "", OrdinanceId: "rent-control", DistrictId: "uptown",
            Deal: new VoteDeal(1, 0, 10, VoteChoice.Yes));
        GameAction[] legal = [new AcceptVoteDeal(), new DeclineVoteDeal()];
        Assert.IsType<DeclineVoteDeal>(await new HardBot(1).DecideAsync(engine.GetView(0), cheap, legal));

        var rich = cheap with { Deal = new VoteDeal(1, 0, 1000, VoteChoice.Yes) };
        Assert.IsType<AcceptVoteDeal>(await new HardBot(1).DecideAsync(engine.GetView(0), rich, legal));
    }

    /// <summary>The bidding question for player 0, with player 1 as the Politician.</summary>
    private static PendingDecision BidFor(GameEngine engine, VoteDeal? top = null) =>
        new(0, DecisionKind.OfferVoteDeal, "", OrdinanceId: "tourism-boost", DistrictId: "uptown",
            MinBid: top == null ? 10 : top.Amount + 10, MaxBid: engine.State.Player(0).Cash, BidIncrement: 10, OtherPlayerId: 1, Deal: top);

    [Fact]
    public async Task HardBot_BidsForThePoliticiansVote_WhenItIsWorthMoreThanItCosts()
    {
        // Skyscrapers: a 25% rent boost is worth over $300 to it, and the Politician's vote (plus the money behind it) lifts its chance a lot.
        var engine = DarkBlueOwner(level: 6, second: "politician");
        var offer = Assert.IsType<OfferVoteDeal>(await new HardBot(1).DecideAsync(engine.GetView(0), BidFor(engine), [new NoThanks()]));
        Assert.Equal(new VoteDeal(0, 1, offer.Deal.Amount, VoteChoice.Yes), offer.Deal);
        Assert.InRange(offer.Deal.Amount, 10, 200);
    }

    [Fact]
    public async Task HardBot_OutbidsAnOpposingOffer_ButOnlyUpToWhatTheVoteIsWorth()
    {
        var engine = DarkBlueOwner(level: 6, second: "politician");
        var against = new VoteDeal(2, 1, 40, VoteChoice.No);
        var raise = Assert.IsType<OfferVoteDeal>(await new HardBot(1).DecideAsync(engine.GetView(0), BidFor(engine, against), [new NoThanks()]));
        Assert.Equal(VoteChoice.Yes, raise.Deal.Choice);
        Assert.True(raise.Deal.Amount >= 50);

        var tooDear = against with { Amount = 1000 };
        Assert.IsType<NoThanks>(await new HardBot(1).DecideAsync(engine.GetView(0), BidFor(engine, tooDear), [new NoThanks()]));
    }

    [Fact]
    public async Task HardBot_DoesNotBid_WhenTheTopOfferIsAlreadyForItsSide()
    {
        var engine = DarkBlueOwner(level: 6, second: "politician");
        var mine = new VoteDeal(2, 1, 40, VoteChoice.Yes);
        Assert.IsType<NoThanks>(await new HardBot(1).DecideAsync(engine.GetView(0), BidFor(engine, mine), [new NoThanks()]));
    }

    [Fact]
    public async Task EasyBot_NeverBids()
    {
        var engine = DarkBlueOwner(second: "politician");
        Assert.IsType<NoThanks>(await new EasyBot(1).DecideAsync(engine.GetView(0), BidFor(engine), [new NoThanks()]));
    }
}
