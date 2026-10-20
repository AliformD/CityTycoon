using CityTycoon.Engine.Actions;
using CityTycoon.Engine.Content;
using CityTycoon.Engine.State;
using CityTycoon.Engine.Steps;

namespace CityTycoon.Engine.Cards;

/// <summary>
/// One kind of card effect, like "CollectFromBank" (Section 21.2). Every card
/// names an effect type plus settings in JSON, so many cards can share one class.
/// A card with brand-new behavior needs one new class here, registered in
/// <see cref="CardEffects"/>.
///
/// The methods with bodies are "default" answers: an effect only overrides
/// the ones it cares about.
/// </summary>
public interface ICardEffect
{
    /// <summary>The name used in the JSON, like "CollectFromBank".</summary>
    string Type { get; }

    /// <summary>Checks the card's JSON has the settings this effect needs. Throws a ContentException if not.</summary>
    void Validate(CardDefinition card) { }

    /// <summary>True for cards played instead of rolling (Shortcut).</summary>
    bool ReplacesRoll => false;

    /// <summary>True for cards that can be played at the start of a Jail turn (Get Out of Jail Free).</summary>
    bool WorksInJail => false;

    /// <summary>Every way this card could be played right now, with its targets filled in. Empty means it can't be played.</summary>
    IEnumerable<PlayCard> Plays(EngineContext ctx, PlayerState player, CardDefinition card) => [new PlayCard(card.Id)];

    /// <summary>Is this exact play allowed? Effects with a number answer (a stake) override this to check a range.</summary>
    bool IsLegalPlay(EngineContext ctx, PlayerState player, CardDefinition card, PlayCard play) =>
        Plays(ctx, player, card).Contains(play);

    /// <summary>The player this card is played against, who may answer with Insurance. Null if nobody.</summary>
    int? Victim(CardEffectContext c) => null;

    /// <summary>Makes the card happen. It may push steps (for example an auction or a choice).</summary>
    void Apply(CardEffectContext c);
}

/// <summary>Everything an effect needs while it runs.</summary>
/// <param name="Parent">The step that was on top before the card was played (a turn, or a rent payment for Rent Strike).</param>
public sealed record CardEffectContext(EngineContext Ctx, CardDefinition Card, PlayerState Player, PlayCard Play, Step? Parent)
{
    public GameState State => Ctx.State;
    public RulesConfig Rules => Ctx.Rules;
}
