using CityTycoon.Engine;
using CityTycoon.Engine.Actions;
using CityTycoon.Engine.Hosting;

namespace CityTycoon.AI.Tests;

/// <summary>Decision #159: bots weigh ordinances in dollars, lobby when it pays, and make and answer vote deals.</summary>
public class VoteDealBotTests
{
    /// <summary>Player 0 owns Dark Blue in Uptown, built to hotels. The other three own nothing.</summary>
    private static GameEngine DarkBlueOwner(string? character = null, int level = 5)
    {
        var engine = GameEngine.NewGame(new GameSetup
        {
            Players = [new("Bot", CharacterId: character), new("B", CharacterId: "lawyer"), new("C", CharacterId: "medic"), new("D", CharacterId: "commuter")],
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
    public async Task HardBot_TakesFreeMoney_ToVoteItsOwnWay()
    {
        var engine = DarkBlueOwner();
        var decision = new PendingDecision(0, DecisionKind.AnswerVoteDeal, "", OrdinanceId: "tourism-boost", DistrictId: "uptown",
            Deal: new VoteDeal(1, 0, 10, VoteChoice.Yes));
        Assert.IsType<AcceptVoteDeal>(await new HardBot(1).DecideAsync(engine.GetView(0), decision, [new AcceptVoteDeal(), new DeclineVoteDeal()]));
    }

    [Fact]
    public async Task HardBot_WillNotSellAVoteThatMattersToIt_Cheaply_ButWillForEnough()
    {
        var engine = DarkBlueOwner();
        var cheap = new PendingDecision(0, DecisionKind.AnswerVoteDeal, "", OrdinanceId: "rent-control", DistrictId: "uptown",
            Deal: new VoteDeal(1, 0, 10, VoteChoice.Yes));
        GameAction[] legal = [new AcceptVoteDeal(), new DeclineVoteDeal()];
        Assert.IsType<DeclineVoteDeal>(await new HardBot(1).DecideAsync(engine.GetView(0), cheap, legal));

        var rich = cheap with { Deal = new VoteDeal(1, 0, 1000, VoteChoice.Yes) };
        Assert.IsType<AcceptVoteDeal>(await new HardBot(1).DecideAsync(engine.GetView(0), rich, legal));
    }

    [Fact]
    public async Task HardBot_OffersADeal_WhenOneMoreVoteIsWorthMoreThanItCosts()
    {
        // Skyscrapers: a 25% rent boost is worth over $300 to it, and one more Yes vote lifts its chance from 47% to 60%.
        var engine = DarkBlueOwner("politician", level: 6);
        var decision = new PendingDecision(0, DecisionKind.OfferVoteDeal, "", OrdinanceId: "tourism-boost", DistrictId: "uptown",
            MaxBid: engine.State.Player(0).Cash);
        var offer = Assert.IsType<OfferVoteDeal>(await new HardBot(1).DecideAsync(engine.GetView(0), decision, [new NoThanks()]));
        Assert.Equal(VoteChoice.Yes, offer.Deal.Choice);
        Assert.NotEqual(0, offer.Deal.ToId);
        Assert.InRange(offer.Deal.Amount, 10, 50);
    }

    [Fact]
    public async Task EasyBot_NeverOffersDeals()
    {
        var engine = DarkBlueOwner();
        var decision = new PendingDecision(0, DecisionKind.OfferVoteDeal, "", OrdinanceId: "tourism-boost", DistrictId: "uptown", MaxBid: 1000);
        Assert.IsType<NoThanks>(await new EasyBot(1).DecideAsync(engine.GetView(0), decision, [new NoThanks()]));
    }
}
