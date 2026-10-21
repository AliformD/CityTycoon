using CityTycoon.Engine.Actions;
using CityTycoon.Engine.Content;
using CityTycoon.Engine.State;

namespace CityTycoon.Engine.Tests.SaveLoad;

/// <summary>Phase 9: a saved game reloads exactly, and carries on the same way as the original.</summary>
public class SaveLoadTests
{
    private static GameEngine NewGame(ulong seed) => GameEngine.NewGame(new GameSetup
    {
        Players = Enumerable.Range(0, 4).Select(i => new PlayerSetup($"P{i}")).ToList(),
        Seed = seed,
        MaxRounds = 12,
        Characters = CharacterMode.Random,
    });

    /// <summary>A simple repeatable player: always the first answer that isn't a "manage your money" extra, and a tiny bid in auctions.</summary>
    private static void Step(GameEngine engine)
    {
        foreach (var decision in engine.GetPendingDecisions().ToList())
        {
            GameAction action = decision.Kind switch
            {
                DecisionKind.SubmitBid when decision.MaxBid >= decision.MinBid && decision.PlayerId % 2 == 0 => new PlaceBid(decision.MinBid),
                DecisionKind.SubmitBid => new PassBid(),
                DecisionKind.CouncilVote => new CastVote(decision.PlayerId % 2 == 0 ? VoteChoice.Yes : VoteChoice.No, 20),
                _ => Fallback(engine, decision.PlayerId),
            };
            if (!engine.Submit(decision.PlayerId, action).Accepted)
                Assert.True(engine.Submit(decision.PlayerId, Fallback(engine, decision.PlayerId)).Accepted);
        }
    }

    private static GameAction Fallback(GameEngine engine, int playerId)
    {
        var legal = engine.GetLegalActions(playerId);
        return legal.FirstOrDefault(a => a is not ManagementAction) ?? legal.First();
    }

    [Fact]
    public void ASavedGame_ReloadsExactly()
    {
        var engine = NewGame(5);
        for (int i = 0; i < 300 && !engine.State.IsOver; i++)
            Step(engine);

        string json = GameStateSerializer.ToJson(engine.State);
        var loaded = GameStateSerializer.FromJson(json);

        Assert.Equal(json, GameStateSerializer.ToJson(loaded));
        Assert.Equal(engine.State.Round, loaded.Round);
        Assert.Equal(engine.State.Log.Count, loaded.Log.Count);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void ALoadedGame_PlaysOnExactlyLikeTheOriginal(ulong seed)
    {
        var original = NewGame(seed);
        for (int i = 0; i < 150 && !original.State.IsOver; i++)
            Step(original);

        var copy = new GameEngine(GameStateSerializer.FromJson(GameStateSerializer.ToJson(original.State)), GameContent.Default);

        for (int i = 0; i < 3000 && !original.State.IsOver; i++)
        {
            Step(original);
            Step(copy);
        }

        Assert.True(original.State.IsOver);
        Assert.True(copy.State.IsOver);
        Assert.Equal(GameStateSerializer.ToJson(original.State), GameStateSerializer.ToJson(copy.State));
    }

    [Fact]
    public void AGame_CanBeSavedAndLoadedAtEveryPoint()
    {
        // Save and reload after every single step; the game must behave exactly the same as one that never was saved.
        var plain = NewGame(9);
        var reloaded = NewGame(9);
        for (int i = 0; i < 400 && !plain.State.IsOver; i++)
        {
            Step(plain);
            Step(reloaded);
            reloaded = new GameEngine(GameStateSerializer.FromJson(GameStateSerializer.ToJson(reloaded.State)), GameContent.Default);
        }
        Assert.Equal(GameStateSerializer.ToJson(plain.State), GameStateSerializer.ToJson(reloaded.State));
    }

    [Fact]
    public void ASaveFromBeforeTheLedger_StillLoads()
    {
        // Older saves have these events without the fields the money ledger added later.
        var engine = NewGame(5);
        string json = GameStateSerializer.ToJson(engine.State);
        string oldEvents = """
            {"$type":"PlayerBankrupt","playerId":1,"creditorId":null,"cashHandedOver":40,"propertiesHandedOver":[]},
            {"$type":"TradeCompleted","offer":{"fromId":0,"toId":1,"giveCash":50,"getCash":0,"giveProperties":[],"getProperties":[]},"mortgageFees":0},
            {"$type":"LotteryWon","winnerId":0,"pot":60},
            """;
        json = json.Replace("\"log\":[", "\"log\":[" + oldEvents);

        var loaded = GameStateSerializer.FromJson(json);
        var bankrupt = Assert.IsType<Events.PlayerBankrupt>(loaded.Log[0]);
        Assert.Equal(0, bankrupt.LoansPaidBack);
        Assert.Equal(0, Assert.IsType<Events.TradeCompleted>(loaded.Log[1]).FromFees);
        Assert.Null(Assert.IsType<Events.LotteryWon>(loaded.Log[2]).Entrants);
        // And the ledger can still be worked out from it.
        var view = new GameEngine(loaded, GameContent.Default).GetView(0);
        Assert.NotEmpty(Events.GameHistory.Ledger(view, GameContent.Default));
    }
}
