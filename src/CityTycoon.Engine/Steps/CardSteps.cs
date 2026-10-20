using CityTycoon.Engine.Actions;
using CityTycoon.Engine.Cards;
using CityTycoon.Engine.Content;
using CityTycoon.Engine.Events;
using CityTycoon.Engine.Rules;
using CityTycoon.Engine.State;

namespace CityTycoon.Engine.Steps;

/// <summary>City Park (Section 15.3): choose a Momentum token or an Opportunity card.</summary>
public sealed class CityParkStep : Step
{
    public int PlayerId { get; set; }
    public bool Answered { get; set; }

    public override StepResult Run(EngineContext ctx)
    {
        if (Answered)
            return StepResult.Done;
        PlayerState player = ctx.Player(PlayerId);
        // No cards left anywhere: the token is the only choice, so don't ask.
        if (!CardRules.CanDraw(ctx.State, CardDeck.Opportunity))
        {
            MomentumRules.Gain(ctx, player, ctx.Rules.CityParkMomentum, "City Park");
            return StepResult.Done;
        }
        return StepResult.WaitFor(new PendingDecision(PlayerId, DecisionKind.CityParkChoice,
            "A sunny day in City Park. Take a Momentum token or draw an Opportunity card?"));
    }

    public override IReadOnlyList<GameAction> LegalActions(EngineContext ctx, PendingDecision decision) =>
        [new TakeMomentum(), new DrawOpportunity()];

    public override void Receive(EngineContext ctx, PendingDecision decision, GameAction action)
    {
        Answered = true;
        PlayerState player = ctx.Player(PlayerId);
        if (action is DrawOpportunity)
            CardRules.DrawOpportunity(ctx, player, "City Park");
        else
            MomentumRules.Gain(ctx, player, ctx.Rules.CityParkMomentum, "City Park");
    }
}

/// <summary>
/// The Mall (Section 15.3): buy one of the face-up shop cards, or sell a card
/// from your hand to the bank. One or the other, or neither.
/// </summary>
public sealed class MallStep : Step
{
    public int PlayerId { get; set; }
    public bool Answered { get; set; }

    public override StepResult Run(EngineContext ctx)
    {
        if (Answered)
            return StepResult.Done;
        PendingDecision decision = new(PlayerId, DecisionKind.MallVisit,
            "Welcome to the Mall. Buy a card, sell one of yours, or just browse.", Cards: ctx.State.MallShop.ToList());
        // Nothing to buy that you can afford and nothing to sell: walk on by.
        return LegalActions(ctx, decision).Count > 1 ? StepResult.WaitFor(decision) : StepResult.Done;
    }

    public override IReadOnlyList<GameAction> LegalActions(EngineContext ctx, PendingDecision decision)
    {
        PlayerState player = ctx.Player(PlayerId);
        var actions = new List<GameAction> { new NoThanks() };
        for (int slot = 0; slot < ctx.State.MallShop.Count; slot++)
        {
            if (player.Cash >= ctx.Rules.MallPrices[slot])
                actions.Add(new BuyCard(slot));
        }
        actions.AddRange(player.Hand.Distinct().Select(id => new SellCard(id)));
        return actions;
    }

    public override void Receive(EngineContext ctx, PendingDecision decision, GameAction action)
    {
        Answered = true;
        PlayerState player = ctx.Player(PlayerId);
        switch (action)
        {
            case BuyCard buy:
            {
                int price = ctx.Rules.MallPrices[buy.Slot];
                string card = ctx.State.MallShop[buy.Slot];
                player.Cash -= price;
                player.Hand.Add(card);
                // Slide the rest left and refill the last slot from the deck.
                ctx.State.MallShop.RemoveAt(buy.Slot);
                ctx.Emit(new MallCardBought(PlayerId, card, price));
                if (CardRules.Draw(ctx, CardDeck.Opportunity) is string refill)
                    ctx.State.MallShop.Add(refill);
                break;
            }
            case SellCard sell:
                player.Hand.Remove(sell.CardId);
                CardRules.Discard(ctx, sell.CardId);
                player.Cash += ctx.Rules.MallSellPrice;
                ctx.Emit(new MallCardSold(PlayerId, sell.CardId, ctx.Rules.MallSellPrice));
                break;
        }
    }
}

/// <summary>Fire! with a tie for the most-built property: the player chooses which one burns.</summary>
public sealed class ChooseFirePropertyStep : Step
{
    public int PlayerId { get; set; }
    public List<string> Choices { get; set; } = [];
    public bool Answered { get; set; }

    public override StepResult Run(EngineContext ctx) => Answered
        ? StepResult.Done
        : StepResult.WaitFor(new PendingDecision(PlayerId, DecisionKind.ChooseFireProperty,
            "Fire! Several of your properties are equally built up. Which one loses a level?"));

    public override IReadOnlyList<GameAction> LegalActions(EngineContext ctx, PendingDecision decision) =>
        Choices.Select(id => (GameAction)new ChooseProperty(id)).ToList();

    public override void Receive(EngineContext ctx, PendingDecision decision, GameAction action)
    {
        Answered = true;
        BuildingRules.Burn(ctx, PlayerId, ((ChooseProperty)action).PropertyId);
    }
}

/// <summary>Tip-Off: the player sees the top Event cards (only they do) and puts them back in any order.</summary>
public sealed class ArrangeEventsStep : Step
{
    public int PlayerId { get; set; }
    public int Count { get; set; }
    public bool Answered { get; set; }

    public override StepResult Run(EngineContext ctx) => Answered
        ? StepResult.Done
        : StepResult.WaitFor(new PendingDecision(PlayerId, DecisionKind.ArrangeEvents,
            $"These are the top {Count} Event cards, top card first. Put them back in any order.",
            Cards: ctx.State.EventDeck.Take(Count).ToList()));

    /// <summary>Every order the cards could go back in (6 orders for 3 cards).</summary>
    public override IReadOnlyList<GameAction> LegalActions(EngineContext ctx, PendingDecision decision) =>
        Orders(Enumerable.Range(0, Count).ToList()).Select(o => (GameAction)new ArrangeCards(o)).ToList();

    // A list inside a record isn't compared item by item, so check the order directly.
    public override bool IsLegal(EngineContext ctx, PendingDecision decision, GameAction action) =>
        action is ArrangeCards arrange && arrange.Order.Count == Count && arrange.Order.Order().SequenceEqual(Enumerable.Range(0, Count));

    public override void Receive(EngineContext ctx, PendingDecision decision, GameAction action)
    {
        Answered = true;
        var top = ctx.State.EventDeck.Take(Count).ToList();
        var order = ((ArrangeCards)action).Order;
        for (int i = 0; i < Count; i++)
            ctx.State.EventDeck[i] = top[order[i]];
        ctx.Emit(new EventsArranged(PlayerId, Count));
    }

    private static IEnumerable<List<int>> Orders(List<int> items)
    {
        if (items.Count <= 1)
        {
            yield return items;
            yield break;
        }
        foreach (int first in items)
        {
            foreach (List<int> rest in Orders(items.Where(i => i != first).ToList()))
                yield return [first, .. rest];
        }
    }
}

/// <summary>
/// A Dice Duel (Section 15.3): both players roll 2 dice and the higher total
/// takes the stake from the other. Ties roll again. The challenged player may say no.
/// Started from the Casino or from the Dice Duel card.
/// </summary>
public sealed class DiceDuelStep : Step
{
    public int ChallengerId { get; set; }
    public int OpponentId { get; set; }
    public int Stake { get; set; }
    public bool Announced { get; set; }

    /// <summary>The challenged player's answer, null while waiting.</summary>
    public bool? Accepted { get; set; }

    public override StepResult Run(EngineContext ctx)
    {
        if (!Announced)
        {
            Announced = true;
            ctx.Emit(new DuelChallenged(ChallengerId, OpponentId, Stake));
        }

        if (Accepted == null)
        {
            return StepResult.WaitFor(new PendingDecision(OpponentId, DecisionKind.RespondToDuel,
                $"{ctx.Player(ChallengerId).Name} challenges you to a Dice Duel for ${Stake}. Both roll 2 dice; the higher total takes everything.",
                Duel: new DuelChallenge(ChallengerId, OpponentId, Stake)));
        }

        if (Accepted == false)
        {
            ctx.Emit(new DuelDeclined(ChallengerId, OpponentId));
            return StepResult.Done;
        }

        // The stake may not be affordable any more (a payment in between); then it shrinks to what both have.
        int stake = Math.Min(Stake, Math.Min(ctx.Player(ChallengerId).Cash, ctx.Player(OpponentId).Cash));
        while (true)
        {
            int mine = ctx.Dice.RollDie() + ctx.Dice.RollDie();
            int theirs = ctx.Dice.RollDie() + ctx.Dice.RollDie();
            ctx.Emit(new DuelRolled(ChallengerId, mine, OpponentId, theirs));
            if (mine == theirs)
                continue;

            int winnerId = mine > theirs ? ChallengerId : OpponentId;
            int loserId = winnerId == ChallengerId ? OpponentId : ChallengerId;
            ctx.Player(winnerId).Cash += stake;
            ctx.Player(loserId).Cash -= stake;
            ctx.Emit(new DuelWon(winnerId, loserId, stake));
            return StepResult.Done;
        }
    }

    public override IReadOnlyList<GameAction> LegalActions(EngineContext ctx, PendingDecision decision) =>
        [new AcceptDuel(), new DeclineDuel()];

    public override void Receive(EngineContext ctx, PendingDecision decision, GameAction action) =>
        Accepted = action is AcceptDuel;
}

/// <summary>End of turn (Section 7.2, step 10): discard down to the hand limit.</summary>
public sealed class HandLimitStep : Step
{
    public int PlayerId { get; set; }

    public override StepResult Run(EngineContext ctx)
    {
        PlayerState player = ctx.Player(PlayerId);
        int limit = ctx.Rules.HandLimit;
        if (player.IsBankrupt || player.Hand.Count <= limit)
            return StepResult.Done;
        int extra = player.Hand.Count - limit;
        return StepResult.WaitFor(new PendingDecision(PlayerId, DecisionKind.DiscardCards,
            $"You can keep {limit} Opportunity cards. Discard {extra} more."));
    }

    public override IReadOnlyList<GameAction> LegalActions(EngineContext ctx, PendingDecision decision) =>
        ctx.Player(PlayerId).Hand.Distinct().Select(id => (GameAction)new DiscardCard(id)).ToList();

    public override void Receive(EngineContext ctx, PendingDecision decision, GameAction action)
    {
        string card = ((DiscardCard)action).CardId;
        ctx.Player(PlayerId).Hand.Remove(card);
        CardRules.Discard(ctx, card);
        ctx.Emit(new CardDiscarded(PlayerId, card));
    }
}
