using CityTycoon.Engine.Actions;
using CityTycoon.Engine.Content;
using CityTycoon.Engine.Events;
using CityTycoon.Engine.Rules;
using CityTycoon.Engine.State;
using CityTycoon.Engine.Steps;
using CityTycoon.Engine.Views;

namespace CityTycoon.AI;

/// <summary>
/// Trading (Section 15.9): offering, accepting, declining and counter-offers.
/// Every choice uses the same sum, <see cref="Gain"/>: how many dollars better
/// off the bot would be after the trade.
/// </summary>
public partial class EasyBot
{
    /// <summary>
    /// Every bot judges a color group in a trade by the rent it can earn before the game
    /// ends (see <see cref="GroupEarnings"/>). This scales what handing a group to someone
    /// else costs the bot: the Easy bot is extra cautious (1.5), Normal and Hard use 1.
    /// </summary>
    protected virtual double SetCaution => 1.25;

    /// <summary>How many rounds ahead the bot looks when it guesses what a color group will earn.</summary>
    private const int GroupHorizon = 10;

    /// <summary>
    /// Finds a color group where it owns every property but one, and offers
    /// cash for the missing one: 150% of assessed value, or more
    /// when the group would pay off well, as long as it still comes out ahead.
    /// </summary>
    private TradeOffer? SetCompletingTrade(PlayerView view)
    {
        int myId = view.ViewerId;
        foreach (var group in content.Groups.Keys)
        {
            var members = content.GroupMembers(group).ToList();
            var others = members.Where(id => view.Properties[id].OwnerId != myId || view.Properties[id].IsPending).ToList();
            if (others.Count != 1)
                continue;

            PropertyInfo target = view.Properties[others[0]];
            if (target.OwnerId is not int ownerId || target.IsPending || target.IsMortgaged
                || view.Player(ownerId).IsBankrupt
                || members.Any(id => view.Properties[id].Level > 0))
            {
                continue;
            }

            int assessed = content.Property(target.Id).AssessedValue;
            int price = (int)(assessed * 1.5) / 10 * 10;
            // A share of what the group should earn, so the owner has a reason to sell.
            double earns = GroupEarnings(view, myId, group, view.Me.Cash - price);
            price = Math.Max(price, (int)(assessed + earns * 0.35) / 10 * 10);
            // Don't pester: wait a few rounds after an offer for this property, then only offer more than last time.
            if (LastOffer(view, ownerId, target.Id) is (int round, int lastPrice))
            {
                if (view.Round - round < RepeatOfferRounds)
                    continue;
                price = Math.Max(price, (int)Math.Ceiling(lastPrice * 1.15 / 10) * 10);
            }
            if (view.Me.Cash - price < Reserve(view))
                continue;
            var offer = new TradeOffer(myId, ownerId, price, 0, [], [target.Id]);
            if (Gain(view, offer.Reversed()) <= 0)
                continue;
            return offer;
        }
        return null;
    }

    /// <summary>After offering for a property, the bot waits this many rounds before offering for it again.</summary>
    private const int RepeatOfferRounds = 3;

    /// <summary>
    /// The round and cash of this bot's last offer to that player for that property,
    /// read from the game log (so it is the same after loading a save), or null if it never offered.
    /// If the trade had happened the bot would own the property, so any offer found didn't work out.
    /// </summary>
    private static (int Round, int Price)? LastOffer(PlayerView view, int ownerId, string propertyId)
    {
        (int, int)? last = null;
        int round = 0;
        foreach (var e in view.Log)
        {
            if (e is TurnStarted t)
                round = t.Round;
            else if (e is TradeProposed { Offer: var o } && o.FromId == view.ViewerId && o.ToId == ownerId && o.GetProperties.Contains(propertyId))
                last = (round, o.GiveCash);
        }
        return last;
    }

    /// <summary>Accepts when the trade leaves it better off and it keeps some cash back.</summary>
    private bool IsGoodForMe(PlayerView view, TradeOffer offer) =>
        view.Me.Cash - offer.GetCash >= Reserve(view) / 2 && Gain(view, offer) > 0;

    /// <summary>
    /// How much better off the bot would be, in dollars, if it accepted this offer.
    /// The offer is written from the proposer's side; this bot is the receiver.
    /// </summary>
    private double Gain(PlayerView view, TradeOffer offer) => Sums(view, offer).Gain;

    /// <summary>
    /// The parts of <see cref="Gain"/>, kept apart so the bot can explain itself:
    /// cash, properties and cards at face value, what color groups it would complete
    /// (or break up) are worth to it, and what the groups the other player completes
    /// would cost it. The names are the groups' names, for the explanation.
    /// </summary>
    private sealed record TradeSums(double Basic, double Mine, double Theirs, string? MyGroup, string? TheirGroup)
    {
        public double Gain => Basic + Mine - Theirs;
    }

    private TradeSums Sums(PlayerView view, TradeOffer offer)
    {
        int me = view.ViewerId;
        int them = offer.FromId;
        double basic = offer.GiveCash - offer.GetCash
            + offer.GiveProperties.Sum(id => Value(view, id))
            - offer.GetProperties.Sum(id => Value(view, id))
            + offer.GiveCards.Sum(id => CardWorth(view, id))
            - offer.GetCards.Sum(id => CardWorth(view, id))
            // The bank's 10% fee on any mortgaged property the bot would receive.
            - MortgageFees(view, offer.GiveProperties);
        string? myGroup = CompletedGroup(view, me, add: offer.GiveProperties, remove: offer.GetProperties);
        string? theirGroup = CompletedGroup(view, them, add: offer.GetProperties, remove: offer.GiveProperties);

        int myCashAfter = view.Me.Cash + offer.GiveCash - offer.GetCash;
        int theirCashAfter = view.Player(them).Cash - offer.GiveCash + offer.GetCash;
        // What they earn from a new group is mostly what the bot (and the others) pay them,
        // and it matters more when they are already ahead.
        return new TradeSums(basic,
            GroupChange(view, me, add: offer.GiveProperties, remove: offer.GetProperties, myCashAfter),
            GroupChange(view, them, add: offer.GetProperties, remove: offer.GiveProperties, theirCashAfter) * LeaderFactor(view, them) * SetCaution,
            myGroup, theirGroup);
    }

    /// <summary>The name of a color group this trade would complete for the player, or null.</summary>
    private string? CompletedGroup(PlayerView view, int playerId, IReadOnlyList<string> add, IReadOnlyList<string> remove)
    {
        bool Owns(string id) => add.Contains(id) || (view.Properties[id].OwnerId == playerId && !view.Properties[id].IsPending && !remove.Contains(id));
        return add.Select(id => content.Property(id).Group).OfType<string>().Distinct()
            .Where(group => content.GroupMembers(group).All(Owns))
            .Select(group => content.Groups[group].Name).FirstOrDefault();
    }

    /// <summary>
    /// Accept a good offer. Otherwise send back terms it would take, if it can and counters are left,
    /// or decline. Either way it says why in a short sentence, so the person knows what to change.
    /// </summary>
    private GameAction RespondToTrade(PlayerView view, PendingDecision decision)
    {
        TradeOffer offer = decision.Trade!;
        if (IsGoodForMe(view, offer))
            return new AcceptTrade();

        string problem = WhatsWrong(view, offer);
        if (decision.CountersLeft == 0)
            return new DeclineTrade($"{problem} We're out of counter-offers on this trade.");
        var (counter, whyNot) = CounterOffer(view, offer);
        if (counter != null)
            return new CounterTrade(counter, $"{problem} Here's what I'd take.");
        return new DeclineTrade($"{problem} {whyNot}");
    }

    /// <summary>One or two plain sentences on why the bot won't take this offer as it is.</summary>
    private string WhatsWrong(PlayerView view, TradeOffer offer)
    {
        if (view.Me.Cash - offer.GetCash < Reserve(view) / 2)
            return $"I can't pay ${offer.GetCash} and still keep enough cash for rent.";
        TradeSums sums = Sums(view, offer);
        int short_ = Math.Max(10, (int)Math.Ceiling(-sums.Gain / 10) * 10);
        string text = $"By my sums I'd be about ${short_} worse off.";
        if (sums.TheirGroup != null && sums.Theirs >= 10)
            text += $" It gives you the whole {sums.TheirGroup} set, which I think costs me about ${Round10(sums.Theirs)}.";
        if (sums.MyGroup != null && sums.Mine >= 10)
            text += $" Finishing {sums.MyGroup} is worth about ${Round10(sums.Mine)} to me.";
        return text;
    }

    private static int Round10(double dollars) => (int)Math.Round(dollars / 10) * 10;

    /// <summary>
    /// A counter-offer the bot would accept itself, with the cash moved so it comes out
    /// ahead by a small margin. It uses the same sums as accepting, so countering never
    /// makes a bad trade easier to get. First it keeps the properties and cards as they
    /// are; if that can't work (for example the bot can't afford it), it tries again
    /// without the properties the other player asked it for.
    /// Returns the counter, or null and the reason why not.
    /// </summary>
    private (TradeOffer? Counter, string WhyNot) CounterOffer(PlayerView view, TradeOffer offer)
    {
        var (counter, whyNot) = CashCounter(view, offer);
        if (counter == null && offer.GetProperties.Count > 0 && offer.GiveProperties.Count > 0)
        {
            // Keep what it was offered, but don't hand over the properties it was asked for.
            var (smaller, _) = CashCounter(view, offer with { GetProperties = [] }, shareSurplus: true);
            if (smaller != null)
                return (smaller, "");
        }
        return (counter, whyNot);
    }

    /// <param name="shareSurplus">
    /// When the trade is already good for the bot (it dropped what it was asked for),
    /// offer half of what it would gain as cash, so the counter is a fair deal and not "give me it for nothing".
    /// </param>
    private (TradeOffer? Counter, string WhyNot) CashCounter(PlayerView view, TradeOffer offer, bool shareSurplus = false)
    {
        int them = offer.FromId;

        // The margin: 5% of everything changing hands, at least $10, so the bot doesn't counter for a break-even trade.
        double traded = offer.GiveCash + offer.GetCash
            + offer.GiveProperties.Concat(offer.GetProperties).Sum(id => Value(view, id));
        double margin = Math.Max(10, traded * 0.05);

        // From the bot's side: it gives what it was asked for and gets what it was offered.
        // First it pays less cash, then it asks for more. Asking for more cash also changes
        // what both players can build with, so it tries a few times.
        TradeOffer counter = offer.Reversed();
        for (int attempt = 0; attempt < 4; attempt++)
        {
            int needed = (int)Math.Ceiling((margin - Gain(view, counter.Reversed())) / 10) * 10;
            if (needed < 0 && shareSurplus && attempt == 0)
            {
                int extra = Math.Min(-needed / 2, view.Me.Cash - counter.GiveCash - Reserve(view)) / 10 * 10;
                if (extra > 0)
                    counter = counter with { GiveCash = counter.GiveCash + extra };
                continue;
            }
            if (needed <= 0)
                break;
            int cut = Math.Min(counter.GiveCash, needed);
            counter = counter with { GiveCash = counter.GiveCash - cut, GetCash = counter.GetCash + needed - cut };
        }

        // Cash for cash is pointless: there must be a property or a card in it.
        if (counter.GiveProperties.Count + counter.GetProperties.Count + counter.GiveCards.Count + counter.GetCards.Count == 0)
            return (null, "I only want to trade if a property or a card changes hands.");
        // Both sides must be able to afford it, including the bank's fee on mortgaged properties.
        if (view.Player(them).Cash < counter.GetCash + MortgageFees(view, counter.GiveProperties))
            return (null, $"I'd need about ${counter.GetCash} from you, and you don't have that much cash.");
        if (view.Me.Cash - counter.GiveCash + counter.GetCash - MortgageFees(view, counter.GetProperties) < Reserve(view) / 2
            || !IsGoodForMe(view, counter.Reversed()))
        {
            return (null, "I can't find a price that works for both of us right now.");
        }
        return (counter, "");
    }

    private int MortgageFees(PlayerView view, IEnumerable<string> propertyIds) =>
        propertyIds.Where(id => view.Properties[id].IsMortgaged)
                   .Sum(id => MoneyRules.MortgageTransferFee(content, content.Property(id)));

    /// <summary>
    /// A property's assessed value. A mortgaged one is worth what is left after paying to
    /// lift the mortgage (the mortgage plus 10%), because that's what it takes to earn rent from it again.
    /// </summary>
    private double Value(PlayerView view, string id)
    {
        PropertyDefinition def = content.Property(id);
        return view.Properties[id].IsMortgaged ? def.AssessedValue - MoneyRules.UnmortgageCost(content, def) : def.AssessedValue;
    }

    // ----- What a color group is really worth -----

    /// <summary>
    /// What the player would earn from groups this trade completes for them, minus
    /// what they'd lose from groups it breaks up.
    /// </summary>
    private double GroupChange(PlayerView view, int playerId, IReadOnlyList<string> add, IReadOnlyList<string> remove, int cashAfter)
    {
        bool OwnsNow(string id) => view.Properties[id].OwnerId == playerId && !view.Properties[id].IsPending;
        bool OwnsAfter(string id) => add.Contains(id) || (OwnsNow(id) && !remove.Contains(id));
        double change = 0;
        foreach (string group in add.Concat(remove).Select(id => content.Property(id).Group).OfType<string>().Distinct())
        {
            var members = content.GroupMembers(group).ToList();
            bool before = members.All(OwnsNow);
            bool after = members.All(OwnsAfter);
            if (after && !before)
                change += EarningsOnceLifted(view, playerId, group, cashAfter);
            else if (before && !after)
                change -= EarningsOnceLifted(view, playerId, group, view.Player(playerId).Cash);
        }
        return change;
    }

    /// <summary>
    /// What a whole group earns once any mortgages in it are lifted. A mortgaged piece earns no rent
    /// and stops the double rent and building, so the owner must pay to lift it first, and that cash
    /// can't go on houses. If they can't afford to lift it, the group earns nothing for now.
    /// </summary>
    private double EarningsOnceLifted(PlayerView view, int playerId, string group, int cash)
    {
        int lift = content.GroupMembers(group).Where(id => view.Properties[id].IsMortgaged)
                          .Sum(id => MoneyRules.UnmortgageCost(content, content.Property(id)));
        return cash < lift ? 0 : GroupEarnings(view, playerId, group, cash - lift);
    }

    /// <summary>
    /// A rough guess at the rent a whole color group earns its owner over the next
    /// few rounds (or until a Timed game ends). It looks at:
    /// how many houses the owner could build with the cash they'd have left (plus a GO
    /// salary if they're about to pass GO), the most the zoning allows, a Building
    /// Moratorium, Rent Control, and how many players are left to land there.
    /// Each player lands on a given space about once every lap, so roughly
    /// (spaces in the group / spaces on the board) times per turn.
    /// </summary>
    protected double GroupEarnings(PlayerView view, int ownerId, string group, int cashAfter)
    {
        var members = content.GroupMembers(group).ToList();
        PropertyDefinition first = content.Property(members[0]);
        string? district = view.Spaces.First(s => s.Id == members[0]).DistrictId;

        double rounds = view.Mode == GameMode.Timed ? Math.Clamp(view.MaxRounds - view.Round + 1, 0, GroupHorizon) : GroupHorizon;
        if (rounds <= 0)
            return 0;

        // Cash to build with: keep $150 back for rent and taxes.
        PlayerInfo owner = view.Player(ownerId);
        int spacesToGo = (view.Spaces.Count - owner.Position) % view.Spaces.Count;
        double budget = Math.Max(0, cashAfter - 150) + (spacesToGo is > 0 and <= 14 ? content.Rules.GoSalary : 0);
        int maxLevel = district != null && view.Zoning.TryGetValue(district, out ZoningLevel zoning)
            ? Math.Min(3, content.Rules.MaxBuildingLevel(zoning))
            : 3;
        // Three houses each is where rent pays best for what they cost, so the guess stops there.
        int levels = first.HouseCost > 0 ? Math.Min(members.Count * maxLevel, (int)(budget / first.HouseCost)) : 0;

        // Nobody can build during a Building Moratorium, so building waits and earns for fewer rounds.
        int moratoriumRounds = view.ActiveOrdinances
            .Where(o => content.Ordinance(o.OrdinanceId).EffectType == "BuildingMoratorium")
            .Select(o => o.RoundsLeft ?? GroupHorizon).DefaultIfEmpty(0).Max();

        double rentPercent = 100 + view.ActiveOrdinances
            .Where(o => o.DistrictId == district && content.Ordinance(o.OrdinanceId).EffectType == "DistrictRent")
            .Sum(o => content.Ordinance(o.OrdinanceId).Int("percent"));

        double AverageRent(int totalLevels)
        {
            double sum = 0;
            for (int i = 0; i < members.Count; i++)
            {
                int level = totalLevels / members.Count + (i < totalLevels % members.Count ? 1 : 0);
                var rents = content.Property(members[i]).Rents!;
                sum += level == 0 ? rents[0] * 2 : rents[level];
            }
            return sum / members.Count * rentPercent / 100;
        }

        int payers = view.Players.Count(p => p.Id != ownerId && !p.IsBankrupt);
        double landingsPerRound = (double)members.Count / view.Spaces.Count * payers;
        double waiting = Math.Min(rounds, moratoriumRounds);
        return landingsPerRound * (AverageRent(0) * waiting + AverageRent(levels) * (rounds - waiting));
    }

    /// <summary>
    /// How much a gain for this player hurts the bot: more when they're ahead on net
    /// worth, less when they're behind (from 0.7 to 1.4 times).
    /// </summary>
    private double LeaderFactor(PlayerView view, int playerId)
    {
        var active = view.Players.Where(p => !p.IsBankrupt).ToList();
        double average = active.Average(p => (double)p.NetWorth);
        return average <= 0 ? 1 : Math.Clamp(view.Player(playerId).NetWorth / average, 0.7, 1.4);
    }

    // ----- Cards in trades -----

    /// <summary>
    /// Rough dollar values for Opportunity cards, for trading. Some depend on the
    /// moment: Get Out of Jail Free is worth more when the bot is in Jail or close to
    /// Go To Jail, Audit is worth 10% of the richest opponent's cash. These are first
    /// guesses; a proper card evaluation is still to come.
    /// </summary>
    protected virtual double CardWorth(PlayerView view, string cardId)
    {
        CardDefinition card = content.Card(cardId);
        return card.EffectType switch
        {
            "GetOutOfJailFree" => 40 + (view.Me.InJail ? view.Me.Bail : 0) + (CloseToGoToJail(view) ? 40 : 0),
            "CancelCard" => 80,
            "RentMultiplier" => 70,
            "PropertyRentBoost" => Math.Max(30, view.Properties.Values
                .Where(p => p.OwnerId == view.ViewerId).Select(p => EstimateRent(view, p.Id, 7)).DefaultIfEmpty(0).Max()),
            "GainMomentum" => TokenValue * card.Int("amount"),
            "PayPercentOfCash" => view.Players.Where(p => p.Id != view.ViewerId && !p.IsBankrupt)
                .Select(p => p.Cash * card.Int("percent") / 100.0).DefaultIfEmpty(0).Max(),
            "ConstructionPermit" => 50,
            "Shortcut" => 40,
            "ForceAuction" => 40,
            "AddInfluence" => 40,
            "Detour" => 30,
            "ArrangeEvents" => 25,
            "PeekTile" => 15,
            "DiceDuel" => 10,
            _ => 20,
        };
    }

    private static bool CloseToGoToJail(PlayerView view)
    {
        for (int step = 1; step <= 12; step++)
        {
            if (view.Spaces[(view.Me.Position + step) % view.Spaces.Count].Kind == SpaceKind.GoToJail)
                return true;
        }
        return false;
    }
}
