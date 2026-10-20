using CityTycoon.Engine.Actions;
using CityTycoon.Engine.Cards;
using CityTycoon.Engine.Content;
using CityTycoon.Engine.Events;
using CityTycoon.Engine.Modifiers;
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
/// Started from the Casino or from the Dice Duel card. A Hustler who is losing the roll
/// may reroll their own dice once (Section 5).
/// </summary>
public sealed class DiceDuelStep : Step
{
    public int ChallengerId { get; set; }
    public int OpponentId { get; set; }
    public int Stake { get; set; }
    public bool Announced { get; set; }

    /// <summary>The challenged player's answer, null while waiting.</summary>
    public bool? Accepted { get; set; }

    /// <summary>The current roll's totals (0 means not rolled yet), kept while a Hustler decides on a reroll.</summary>
    public int ChallengerTotal { get; set; }
    public int OpponentTotal { get; set; }

    /// <summary>Players who have been asked about a reroll already (each is asked once per duel).</summary>
    public List<int> RerollOffered { get; set; } = [];

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

        while (true)
        {
            if (ChallengerTotal == 0)
            {
                ChallengerTotal = ctx.Dice.RollDie() + ctx.Dice.RollDie();
                OpponentTotal = ctx.Dice.RollDie() + ctx.Dice.RollDie();
                ctx.Emit(new DuelRolled(ChallengerId, ChallengerTotal, OpponentId, OpponentTotal));
            }
            if (ChallengerTotal == OpponentTotal)
            {
                ChallengerTotal = OpponentTotal = 0;   // a tie: roll again
                continue;
            }

            // The player who is losing may be a Hustler with a reroll to use.
            int loserId = ChallengerTotal < OpponentTotal ? ChallengerId : OpponentId;
            if (!RerollOffered.Contains(loserId) && RuleModifiers.CasinoReroll(ctx.State, ctx.Content, loserId))
            {
                RerollOffered.Add(loserId);
                int mine = loserId == ChallengerId ? ChallengerTotal : OpponentTotal;
                int theirs = loserId == ChallengerId ? OpponentTotal : ChallengerTotal;
                return StepResult.WaitFor(new PendingDecision(loserId, DecisionKind.ChooseReroll,
                    $"In the ${Stake} duel you rolled {mine} and {ctx.Player(loserId == ChallengerId ? OpponentId : ChallengerId).Name} rolled {theirs}. Use your Hustler reroll on your dice?",
                    Duel: new DuelChallenge(ChallengerId, OpponentId, Stake)));
            }

            // The stake may not be affordable any more (a payment in between); then it shrinks to what both have.
            int stake = Math.Min(Stake, Math.Min(ctx.Player(ChallengerId).Cash, ctx.Player(OpponentId).Cash));
            int winnerId = ChallengerTotal > OpponentTotal ? ChallengerId : OpponentId;
            int lostId = winnerId == ChallengerId ? OpponentId : ChallengerId;
            ctx.Player(winnerId).Cash += stake;
            ctx.Player(lostId).Cash -= stake;
            ctx.Emit(new DuelWon(winnerId, lostId, stake));
            return StepResult.Done;
        }
    }

    public override IReadOnlyList<GameAction> LegalActions(EngineContext ctx, PendingDecision decision) =>
        decision.Kind == DecisionKind.ChooseReroll ? [new RerollDice(), new NoThanks()] : [new AcceptDuel(), new DeclineDuel()];

    public override void Receive(EngineContext ctx, PendingDecision decision, GameAction action)
    {
        if (decision.Kind != DecisionKind.ChooseReroll)
        {
            Accepted = action is AcceptDuel;
            return;
        }
        if (action is not RerollDice)
            return;

        // Only the Hustler's own dice are rolled again.
        ctx.Emit(new CasinoRerolled(decision.PlayerId, "duel roll"));
        int again = ctx.Dice.RollDie() + ctx.Dice.RollDie();
        if (decision.PlayerId == ChallengerId)
            ChallengerTotal = again;
        else
            OpponentTotal = again;
        ctx.Emit(new DuelRolled(ChallengerId, ChallengerTotal, OpponentId, OpponentTotal));
    }
}

/// <summary>End of turn (Section 7.2, step 10): discard down to the hand limit.</summary>
public sealed class HandLimitStep : Step
{
    public int PlayerId { get; set; }

    public override StepResult Run(EngineContext ctx)
    {
        PlayerState player = ctx.Player(PlayerId);
        int limit = RuleModifiers.HandLimit(ctx.State, ctx.Content, PlayerId);
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

/// <summary>
/// The Pink perk, Arts District (Section 13.2): you drew two Opportunity cards. Keep one;
/// the other goes to the discard pile. Only the player drawing sees the two cards.
/// </summary>
public sealed class KeepOneCardStep : Step
{
    public int PlayerId { get; set; }

    /// <summary>The two cards drawn, as card IDs.</summary>
    public List<string> Cards { get; set; } = [];

    /// <summary>Where the cards came from, for the log ("Opportunity space", "City Park"...).</summary>
    public string Source { get; set; } = "";

    public bool Answered { get; set; }

    public override StepResult Run(EngineContext ctx) => Answered
        ? StepResult.Done
        : StepResult.WaitFor(new PendingDecision(PlayerId, DecisionKind.KeepCard,
            "Arts District: you drew two Opportunity cards. Keep one. The other is discarded.", Cards: Cards.ToList()));

    public override IReadOnlyList<GameAction> LegalActions(EngineContext ctx, PendingDecision decision) =>
        Cards.Distinct().Select(id => (GameAction)new KeepCard(id)).ToList();

    public override void Receive(EngineContext ctx, PendingDecision decision, GameAction action)
    {
        Answered = true;
        string kept = ((KeepCard)action).CardId;
        Cards.Remove(kept);
        ctx.Player(PlayerId).Hand.Add(kept);
        foreach (string other in Cards)
            CardRules.Discard(ctx, other);
        ctx.Emit(new OpportunityDrawn(PlayerId, Source));
        ctx.Emit(new CardKept(PlayerId));
    }
}
