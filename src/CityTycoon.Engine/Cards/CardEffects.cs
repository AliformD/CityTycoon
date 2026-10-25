using CityTycoon.Engine.Actions;
using CityTycoon.Engine.Content;
using CityTycoon.Engine.Events;
using CityTycoon.Engine.Modifiers;
using CityTycoon.Engine.Rules;
using CityTycoon.Engine.State;
using CityTycoon.Engine.Steps;
using CityTycoon.Engine.Tiles;

namespace CityTycoon.Engine.Cards;

/// <summary>
/// The list of every card effect type, looked up by the "type" name in the
/// card JSON. To add a brand-new kind of card: write one class below and add
/// it to this list. Cards that reuse an existing type need no C# at all.
/// </summary>
public static class CardEffects
{
    private static readonly Dictionary<string, ICardEffect> All = new ICardEffect[]
    {
        // Event effects (Section 15.5)
        new SendToHospitalEffect(),
        new FireEffect(),
        new PayPercentOfCashEffect(),
        new PerBuildingEffect(),
        new CollectFromBankEffect(),
        new CollectFromEachPlayerEffect(),
        new LotteryEffect(),
        new MoveEveryoneBackEffect(),
        new RoadWorkEffect(),
        new GainMomentumEffect(),
        new AdvanceToEffect(),
        new GoToJailEffect(),
        new EmergencySessionEffect(),

        // Opportunity effects (Section 15.4)
        new RentMultiplierEffect(),
        new CancelCardEffect(),
        new DetourEffect(),
        new ShortcutEffect(),
        new ForceAuctionEffect(),
        new ConstructionPermitEffect(),
        new GetOutOfJailFreeEffect(),
        new ShowdownEffect(),
        new ArrangeEventsEffect(),
        new PropertyRentBoostEffect(),
        new PeekTileEffect(),
        new AddInfluenceEffect(),
    }.ToDictionary(e => e.Type);

    /// <summary>The effect with this type name, or null if there isn't one.</summary>
    public static ICardEffect? Find(string type) => All.GetValueOrDefault(type);

    public static IReadOnlyCollection<string> Types => All.Keys;

    /// <summary>The other players still in the game, in turn order starting after this one.</summary>
    internal static List<PlayerState> OthersInTurnOrder(GameState state, int playerId)
    {
        int seat = state.Players.FindIndex(p => p.Id == playerId);
        return Enumerable.Range(1, state.Players.Count - 1)
            .Select(offset => state.Players[(seat + offset) % state.Players.Count])
            .Where(p => !p.IsBankrupt)
            .ToList();
    }

    /// <summary>Everyone still in the game, in turn order starting with this player.</summary>
    internal static List<PlayerState> EveryoneFrom(GameState state, int playerId) =>
        [state.Player(playerId), .. OthersInTurnOrder(state, playerId)];

    /// <summary>
    /// Fundraising (decision #153): when a card has just given this player <paramref name="amount"/> in cash,
    /// the bank pays them the bonus on top (the Politician gets the same again). Logged as perk income.
    /// </summary>
    public static void Fundraise(EngineContext ctx, PlayerState player, int amount)
    {
        if (amount <= 0 || player.IsBankrupt)
            return;
        foreach (var (bonus, source) in RuleModifiers.CardCashBonuses(ctx.State, ctx.Content, player.Id, amount))
        {
            player.Cash += bonus;
            ctx.Emit(new PerkIncome(player.Id, bonus, source));
        }
    }
}

// ---------------------------------------------------------------- Event effects

/// <summary>Sprained Ankle: go to the Hospital (Section 15.3).</summary>
public sealed class SendToHospitalEffect : ICardEffect
{
    public string Type => "SendToHospital";
    public void Apply(CardEffectContext c) => Movement.SendToHospital(c.Ctx, c.Player, c.Card.Name);
}

/// <summary>Fire!: your most-built property loses 1 level, unless an Insured marker protects you.</summary>
public sealed class FireEffect : ICardEffect
{
    public string Type => "Fire";

    public void Apply(CardEffectContext c)
    {
        var built = c.State.Properties.Values.Where(p => p.OwnerId == c.Player.Id && p.Level > 0).ToList();
        if (built.Count == 0)
        {
            c.Ctx.Emit(new FireAvoided(c.Player.Id, "no buildings"));
            return;
        }
        // Properties next to a revealed Fire Station can't burn (Section 12.3).
        built = built.Where(p => !RuleModifiers.IsFireSafe(c.State, c.Ctx.Content, p.PropertyId)).ToList();
        if (built.Count == 0)
        {
            c.Ctx.Emit(new FireAvoided(c.Player.Id, "Fire Station"));
            return;
        }
        // An Insured marker covers one Hospital bill or one Fire!, whichever comes first.
        if (c.Player.IsInsured)
        {
            c.Player.IsInsured = false;
            c.Ctx.Emit(new InsuranceUsed(c.Player.Id));
            c.Ctx.Emit(new FireAvoided(c.Player.Id, "Insured marker"));
            return;
        }

        int tallest = built.Max(p => p.Level);
        var candidates = built.Where(p => p.Level == tallest).Select(p => p.PropertyId).ToList();
        if (candidates.Count == 1)
            BuildingRules.Burn(c.Ctx, c.Player.Id, candidates[0]);
        else
            c.Ctx.Push(new ChooseFirePropertyStep { PlayerId = c.Player.Id, Choices = candidates });
    }
}

/// <summary>
/// Pay a percent of your cash to the bank, rounded down. "who" is "target"
/// (Audit), "you", or "everyoneButPoorest" (Recession: whoever has the least
/// cash is exempt, and so is everyone tied with them).
/// </summary>
public sealed class PayPercentOfCashEffect : ICardEffect
{
    public string Type => "PayPercentOfCash";

    public void Validate(CardDefinition card)
    {
        card.Int("percent");
        if (card.Setting("who") is not ("target" or "you" or "everyoneButPoorest"))
            throw new ContentException($"Card '{card.Id}': \"who\" must be target, you or everyoneButPoorest.");
    }

    public IEnumerable<PlayCard> Plays(EngineContext ctx, PlayerState player, CardDefinition card) =>
        card.Setting("who") == "target"
            ? CardEffects.OthersInTurnOrder(ctx.State, player.Id)
                .Where(p => RuleModifiers.CanPlayerBeTargeted(ctx.State, ctx.Content, p.Id, card))
                .Select(p => new PlayCard(card.Id, TargetPlayerId: p.Id))
            : [new PlayCard(card.Id)];

    public int? Victim(CardEffectContext c) => c.Card.Setting("who") == "target" ? c.Play.TargetPlayerId : null;

    public void Apply(CardEffectContext c)
    {
        List<PlayerState> payers = c.Card.Setting("who") switch
        {
            "target" => [c.State.Player(c.Play.TargetPlayerId!.Value)],
            "you" => [c.Player],
            _ => EveryoneButPoorest(c),
        };
        foreach (PlayerState p in payers)
        {
            int amount = p.Cash * c.Card.Int("percent") / 100;
            if (amount <= 0)
                continue;
            p.Cash -= amount;
            c.Ctx.Emit(new CashToBank(p.Id, amount, c.Card.Id));
        }
    }

    private static List<PlayerState> EveryoneButPoorest(CardEffectContext c)
    {
        var everyone = CardEffects.EveryoneFrom(c.State, c.Player.Id);
        int least = everyone.Min(p => p.Cash);
        return everyone.Where(p => p.Cash > least).ToList();
    }
}

/// <summary>
/// Money for each building you own: collected from the bank (Housing Boom) or,
/// with "pay": true, paid to the bank (Property Reassessment). "who" is "you" or "everyone".
/// </summary>
public sealed class PerBuildingEffect : ICardEffect
{
    public string Type => "PerBuilding";

    public void Validate(CardDefinition card)
    {
        card.Int("perHouse");
        card.Int("perHotel");
        card.Int("perSkyscraper");
        if (card.Setting("who") is not ("you" or "everyone"))
            throw new ContentException($"Card '{card.Id}': \"who\" must be you or everyone.");
    }

    public void Apply(CardEffectContext c)
    {
        var players = c.Card.Setting("who") == "everyone" ? CardEffects.EveryoneFrom(c.State, c.Player.Id) : [c.Player];
        // Pushed in reverse, so the first player in turn order pays first.
        foreach (PlayerState p in Enumerable.Reverse(players))
        {
            int amount = Amount(c, p.Id);
            if (amount <= 0)
                continue;
            if (c.Card.Flag("pay"))
            {
                c.Ctx.Push(new PaymentStep { FromId = p.Id, Amount = amount, Reason = PaymentReason.Card, CardId = c.Card.Id });
            }
            else
            {
                p.Cash += amount;
                c.Ctx.Emit(new CashFromBank(p.Id, amount, c.Card.Id));
                CardEffects.Fundraise(c.Ctx, p, amount);
            }
        }
    }

    private static int Amount(CardEffectContext c, int playerId)
    {
        RulesConfig rules = c.Rules;
        int total = 0;
        foreach (PropertyState p in c.State.Properties.Values.Where(p => p.OwnerId == playerId && p.Level > 0))
        {
            total += p.Level == rules.SkyscraperLevel ? c.Card.Int("perSkyscraper")
                : p.Level == rules.HotelLevel ? c.Card.Int("perHotel")
                : p.Level * c.Card.Int("perHouse");
        }
        return total;
    }
}

/// <summary>Collect money from the bank. "everyoneElse" (optional) is what every other player collects.</summary>
public sealed class CollectFromBankEffect : ICardEffect
{
    public string Type => "CollectFromBank";

    public void Validate(CardDefinition card)
    {
        card.Int("amount");
        card.Int("everyoneElse", 0);
    }

    public void Apply(CardEffectContext c)
    {
        Collect(c, c.Player, c.Card.Int("amount"));
        int others = c.Card.Int("everyoneElse", 0);
        foreach (PlayerState p in CardEffects.OthersInTurnOrder(c.State, c.Player.Id))
            Collect(c, p, others);
    }

    private static void Collect(CardEffectContext c, PlayerState p, int amount)
    {
        if (amount <= 0)
            return;
        p.Cash += amount;
        c.Ctx.Emit(new CashFromBank(p.Id, amount, c.Card.Id));
        CardEffects.Fundraise(c.Ctx, p, amount);
    }
}

/// <summary>Block Party: every other player pays you this amount.</summary>
public sealed class CollectFromEachPlayerEffect : ICardEffect
{
    public string Type => "CollectFromEachPlayer";

    public void Validate(CardDefinition card) => card.Int("amount");

    public void Apply(CardEffectContext c)
    {
        // Pushed in reverse, so the next player in turn order pays first.
        foreach (PlayerState p in Enumerable.Reverse(CardEffects.OthersInTurnOrder(c.State, c.Player.Id)))
            c.Ctx.Push(new PaymentStep { FromId = p.Id, ToId = c.Player.Id, Amount = c.Card.Int("amount"), Reason = PaymentReason.Card, CardId = c.Card.Id });
    }
}

/// <summary>Lottery: everyone who can puts in the stake and rolls 1 die. The highest roll takes the pot. Ties roll again.</summary>
public sealed class LotteryEffect : ICardEffect
{
    public string Type => "Lottery";

    public void Validate(CardDefinition card) => card.Int("stake");

    public void Apply(CardEffectContext c)
    {
        int stake = c.Card.Int("stake");
        var players = CardEffects.EveryoneFrom(c.State, c.Player.Id).Where(p => p.Cash >= stake).ToList();
        if (players.Count == 0)
            return;
        foreach (PlayerState p in players)
            p.Cash -= stake;
        int pot = stake * players.Count;
        var entrants = players.Select(p => p.Id).ToList();

        while (players.Count > 1)
        {
            var rolls = players.Select(p => new LotteryRoll(p.Id, c.Ctx.Dice.RollDie())).ToList();
            c.Ctx.Emit(new LotteryRolled(rolls));
            int best = rolls.Max(r => r.Die);
            players = players.Where(p => rolls.Single(r => r.PlayerId == p.Id).Die == best).ToList();
        }
        players[0].Cash += pot;
        c.Ctx.Emit(new LotteryWon(players[0].Id, pot, stake, entrants));
    }
}

/// <summary>Rush Hour: everyone moves back. Nobody resolves where they land or collects GO. Players in Jail stay put.</summary>
public sealed class MoveEveryoneBackEffect : ICardEffect
{
    public string Type => "MoveEveryoneBack";

    public void Validate(CardDefinition card) => card.Int("spaces");

    public void Apply(CardEffectContext c)
    {
        foreach (PlayerState p in CardEffects.EveryoneFrom(c.State, c.Player.Id).Where(p => !p.InJail))
            Movement.MoveBackward(c.Ctx, p, c.Card.Int("spaces"));
    }
}

/// <summary>Road Work: next turn you roll only 1 die, unless you spend "cost" Momentum tokens.</summary>
public sealed class RoadWorkEffect : ICardEffect
{
    public string Type => "RoadWork";

    public void Validate(CardDefinition card) => card.Int("cost");

    public void Apply(CardEffectContext c)
    {
        c.Player.RoadWorkCost = c.Card.Int("cost");
        c.Ctx.Emit(new RoadWorkStarted(c.Player.Id));
    }
}

/// <summary>Gain Momentum tokens. "everyoneElse" (optional) is what every other player gains.</summary>
public sealed class GainMomentumEffect : ICardEffect
{
    public string Type => "GainMomentum";

    public void Validate(CardDefinition card)
    {
        card.Int("amount");
        card.Int("everyoneElse", 0);
    }

    public void Apply(CardEffectContext c)
    {
        MomentumRules.Gain(c.Ctx, c.Player, c.Card.Int("amount"), c.Card.Name);
        int others = c.Card.Int("everyoneElse", 0);
        if (others > 0)
        {
            foreach (PlayerState p in CardEffects.OthersInTurnOrder(c.State, c.Player.Id))
                MomentumRules.Gain(c.Ctx, p, others, c.Card.Name);
        }
    }
}

/// <summary>Advance to a space, collecting GO if you pass it, and resolve the space (card moves count as landing).</summary>
public sealed class AdvanceToEffect : ICardEffect
{
    public string Type => "AdvanceTo";

    public void Validate(CardDefinition card) => card.Setting("space");

    public void Apply(CardEffectContext c)
    {
        int target = c.State.IndexOf(c.Card.Setting("space"));
        int spaces = (target - c.Player.Position + c.State.Board.Count) % c.State.Board.Count;
        if (spaces == 0)
            return;
        // Resolve first, then move, like a normal move: anything passing GO pushes on top and goes first.
        c.Ctx.Push(new ResolveSpaceStep { PlayerId = c.Player.Id });
        Movement.MoveForward(c.Ctx, c.Player, spaces);
    }
}

/// <summary>Go straight to Jail (Section 14.1).</summary>
public sealed class GoToJailEffect : ICardEffect
{
    public string Type => "GoToJail";
    public void Apply(CardEffectContext c) => Movement.SendToJail(c.Ctx, c.Player, $"drew {c.Card.Name}");
}

/// <summary>Emergency Session: an Emergency City Council session now. The player who drew it sets the agenda (Section 11.1).</summary>
public sealed class EmergencySessionEffect : ICardEffect
{
    public string Type => "EmergencySession";
    public void Apply(CardEffectContext c) => c.Ctx.Push(new CouncilStep { Emergency = true, TriggeredById = c.Player.Id });
}

// ---------------------------------------------------------- Opportunity effects

/// <summary>Rent Strike: when you owe rent, multiply it (0.5 = pay half).</summary>
public sealed class RentMultiplierEffect : ICardEffect
{
    public string Type => "RentMultiplier";

    public void Validate(CardDefinition card) => card.Number("multiplier");

    /// <summary>Rent Strike is played against the owner who is losing rent.</summary>
    public int? Victim(CardEffectContext c) => (c.Parent as PaymentStep)?.ToId;

    public void Apply(CardEffectContext c)
    {
        if (c.Parent is not PaymentStep { Reason: PaymentReason.Rent } payment)
            return;
        int before = payment.Amount;
        payment.Amount = Math.Max(1, (int)Math.Floor(before * c.Card.Number("multiplier")));
        c.Ctx.Emit(new RentReduced(c.Player.Id, payment.SpaceId!, before, payment.Amount));
    }
}

/// <summary>Insurance: cancels a card played against you. The Reaction window does the work, so there's nothing to apply.</summary>
public sealed class CancelCardEffect : ICardEffect
{
    public string Type => "CancelCard";
    public void Apply(CardEffectContext c) { }
}

/// <summary>
/// Detour: cancels the other player's Nudge (the token stays spent), then moves their piece
/// a space forward or back from what they rolled. The card's Number is how far that is from
/// where they would have landed, so a Detour that would land them where their Nudge did isn't offered.
/// </summary>
public sealed class DetourEffect : ICardEffect
{
    public string Type => "Detour";

    public void Validate(CardDefinition card) => card.Int("spaces");

    public IEnumerable<PlayCard> Plays(EngineContext ctx, PlayerState player, CardDefinition card)
    {
        if (CardRules.CurrentTurn(ctx) is not { } turn || turn.PlayerId == player.Id)
            return [];
        int spaces = card.Int("spaces");
        int rolled = turn.Die1 + turn.Die2;
        // The move must still be at least 1 space forward, like a Nudge.
        return new[] { rolled - spaces, rolled + spaces }
            .Where(total => total >= 1 && total != turn.PlannedSpaces)
            .Select(total => new PlayCard(card.Id, TargetPlayerId: turn.PlayerId, Number: total - turn.PlannedSpaces));
    }

    public int? Victim(CardEffectContext c) => (c.Parent as TurnStep)?.PlayerId;

    public void Apply(CardEffectContext c)
    {
        if (c.Parent is not TurnStep turn)
            return;
        turn.DetourSpaces = c.Play.Number;
        c.Ctx.Emit(new Detoured(c.Player.Id, turn.PlayerId, c.Play.Number, CancelledNudge: turn.NudgeSpaces != 0));
    }
}

/// <summary>Shortcut: instead of rolling, move up to "maxSpaces" ahead. Never doubles.</summary>
public sealed class ShortcutEffect : ICardEffect
{
    public string Type => "Shortcut";
    public bool ReplacesRoll => true;

    public void Validate(CardDefinition card) => card.Int("maxSpaces");

    public IEnumerable<PlayCard> Plays(EngineContext ctx, PlayerState player, CardDefinition card) =>
        Enumerable.Range(1, card.Int("maxSpaces")).Select(n => new PlayCard(card.Id, Number: n));

    public void Apply(CardEffectContext c)
    {
        if (c.Parent is TurnStep turn)
            turn.TakeShortcut(c.Ctx, c.Player, c.Play.Number);
    }
}

/// <summary>Hostile Bid: force an auction on an eligible opponent property (Section 8.6).</summary>
public sealed class ForceAuctionEffect : ICardEffect
{
    public string Type => "ForceAuction";

    public IEnumerable<PlayCard> Plays(EngineContext ctx, PlayerState player, CardDefinition card) =>
        ctx.State.Board
            .Where(s => ctx.State.Properties.TryGetValue(s.Id, out var p) && p.OwnerId != null && p.OwnerId != player.Id
                && AuctionStep.CanForceAuction(ctx, s.Id))
            .Select(s => new PlayCard(card.Id, TargetSpaceId: s.Id));

    public int? Victim(CardEffectContext c) => c.State.Properties[c.Play.TargetSpaceId!].OwnerId;

    public void Apply(CardEffectContext c)
    {
        string propertyId = c.Play.TargetSpaceId!;
        if (!AuctionStep.CanForceAuction(c.Ctx, propertyId))
            return;
        c.Ctx.Push(new AuctionStep
        {
            PropertyId = propertyId,
            Kind = AuctionKind.Forced,
            TieBreakStartId = c.Player.Id,
            OwnerId = c.State.Properties[propertyId].OwnerId,
        });
    }
}

/// <summary>Construction Permit: this turn, build extra levels past the build limit, and every level is cheaper.</summary>
public sealed class ConstructionPermitEffect : ICardEffect
{
    public string Type => "ConstructionPermit";

    public void Validate(CardDefinition card)
    {
        card.Int("extraLevels");
        card.Int("discountPercent");
    }

    public void Apply(CardEffectContext c)
    {
        c.Player.ExtraBuildsThisTurn += c.Card.Int("extraLevels");
        c.Player.BuildDiscountThisTurn = Math.Max(c.Player.BuildDiscountThisTurn, c.Card.Int("discountPercent"));
    }
}

/// <summary>Get Out of Jail Free: leave Jail, or skip the 1-die Hospital recovery turn.</summary>
public sealed class GetOutOfJailFreeEffect : ICardEffect
{
    public string Type => "GetOutOfJailFree";
    public bool WorksInJail => true;

    public IEnumerable<PlayCard> Plays(EngineContext ctx, PlayerState player, CardDefinition card)
    {
        TurnStep? turn = CardRules.CurrentTurn(ctx);
        bool recoveringBeforeRoll = turn is { RecoveringThisTurn: true, Stage: TurnStage.ChooseMove } && turn.PlayerId == player.Id;
        return player.InJail || recoveringBeforeRoll ? [new PlayCard(card.Id)] : [];
    }

    public void Apply(CardEffectContext c)
    {
        if (c.Parent is not TurnStep turn)
            return;
        if (c.Player.InJail)
            turn.LeaveJailFree(c.Ctx, c.Player);
        else
        {
            turn.RecoveringThisTurn = false;
            c.Ctx.Emit(new RecoverySkipped(c.Player.Id));
        }
    }
}

/// <summary>
/// Showdown card (decision #164): call any player with cash to a Showdown from anywhere. The challenger rolls a hand
/// first, then decides whether to go ahead with it.
/// </summary>
public sealed class ShowdownEffect : ICardEffect
{
    public string Type => "Showdown";

    public IEnumerable<PlayCard> Plays(EngineContext ctx, PlayerState player, CardDefinition card) =>
        player.Cash <= 0 ? []
        : CardEffects.OthersInTurnOrder(ctx.State, player.Id).Where(p => p.Cash > 0).Select(p => new PlayCard(card.Id, TargetPlayerId: p.Id));

    public void Apply(CardEffectContext c)
    {
        int opponentId = c.Play.TargetPlayerId!.Value;
        c.Ctx.Push(new ShowdownStep { ChallengerId = c.Player.Id, OpponentId = opponentId, Stake = ShowdownStep.StakeFor(c.Ctx, c.Player.Id, opponentId), Confirmed = false });
    }
}

/// <summary>Tip-Off: look at the top Event cards and put them back in any order.</summary>
public sealed class ArrangeEventsEffect : ICardEffect
{
    public string Type => "ArrangeEvents";

    public void Validate(CardDefinition card) => card.Int("count");

    public IEnumerable<PlayCard> Plays(EngineContext ctx, PlayerState player, CardDefinition card) =>
        CardRules.CanDraw(ctx.State, CardDeck.Event) ? [new PlayCard(card.Id)] : [];

    public void Apply(CardEffectContext c)
    {
        CardRules.EnsureCards(c.Ctx, CardDeck.Event, c.Card.Int("count"));
        int count = Math.Min(c.Card.Int("count"), c.State.EventDeck.Count);
        if (count > 0)
            c.Ctx.Push(new ArrangeEventsStep { PlayerId = c.Player.Id, Count = count });
    }
}

/// <summary>Grand Opening: one of your properties earns multiplied rent until your next turn.</summary>
public sealed class PropertyRentBoostEffect : ICardEffect
{
    public string Type => "PropertyRentBoost";

    public void Validate(CardDefinition card) => card.Int("multiplier");

    public IEnumerable<PlayCard> Plays(EngineContext ctx, PlayerState player, CardDefinition card) =>
        ctx.State.Board
            .Where(s => ctx.State.Properties.TryGetValue(s.Id, out var p) && p.IsOwnedOutright(player.Id) && !p.IsMortgaged)
            .Select(s => new PlayCard(card.Id, TargetSpaceId: s.Id));

    public void Apply(CardEffectContext c)
    {
        string propertyId = c.Play.TargetSpaceId!;
        c.State.PropertyBoosts.Add(new PropertyBoost(propertyId, c.Player.Id, c.Card.Int("multiplier")));
        c.Ctx.Emit(new RentBoostStarted(c.Player.Id, propertyId, c.Card.Int("multiplier")));
    }
}

/// <summary>Inside Source: secretly look at one hidden Government tile.</summary>
public sealed class PeekTileEffect : ICardEffect
{
    public string Type => "PeekTile";

    public IEnumerable<PlayCard> Plays(EngineContext ctx, PlayerState player, CardDefinition card) =>
        ctx.State.Board
            .Where(s => s.Kind == SpaceKind.GovernmentTile && !s.IsRevealed && !player.KnownTiles.Contains(s.Id))
            .Select(s => new PlayCard(card.Id, TargetSpaceId: s.Id));

    public void Apply(CardEffectContext c)
    {
        TileRules.Peek(c.Ctx, c.Player, c.Play.TargetSpaceId!);
    }
}

/// <summary>
/// Lobbyist: extra influence in a City Council vote. It isn't played like other cards: you tick it as part of
/// your vote (see <see cref="CastVote"/>), so it never shows up as a play on your own turn.
/// </summary>
public sealed class AddInfluenceEffect : ICardEffect
{
    public string Type => "AddInfluence";

    public void Validate(CardDefinition card) => card.Int("amount");

    public IEnumerable<PlayCard> Plays(EngineContext ctx, PlayerState player, CardDefinition card) => [];

    public void Apply(CardEffectContext c) { }
}
