using CityTycoon.Engine.Actions;
using CityTycoon.Engine.Content;
using CityTycoon.Engine.State;
using CityTycoon.Engine.Steps;

namespace CityTycoon.Engine.Cards;

/// <summary>
/// A Reaction window (Section 17.2): a moment when players holding a matching
/// Reaction card may play it. Only players who hold one are asked, one at a
/// time, so everyone else is passed automatically. The first card played
/// closes the window.
///
/// A step that has a Reaction moment keeps one of these as a property, so the
/// window is saved with the step.
/// </summary>
public sealed class ReactionWindow
{
    public CardTrigger Trigger { get; set; }

    /// <summary>Players still to be asked, in order.</summary>
    public List<int> ToAsk { get; set; } = [];

    /// <summary>The card that triggered this window, if any (shown to the player).</summary>
    public string? TriggerCardId { get; set; }

    /// <summary>For Detour: who is about to move, and how far.</summary>
    public int? MoverId { get; set; }
    public int MoveSpaces { get; set; }

    /// <summary>The card played, once someone plays one.</summary>
    public PlayCard? Played { get; set; }
    public int PlayedBy { get; set; } = -1;

    public bool IsOver => Played != null || ToAsk.Count == 0;

    /// <summary>Opens a window. Candidates who hold no card that could be played here are skipped straight away.</summary>
    public static ReactionWindow Open(EngineContext ctx, CardTrigger trigger, IEnumerable<int> candidates,
        string? triggerCardId = null, int? moverId = null, int moveSpaces = 0)
    {
        var window = new ReactionWindow { Trigger = trigger, TriggerCardId = triggerCardId, MoverId = moverId, MoveSpaces = moveSpaces };
        window.ToAsk = candidates.Where(id => !ctx.Player(id).IsBankrupt && window.PlaysFor(ctx, ctx.Player(id)).Count > 0).ToList();
        return window;
    }

    /// <summary>The question for the next player to ask, or null when the window is over.</summary>
    public PendingDecision? Decision(EngineContext ctx, string prompt)
    {
        // Someone's hand may have changed since the window opened, so check again.
        while (!IsOver && PlaysFor(ctx, ctx.Player(ToAsk[0])).Count == 0)
            ToAsk.RemoveAt(0);
        if (IsOver)
            return null;
        return new PendingDecision(ToAsk[0], DecisionKind.PlayReaction, prompt,
            Cards: TriggerCardId != null ? [TriggerCardId] : null, OtherPlayerId: MoverId, Spaces: MoveSpaces);
    }

    public IReadOnlyList<GameAction> LegalActions(EngineContext ctx, PendingDecision decision) =>
        [new NoThanks(), .. PlaysFor(ctx, ctx.Player(decision.PlayerId))];

    public void Receive(EngineContext ctx, PendingDecision decision, GameAction action)
    {
        ToAsk.Remove(decision.PlayerId);
        if (action is not PlayCard play)
            return;
        Played = play;
        PlayedBy = decision.PlayerId;
        CardRules.TakeFromHand(ctx, ctx.Player(decision.PlayerId), play);
    }

    /// <summary>Every reaction card play this player could make in this window.</summary>
    private List<PlayCard> PlaysFor(EngineContext ctx, PlayerState player)
    {
        var plays = new List<PlayCard>();
        foreach (string cardId in player.Hand.Distinct())
        {
            CardDefinition card = ctx.Content.Card(cardId);
            if (card.Timing == CardTiming.Reaction && card.Trigger == Trigger)
                plays.AddRange(CardRules.EffectOf(ctx.Content, cardId).Plays(ctx, player, card));
        }
        return plays;
    }
}
