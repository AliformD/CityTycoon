using CityTycoon.Engine.Actions;
using CityTycoon.Engine.Cards;
using CityTycoon.Engine.Content;
using CityTycoon.Engine.Events;
using CityTycoon.Engine.State;

namespace CityTycoon.Engine.Steps;

/// <summary>
/// Makes one card happen: an Event card that was just drawn, or an
/// Opportunity card that was just played. If the card is played against
/// someone holding Insurance, they're asked first whether to cancel it.
/// </summary>
public sealed class CardPlayStep : Step
{
    public int PlayerId { get; set; }
    public PlayCard Play { get; set; } = new("");

    /// <summary>The Insurance question, once it has been opened.</summary>
    public ReactionWindow? Insurance { get; set; }

    public override StepResult Run(EngineContext ctx)
    {
        PlayerState player = ctx.Player(PlayerId);
        CardDefinition card = ctx.Content.Card(Play.CardId);
        ICardEffect effect = CardRules.EffectOf(ctx.Content, card.Id);
        var effectContext = new CardEffectContext(ctx, card, player, Play, Parent(ctx));

        // Played against someone? They may cancel it with Insurance (Section 15.4).
        // Event cards aren't "played" by anyone, so they can't be cancelled.
        if (card.Deck == CardDeck.Opportunity && effect.Victim(effectContext) is int victim && victim != PlayerId)
        {
            Insurance ??= ReactionWindow.Open(ctx, CardTrigger.CardPlayedAgainstYou, [victim], triggerCardId: card.Id);
            if (Insurance.Decision(ctx, $"{player.Name} played {card.Name} against you. Cancel it with Insurance?") is { } ask)
                return StepResult.WaitFor(ask);
            if (Insurance.Played != null)
            {
                ctx.Emit(new CardCancelled(PlayerId, card.Id, Insurance.PlayedBy));
                return StepResult.Done;
            }
        }

        if (player.IsBankrupt && card.Deck == CardDeck.Opportunity)
            return StepResult.Done;

        effect.Apply(effectContext);
        if (card.Deck == CardDeck.Event)
            CardRules.Discard(ctx, card.Id);
        return StepResult.Done;
    }

    /// <summary>The step underneath this one: what the card was played during.</summary>
    private Step? Parent(EngineContext ctx)
    {
        int index = ctx.State.Stack.IndexOf(this);
        return index > 0 ? ctx.State.Stack[index - 1] : null;
    }

    public override IReadOnlyList<GameAction> LegalActions(EngineContext ctx, PendingDecision decision) =>
        Insurance!.LegalActions(ctx, decision);

    public override void Receive(EngineContext ctx, PendingDecision decision, GameAction action) =>
        Insurance!.Receive(ctx, decision, action);
}
