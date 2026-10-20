using CityTycoon.Engine;
using CityTycoon.Engine.Actions;

namespace CityTycoon.AI.Tests;

/// <summary>Phase 5 bot behaviour: using Opportunity cards.</summary>
public class CardBotTests
{
    private static GameEngine NewGame() => GameEngine.NewGame(new GameSetup
    {
        Players = [new("Bot"), new("Other")],
        Seed = 3,
        ShuffleTurnOrder = false,
    });

    [Fact]
    public async Task EasyBot_AlwaysUsesRentStrike()
    {
        var engine = NewGame();
        engine.State.Player(0).Hand.Add("rent-strike");
        var decision = new PendingDecision(0, DecisionKind.PlayReaction, "");
        GameAction[] legal = [new NoThanks(), new PlayCard("rent-strike")];

        Assert.Equal(new PlayCard("rent-strike"), await new EasyBot(1).DecideAsync(engine.GetView(0), decision, legal));
    }

    [Fact]
    public async Task EasyBot_DetoursAnOpponentOntoItsOwnProperty()
    {
        var engine = NewGame();
        engine.State.Player(0).Hand.Add("detour");
        engine.State.Properties["platinum-row"].OwnerId = 0;
        engine.State.Properties["platinum-row"].Level = 4;
        int n = engine.State.Board.Count;
        engine.State.Player(1).Position = (engine.State.IndexOf("platinum-row") - 8 + n) % n;
        var decision = new PendingDecision(0, DecisionKind.PlayReaction, "", OtherPlayerId: 1, Spaces: 7);
        GameAction[] legal = [new NoThanks(), new PlayCard("detour", 1, Number: -1), new PlayCard("detour", 1, Number: 1)];

        Assert.Equal(new PlayCard("detour", 1, Number: 1), await new EasyBot(1).DecideAsync(engine.GetView(0), decision, legal));
    }

    [Fact]
    public async Task EasyBot_DoesntDetourForNothing()
    {
        var engine = NewGame();
        engine.State.Player(0).Hand.Add("detour");
        engine.State.Player(1).Position = 0;
        var decision = new PendingDecision(0, DecisionKind.PlayReaction, "", OtherPlayerId: 1, Spaces: 7);
        GameAction[] legal = [new NoThanks(), new PlayCard("detour", 1, Number: -1), new PlayCard("detour", 1, Number: 1)];

        Assert.Equal(new NoThanks(), await new EasyBot(1).DecideAsync(engine.GetView(0), decision, legal));
    }

    [Fact]
    public async Task EasyBot_DiscardsItsLeastUsefulCard()
    {
        var engine = NewGame();
        engine.State.Player(0).Hand.AddRange(["insurance", "dice-duel", "rent-strike", "fast-track"]);
        var decision = new PendingDecision(0, DecisionKind.DiscardCards, "");
        var legal = engine.State.Player(0).Hand.Select(c => (GameAction)new DiscardCard(c)).ToList();

        Assert.Equal(new DiscardCard("dice-duel"), await new EasyBot(1).DecideAsync(engine.GetView(0), decision, legal));
    }

    [Fact]
    public async Task EasyBot_PlaysGetOutOfJailFree_InJail()
    {
        var engine = NewGame();
        var decision = new PendingDecision(0, DecisionKind.JailChoice, "");
        GameAction[] legal = [new RollForDoubles(), new PayBail(), new PlayCard("get-out-of-jail-free")];

        Assert.Equal(new PlayCard("get-out-of-jail-free"), await new EasyBot(1).DecideAsync(engine.GetView(0), decision, legal));
    }

    [Fact]
    public async Task EasyBot_PlaysCardsInARealGame()
    {
        // Bots play a whole game with cards stacked in their hands, and the engine accepts every choice.
        var engine = NewGame();
        engine.State.Player(0).Hand.AddRange(["fast-track", "audit", "inside-source"]);
        var host = new Engine.Hosting.GameHost(engine, new Dictionary<int, Engine.Hosting.IPlayerController>
        {
            [0] = new EasyBot(1),
            [1] = new EasyBot(2),
        });
        for (int i = 0; i < 2000 && !engine.State.IsOver; i++)
            await host.StepAsync();
        Assert.Contains(engine.State.Log, e => e is Engine.Events.CardPlayed { PlayerId: 0, CardId: "fast-track" });
        Assert.Contains(engine.State.Log, e => e is Engine.Events.CardPlayed { PlayerId: 0, CardId: "audit" });
    }
}
