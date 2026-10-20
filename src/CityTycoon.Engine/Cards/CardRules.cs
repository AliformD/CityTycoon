using CityTycoon.Engine.Actions;
using CityTycoon.Engine.Content;
using CityTycoon.Engine.Events;
using CityTycoon.Engine.Modifiers;
using CityTycoon.Engine.State;
using CityTycoon.Engine.Steps;

namespace CityTycoon.Engine.Cards;

/// <summary>
/// The decks, the Mall shop and players' hands (Sections 15.3 to 15.6), and
/// which cards a player may play right now.
/// </summary>
public static class CardRules
{
    /// <summary>Builds and shuffles both decks, then fills the Mall shop (game setup).</summary>
    public static void SetUpDecks(GameState state, GameContent content)
    {
        state.OpportunityDeck = BuildDeck(content, CardDeck.Opportunity);
        state.EventDeck = BuildDeck(content, CardDeck.Event);
        state.Rng.Shuffle(state.OpportunityDeck);
        state.Rng.Shuffle(state.EventDeck);
        for (int slot = 0; slot < content.Rules.MallPrices.Count && state.OpportunityDeck.Count > 0; slot++)
        {
            state.MallShop.Add(state.OpportunityDeck[0]);
            state.OpportunityDeck.RemoveAt(0);
        }
    }

    /// <summary>One entry per copy of every card in this deck, in a fixed order (it's shuffled afterwards).</summary>
    private static List<string> BuildDeck(GameContent content, CardDeck deck) =>
        content.Cards.Values
            .Where(c => c.Deck == deck)
            .OrderBy(c => c.Id, StringComparer.Ordinal)
            .SelectMany(c => Enumerable.Repeat(c.Id, c.Copies))
            .ToList();

    public static List<string> DrawPile(GameState state, CardDeck deck) => deck == CardDeck.Opportunity ? state.OpportunityDeck : state.EventDeck;

    public static List<string> DiscardPile(GameState state, CardDeck deck) => deck == CardDeck.Opportunity ? state.OpportunityDiscard : state.EventDiscard;

    /// <summary>
    /// Makes sure the draw pile has at least this many cards, shuffling the
    /// discard pile underneath when it runs low (Section 15.6). Returns false
    /// if there still aren't any cards (they're all in hands or the shop).
    /// </summary>
    public static bool EnsureCards(EngineContext ctx, CardDeck deck, int wanted = 1)
    {
        List<string> pile = DrawPile(ctx.State, deck);
        List<string> discard = DiscardPile(ctx.State, deck);
        if (pile.Count < wanted && discard.Count > 0)
        {
            ctx.State.Rng.Shuffle(discard);
            pile.AddRange(discard);
            ctx.Emit(new DeckReshuffled(deck, discard.Count));
            discard.Clear();
        }
        return pile.Count > 0;
    }

    /// <summary>Takes the top card, reshuffling first if the deck is empty. Null if there are no cards left at all.</summary>
    public static string? Draw(EngineContext ctx, CardDeck deck)
    {
        if (!EnsureCards(ctx, deck))
            return null;
        List<string> pile = DrawPile(ctx.State, deck);
        string card = pile[0];
        pile.RemoveAt(0);
        return card;
    }

    /// <summary>True if a card could be drawn from this deck (now or after a reshuffle).</summary>
    public static bool CanDraw(GameState state, CardDeck deck) =>
        DrawPile(state, deck).Count > 0 || DiscardPile(state, deck).Count > 0;

    /// <summary>Draws an Opportunity card into a player's hand. Nobody else learns which card it was.</summary>
    public static void DrawOpportunity(EngineContext ctx, PlayerState player, string source)
    {
        if (Draw(ctx, CardDeck.Opportunity) is not string card)
        {
            ctx.Emit(new NoCardToDraw(player.Id, CardDeck.Opportunity));
            return;
        }
        player.Hand.Add(card);
        ctx.Emit(new OpportunityDrawn(player.Id, source));
    }

    /// <summary>Draws an Event card and makes it happen (Section 15.5).</summary>
    public static void DrawEvent(EngineContext ctx, PlayerState player)
    {
        if (Draw(ctx, CardDeck.Event) is not string card)
        {
            ctx.Emit(new NoCardToDraw(player.Id, CardDeck.Event));
            return;
        }
        ctx.Emit(new EventDrawn(player.Id, card));
        ctx.Push(new CardPlayStep { PlayerId = player.Id, Play = new PlayCard(card) });
    }

    public static void Discard(EngineContext ctx, string cardId) =>
        DiscardPile(ctx.State, ctx.Content.Card(cardId).Deck).Add(cardId);

    /// <summary>Takes a card out of a hand, puts it on the discard pile and logs that it was played.</summary>
    public static void TakeFromHand(EngineContext ctx, PlayerState player, PlayCard play)
    {
        player.Hand.Remove(play.CardId);
        Discard(ctx, play.CardId);
        ctx.Emit(new CardPlayed(player.Id, play.CardId, play.TargetPlayerId, play.TargetSpaceId, play.Number));
    }

    /// <summary>The effect class that runs a card.</summary>
    public static ICardEffect EffectOf(GameContent content, string cardId) =>
        CardEffects.Find(content.Card(cardId).EffectType)!;

    /// <summary>The turn currently being played (the top-most turn on the stack).</summary>
    public static TurnStep? CurrentTurn(EngineContext ctx) => ctx.State.Stack.OfType<TurnStep>().LastOrDefault();

    /// <summary>
    /// Every own-turn card play this player could make at this moment of their turn.
    /// In Jail, only Get Out of Jail Free can be played (Section 14.2).
    /// </summary>
    public static List<GameAction> OwnTurnPlays(EngineContext ctx, PlayerState player, DecisionKind moment)
    {
        var plays = new List<GameAction>();
        foreach (string cardId in player.Hand.Distinct())
        {
            CardDefinition card = ctx.Content.Card(cardId);
            if (card.Timing != CardTiming.OwnTurn)
                continue;
            ICardEffect effect = EffectOf(ctx.Content, cardId);
            bool fits = moment switch
            {
                DecisionKind.ChooseMoveMethod => true,
                DecisionKind.PostMoveActions => !effect.ReplacesRoll,
                DecisionKind.JailChoice => effect.WorksInJail,
                _ => false,
            };
            if (fits)
                plays.AddRange(effect.Plays(ctx, player, card).Where(p => TargetAllowed(ctx, p)));
        }
        return plays;
    }

    /// <summary>A property next to a revealed Police Station can't be the target of an Opportunity card (Section 12.3).</summary>
    public static bool TargetAllowed(EngineContext ctx, PlayCard play) =>
        play.TargetSpaceId is not string id
        || !ctx.State.Properties.ContainsKey(id)
        || RuleModifiers.CanBeTargetedByCards(ctx.State, ctx.Content, id);

    /// <summary>Checks a card play is allowed at this moment (number answers like stakes are range-checked).</summary>
    public static bool IsLegalOwnTurnPlay(EngineContext ctx, PlayerState player, DecisionKind moment, PlayCard play)
    {
        if (!player.Hand.Contains(play.CardId))
            return false;
        CardDefinition card = ctx.Content.Card(play.CardId);
        ICardEffect effect = EffectOf(ctx.Content, play.CardId);
        bool fits = card.Timing == CardTiming.OwnTurn && moment switch
        {
            DecisionKind.ChooseMoveMethod => true,
            DecisionKind.PostMoveActions => !effect.ReplacesRoll,
            DecisionKind.JailChoice => effect.WorksInJail,
            _ => false,
        };
        return fits && TargetAllowed(ctx, play) && effect.IsLegalPlay(ctx, player, card, play);
    }

    /// <summary>Starts playing a card from a hand: it's discarded now, and its effect runs next.</summary>
    public static void BeginPlay(EngineContext ctx, PlayerState player, PlayCard play)
    {
        TakeFromHand(ctx, player, play);
        ctx.Push(new CardPlayStep { PlayerId = player.Id, Play = play });
    }
}
