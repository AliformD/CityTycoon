using CityTycoon.Engine.Actions;
using CityTycoon.Engine.Content;
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
    /// False for the Easy bot: a color group it hands over or gets counts as half
    /// the group's value, and it never counters a trade that hands someone a group.
    /// True for Normal and Hard: a group is worth the rent it can earn before the
    /// game ends, given the owner's cash to build, time left, and ordinances.
    /// </summary>
    protected virtual bool JudgesGroupsCarefully => false;

    /// <summary>How many rounds ahead the bot looks when it guesses what a color group will earn.</summary>
    private const int GroupHorizon = 10;

    /// <summary>
    /// Finds a color group where it owns every property but one, and offers
    /// cash for the missing one: 150% of assessed value, or (careful bots) more
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
            if (JudgesGroupsCarefully)
            {
                // A share of what the group should earn, so the owner has a reason to sell.
                double earns = GroupEarnings(view, myId, group, view.Me.Cash - price);
                price = Math.Max(price, (int)(assessed + earns * 0.35) / 10 * 10);
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

    /// <summary>Accepts when the trade leaves it better off and it keeps some cash back.</summary>
    private bool IsGoodForMe(PlayerView view, TradeOffer offer) =>
        view.Me.Cash - offer.GetCash >= Reserve(view) / 2 && Gain(view, offer) > 0;

    /// <summary>
    /// How much better off the bot would be, in dollars, if it accepted this offer.
    /// The offer is written from the proposer's side; this bot is the receiver.
    /// </summary>
    private double Gain(PlayerView view, TradeOffer offer)
    {
        int me = view.ViewerId;
        int them = offer.FromId;
        double gain = offer.GiveCash - offer.GetCash
            + offer.GiveProperties.Sum(id => Value(view, id))
            - offer.GetProperties.Sum(id => Value(view, id))
            + offer.GiveCards.Sum(id => CardWorth(view, id))
            - offer.GetCards.Sum(id => CardWorth(view, id));

        if (!JudgesGroupsCarefully)
        {
            gain += GroupBonus(view, me, add: offer.GiveProperties, remove: offer.GetProperties);
            gain -= GroupBonus(view, them, add: offer.GetProperties, remove: offer.GiveProperties);
            return gain;
        }

        int myCashAfter = view.Me.Cash + offer.GiveCash - offer.GetCash;
        int theirCashAfter = view.Player(them).Cash - offer.GiveCash + offer.GetCash;
        gain += GroupChange(view, me, add: offer.GiveProperties, remove: offer.GetProperties, myCashAfter);
        // What they earn from a new group is mostly what the bot (and the others) pay them,
        // and it matters more when they are already ahead.
        gain -= GroupChange(view, them, add: offer.GetProperties, remove: offer.GiveProperties, theirCashAfter) * LeaderFactor(view, them);
        return gain;
    }

    /// <summary>Accept a good offer. Otherwise send back terms it would take, if it can and counters are left, or decline.</summary>
    private GameAction RespondToTrade(PlayerView view, PendingDecision decision)
    {
        TradeOffer offer = decision.Trade!;
        if (IsGoodForMe(view, offer))
            return new AcceptTrade();
        if (decision.CountersLeft > 0 && CounterOffer(view, offer) is TradeOffer counter)
            return new CounterTrade(counter);
        return new DeclineTrade();
    }

    /// <summary>
    /// A counter-offer the bot would accept itself: the same properties and cards,
    /// with the cash moved so it comes out ahead by a small margin. It uses the same
    /// sums as accepting, so countering never makes a bad trade easier to get.
    /// The Easy bot doesn't counter a trade that would hand the other player a whole
    /// color group: it just declines. Returns null when no counter makes sense.
    /// </summary>
    private TradeOffer? CounterOffer(PlayerView view, TradeOffer offer)
    {
        int me = view.ViewerId;
        int them = offer.FromId;
        if (!JudgesGroupsCarefully && GroupBonus(view, them, add: offer.GetProperties, remove: offer.GiveProperties) > 0)
            return null;

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
            if (needed <= 0)
                break;
            int cut = Math.Min(counter.GiveCash, needed);
            counter = counter with { GiveCash = counter.GiveCash - cut, GetCash = counter.GetCash + needed - cut };
        }

        // Cash for cash is pointless: there must be a property or a card in it.
        if (counter.GiveProperties.Count + counter.GetProperties.Count + counter.GiveCards.Count + counter.GetCards.Count == 0)
            return null;
        // Both sides must be able to afford it, including the bank's fee on mortgaged properties.
        if (view.Player(them).Cash < counter.GetCash + MortgageFees(view, counter.GiveProperties)
            || view.Me.Cash - counter.GiveCash + counter.GetCash - MortgageFees(view, counter.GetProperties) < Reserve(view) / 2
            || !IsGoodForMe(view, counter.Reversed()))
        {
            return null;
        }
        return counter;
    }

    private int MortgageFees(PlayerView view, IEnumerable<string> propertyIds) =>
        propertyIds.Where(id => view.Properties[id].IsMortgaged)
                   .Sum(id => MoneyRules.MortgageTransferFee(content, content.Property(id)));

    private double Value(PlayerView view, string id)
    {
        int assessed = content.Property(id).AssessedValue;
        return view.Properties[id].IsMortgaged ? assessed / 2.0 : assessed;
    }

    /// <summary>Half the group's value, for each color group this trade would complete for the player (the Easy bot's rule).</summary>
    private double GroupBonus(PlayerView view, int playerId, IReadOnlyList<string> add, IReadOnlyList<string> remove)
    {
        bool Owns(string id) => add.Contains(id) || (view.Properties[id].OwnerId == playerId && !remove.Contains(id));
        double bonus = 0;
        foreach (string group in add.Select(id => content.Property(id).Group).OfType<string>().Distinct())
        {
            var members = content.GroupMembers(group).ToList();
            if (members.All(Owns))
                bonus += members.Sum(id => content.Property(id).AssessedValue) / 2.0;
        }
        return bonus;
    }

    // ----- Careful bots: what a color group is really worth -----

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
                change += GroupEarnings(view, playerId, group, cashAfter);
            else if (before && !after)
                change -= GroupEarnings(view, playerId, group, view.Player(playerId).Cash);
        }
        return change;
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
