using CityTycoon.Engine.Actions;
using CityTycoon.Engine.Content;
using CityTycoon.Engine.Events;
using CityTycoon.Engine.State;
using CityTycoon.Engine.Tests.Helpers;

namespace CityTycoon.Engine.Tests.Cards;

/// <summary>Decks, drawing, the hand limit and the Mall (Sections 15.3–15.5).</summary>
public class DeckTests
{
    [Fact]
    public void NewGame_ShufflesBothDecks_AndDealsThreeShopCards()
    {
        var game = new TestGame();
        int opportunities = GameContent.Default.Cards.Values.Where(c => c.Deck == CardDeck.Opportunity).Sum(c => c.Copies);
        int events = GameContent.Default.Cards.Values.Where(c => c.Deck == CardDeck.Event).Sum(c => c.Copies);
        Assert.Equal(18, opportunities);
        Assert.Equal(17, events);
        Assert.Equal(3, game.State.MallShop.Count);
        Assert.Equal(opportunities - 3, game.State.OpportunityDeck.Count);
        Assert.Equal(events, game.State.EventDeck.Count);
    }

    [Fact]
    public void OpportunityCards_AreSecret()
    {
        var game = new TestGame();
        game.StackOpportunities("audit");
        game.RollOnto(0, "old-town-opportunity");

        Assert.Equal(["audit"], game.Player(0).Hand);
        Assert.Contains(game.State.Log, e => e is OpportunityDrawn { PlayerId: 0 });
        Assert.Equal(["audit"], game.Engine.GetView(0).MyHand);
        Assert.Empty(game.Engine.GetView(1).MyHand);
        Assert.Equal(1, game.Engine.GetView(1).Players[0].HandCount);
    }

    [Fact]
    public void HandLimit_DiscardDownToThree_AtTheEndOfYourTurn()
    {
        var game = new TestGame();
        game.GiveCard(0, "audit", "fast-track", "lobbyist");
        game.StackOpportunities("dice-duel");
        game.RollOnto(0, "old-town-opportunity");
        Assert.Equal(4, game.Player(0).Hand.Count); // over the limit is fine during the turn

        game.EndTurn(0);
        Assert.Equal(DecisionKind.DiscardCards, game.DecisionFor(0)!.Kind);
        game.Do(0, new DiscardCard("lobbyist"));
        Assert.Equal(3, game.Player(0).Hand.Count);
        Assert.Contains("lobbyist", game.State.OpportunityDiscard);
        Assert.Equal(DecisionKind.ChooseMoveMethod, game.DecisionFor(1)!.Kind);
    }

    [Fact]
    public void CityPark_CanDrawACard()
    {
        var game = new TestGame();
        game.StackOpportunities("fast-track");
        game.RollOnto(0, "city-park");
        Assert.Equal(DecisionKind.CityParkChoice, game.DecisionFor(0)!.Kind);
        game.Do(0, new DrawOpportunity());
        Assert.Equal(["fast-track"], game.Player(0).Hand);
    }

    [Fact]
    public void AnEmptyDeck_ReshufflesItsDiscardPile()
    {
        var game = new TestGame();
        game.State.EventDiscard.AddRange(game.State.EventDeck);
        game.State.EventDeck.Clear();
        game.RollOnto(0, "midtown-event");

        Assert.Contains(game.State.Log, e => e is DeckReshuffled { Deck: CardDeck.Event, Cards: 17 });
        Assert.Contains(game.State.Log, e => e is EventDrawn { PlayerId: 0 });
    }

    [Fact]
    public void NoCardsAnywhere_NothingIsDrawn()
    {
        var game = new TestGame();
        game.State.OpportunityDeck.Clear();
        game.RollOnto(0, "old-town-opportunity");
        Assert.Empty(game.Player(0).Hand);
        Assert.Contains(game.State.Log, e => e is NoCardToDraw);
    }

    [Fact]
    public void Mall_BuyingACard_SlidesTheShopAndRefillsIt()
    {
        var game = new TestGame();
        game.State.MallShop.Clear();
        game.State.MallShop.AddRange(["audit", "fast-track", "lobbyist"]);
        game.StackOpportunities("dice-duel");
        game.RollOnto(0, "mall");

        Assert.Equal(DecisionKind.MallVisit, game.DecisionFor(0)!.Kind);
        game.Do(0, new BuyCard(1)); // the middle slot costs $100
        Assert.Equal(["fast-track"], game.Player(0).Hand);
        Assert.Equal(1400, game.Player(0).Cash);
        Assert.Equal(["audit", "lobbyist", "dice-duel"], game.State.MallShop);
    }

    [Fact]
    public void Mall_SellingACard_Pays50()
    {
        var game = new TestGame();
        game.GiveCard(0, "audit");
        game.RollOnto(0, "mall");
        game.Do(0, new SellCard("audit"));
        Assert.Empty(game.Player(0).Hand);
        Assert.Equal(1550, game.Player(0).Cash);
        Assert.Contains("audit", game.State.OpportunityDiscard);
    }

    [Fact]
    public void Mall_CantBuyWhatYouCantAfford()
    {
        var game = new TestGame();
        game.Player(0).Cash = 60;
        game.RollOnto(0, "mall");
        var actions = game.MainActions(0);
        Assert.Contains(new BuyCard(0), actions);
        Assert.DoesNotContain(new BuyCard(1), actions);
        Assert.DoesNotContain(new BuyCard(2), actions);
    }
}

/// <summary>Section 21.2: cards are data, so new ones need only JSON.</summary>
public class CardContentTests
{
    [Fact]
    public void AnExpansionFile_AddsACard_WithNoNewCode()
    {
        // A new card reusing the Audit effect with different numbers.
        const string json = """
            {
              "contentVersion": 1,
              "cards": [
                { "id": "tax-inspector", "deck": "opportunity", "polarity": "negative", "name": "Tax Inspector",
                  "text": "Choose an opponent. They pay 20% of their cash to the bank.",
                  "timing": "ownTurn", "target": "opponent",
                  "effect": { "type": "PayPercentOfCash", "percent": 20, "who": "target" } }
              ]
            }
            """;
        GameContent content = GameContent.Default.WithExtraCards(ContentLoader.ParseCards(json));
        var dice = new CityTycoon.Engine.Randomness.ScriptedDice();
        var engine = GameEngine.NewGame(new GameSetup
        {
            Players = [new PlayerSetup("A"), new PlayerSetup("B")],
            Seed = 1,
            ShuffleTurnOrder = false,
        }, content, dice);

        Assert.Equal(19, engine.State.OpportunityDeck.Count + engine.State.MallShop.Count);
        engine.State.Player(0).Hand.Add("tax-inspector");
        Assert.True(engine.Submit(0, new PlayCard("tax-inspector", TargetPlayerId: 1)).Accepted);
        Assert.Equal(1200, engine.State.Player(0).Cash);
        Assert.Equal(960, engine.State.Player(1).Cash);
    }

    [Theory]
    [InlineData("""{ "type": "NoSuchEffect" }""", "ownTurn", "unknown effect type")]
    [InlineData("""{ "type": "GainMomentum", "amount": 1 }""", "immediate", "Opportunity")]
    [InlineData("""{ "type": "GainMomentum", "amount": 1 }""", "reaction", "trigger")]
    public void BadCards_AreRejectedWhenTheContentLoads(string effect, string timing, string expected)
    {
        string json = $$"""
            { "contentVersion": 1, "cards": [
              { "id": "broken", "deck": "opportunity", "polarity": "negative", "name": "Broken", "text": "?", "timing": "{{timing}}", "effect": {{effect}} } ] }
            """;
        var error = Assert.ThrowsAny<Exception>(() => GameContent.Default.WithExtraCards(ContentLoader.ParseCards(json)));
        Assert.Contains(expected, error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AnOpportunityCardWithoutAPolarity_IsRejected()
    {
        const string json = """
            { "contentVersion": 1, "cards": [
              { "id": "no-flag", "deck": "opportunity", "name": "No Flag", "text": "?", "timing": "ownTurn", "target": "opponent",
                "effect": { "type": "PayPercentOfCash", "percent": 10, "who": "target" } } ] }
            """;
        var error = Assert.ThrowsAny<Exception>(() => GameContent.Default.WithExtraCards(ContentLoader.ParseCards(json)));
        Assert.Contains("polarity", error.Message);
    }

    [Fact]
    public void ADuplicateCardId_IsRejected()
    {
        const string json = """
            { "contentVersion": 1, "cards": [
              { "id": "audit", "deck": "opportunity", "polarity": "negative", "name": "Audit 2", "text": "?", "timing": "ownTurn", "target": "opponent",
                "effect": { "type": "PayPercentOfCash", "percent": 10, "who": "target" } } ] }
            """;
        Assert.ThrowsAny<Exception>(() => GameContent.Default.WithExtraCards(ContentLoader.ParseCards(json)));
    }
}

public class CardSaveTests
{
    [Fact]
    public void GameState_SurvivesARoundTrip_InTheMiddleOfAReactionWindow()
    {
        var game = new TestGame();
        game.Give(1, "market-street");
        game.GiveCard(0, "rent-strike");
        game.GiveCard(1, "insurance");
        game.RollOnto(0, "market-street");
        game.Do(0, new PlayCard("rent-strike")); // now player 1 is asked about Insurance

        GameState loaded = GameStateSerializer.FromJson(GameStateSerializer.ToJson(game.State));
        var resumed = new GameEngine(loaded, GameContent.Default, game.Dice);

        Assert.Equal(DecisionKind.PlayReaction, resumed.GetPendingDecisions().Single().Kind);
        Assert.True(resumed.Submit(1, new NoThanks()).Accepted);
        Assert.Equal(1493, loaded.Player(0).Cash);
        Assert.Equal(["insurance"], loaded.Player(1).Hand);
    }
}
