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
    /// Every bot judges a color group in a trade by what it can earn before the game
    /// ends: rent plus the group's perk (see <see cref="GroupEarnings"/>). This scales what handing a group to someone
    /// else costs the bot: the Easy bot is extra cautious (1.25), Normal and Hard use 1.
    /// </summary>
    protected virtual double SetCaution => 1.25;

    /// <summary>
    /// How much finishing its own color group is worth to the bot, compared with a Balanced bot:
    /// Aggressive bots want sets more and pay more for them (decision #145).
    /// </summary>
    protected double SetAppetite => Personality switch
    {
        BotPersonality.Aggressive => 1.15,
        BotPersonality.Reserved => 0.9,
        _ => 1.0,
    };

    /// <summary>
    /// Where the bot starts haggling, as a share of the most it would pay: an Aggressive bot
    /// opens close to its limit, a Reserved one starts low and makes you work for it.
    /// </summary>
    protected double OpeningShare => Personality switch
    {
        BotPersonality.Aggressive => 0.75,
        BotPersonality.Reserved => 0.55,
        _ => 0.65,
    };

    /// <summary>For the first this many rounds the bot builds only with the cash it has now.</summary>
    private const int NearRounds = 10;

    /// <summary>In a Classic game (no round limit) the bot plans for this many more rounds.</summary>
    private const int ClassicRoundsLeft = 30;

    /// <summary>
    /// Money further ahead is less sure (someone may go broke, a vote may change the rules),
    /// so rent and perks after the first <see cref="NearRounds"/> rounds count this much.
    /// </summary>
    private const double FarDiscount = 0.6;

    /// <summary>Average spaces moved per turn: two dice average 7.</summary>
    private const double SpacesPerTurn = 7;

    /// <summary>Rounds left in the game, counting this one.</summary>
    protected static double RoundsLeft(PlayerView view) =>
        view.Mode == GameMode.Timed ? Math.Max(0, view.MaxRounds - view.Round + 1) : ClassicRoundsLeft;

    /// <summary>
    /// Finds a color group where it owns every property but one, and offers
    /// cash for the missing one. It works out the most it would pay (its limit, see <see cref="Limit"/>)
    /// and opens below that, so there is room to haggle (decision #145).
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

            var ask = new TradeOffer(myId, ownerId, 0, 0, [], [target.Id]);
            int limit = Math.Min(Limit(view, ask).GiveCash, view.Me.Cash - Reserve(view)) / 10 * 10;
            int assessed = content.Property(target.Id).AssessedValue;
            if (limit < assessed)
                continue;
            int price = Math.Max(assessed, (int)(limit * OpeningShare) / 10 * 10);
            // Don't pester: wait a few rounds after an offer for this property, then only offer more than last time.
            if (LastOffer(view, ownerId, target.Id) is (int round, int lastPrice))
            {
                if (view.Round - round < RepeatOfferRounds || lastPrice >= limit)
                    continue;
                price = Math.Max(price, (int)Math.Ceiling(lastPrice * 1.15 / 10) * 10);
            }
            return ask with { GiveCash = Math.Min(price, limit) };
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
        double mine = GroupChange(view, me, add: offer.GiveProperties, remove: offer.GetProperties, myCashAfter);
        return new TradeSums(basic,
            mine > 0 ? mine * SetAppetite : mine,
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
    /// Accept a good offer. Otherwise haggle: send back terms between the two sides, up to the most
    /// it would give, if counters are left, or decline. Either way it says why in a short sentence,
    /// so the person knows what to change.
    /// </summary>
    private GameAction RespondToTrade(PlayerView view, PendingDecision decision)
    {
        TradeOffer offer = decision.Trade!;
        if (IsGoodForMe(view, offer))
            return new AcceptTrade();

        string problem = WhatsWrong(view, offer);
        if (decision.CountersLeft == 0)
            return new DeclineTrade($"{problem} We're out of counter-offers on this trade.");
        var (counter, whyNot) = CounterOffer(view, offer, decision.CountersLeft);
        if (counter != null)
            return new CounterTrade(counter, $"{problem} {whyNot}");
        return new DeclineTrade($"{problem} {whyNot}");
    }

    /// <summary>One or two plain sentences on why the bot won't take this offer as it is.</summary>
    private string WhatsWrong(PlayerView view, TradeOffer offer)
    {
        if (view.Me.Cash - offer.GetCash < Reserve(view) / 2)
            return $"I can't pay ${offer.GetCash} and still keep enough cash for rent.";
        TradeSums sums = Sums(view, offer);
        int ask = offer.GetCash - offer.GiveCash;
        // It doesn't say its limit here, or there would be nothing left to haggle over.
        if (sums.MyGroup != null && sums.Mine >= 10 && ask > 0)
            return $"I want {sums.MyGroup}, but ${ask} is more than I'd pay for it.";
        int short_ = Math.Max(10, (int)Math.Ceiling(-sums.Gain / 10) * 10);
        string text = $"By my sums I'd be about ${short_} worse off.";
        if (sums.TheirGroup != null && sums.Theirs >= 10)
            text += $" It gives you the whole {sums.TheirGroup} set, which I think costs me about ${Round10(sums.Theirs)}.";
        return text;
    }

    private static int Round10(double dollars) => (int)Math.Round(dollars / 10) * 10;

    /// <summary>Cash the bot pays in this offer, written from its side; negative when it is paid.</summary>
    private static int Net(TradeOffer mine) => mine.GiveCash - mine.GetCash;

    private static TradeOffer WithNet(TradeOffer mine, int net) =>
        net >= 0 ? mine with { GiveCash = net, GetCash = 0 } : mine with { GiveCash = 0, GetCash = -net };

    /// <summary>The margin: 5% of everything changing hands, at least $10, so the bot doesn't trade for a break-even deal.</summary>
    private double Margin(PlayerView view, TradeOffer mine) =>
        Math.Max(10, (Math.Abs(Net(mine)) + mine.GiveProperties.Concat(mine.GetProperties).Sum(id => Value(view, id))) * 0.05);

    /// <summary>
    /// The bot's limit: this trade (written from the bot's side) with the cash moved to the most
    /// the bot would pay, or the least it would take, and still come out ahead by the margin.
    /// More cash paid also leaves less to build with, so it can't just subtract: it halves the
    /// range of possible amounts until it finds the line, in $10 steps.
    /// If no amount works, the result still isn't good for the bot, and the caller checks that.
    /// </summary>
    private TradeOffer Limit(PlayerView view, TradeOffer mine)
    {
        bool Works(int net)
        {
            TradeOffer o = WithNet(mine, net);
            return Gain(view, o.Reversed()) >= Margin(view, o);
        }
        // From the most they could pay the bot, to all of the bot's cash.
        int low = -view.Player(mine.ToId).Cash / 10;
        int high = view.Me.Cash / 10;
        if (!Works(low * 10))
            return WithNet(mine, low * 10);
        while (low < high)
        {
            int middle = (low + high + 1) / 2;
            if (Works(middle * 10))
                low = middle;
            else
                high = middle - 1;
        }
        return WithNet(mine, low * 10);
    }

    /// <summary>
    /// The bot's last offer in this negotiation with the same properties and cards, read from the log
    /// (the opening offer or one of its counters), or null if it hasn't made one yet.
    /// </summary>
    private static TradeOffer? MyLastPosition(PlayerView view, TradeOffer mine)
    {
        for (int i = view.Log.Count - 1; i >= 0; i--)
        {
            TradeOffer? o = view.Log[i] switch
            {
                TradeCountered c => c.Counter,
                TradeProposed p => p.Offer,
                _ => null,
            };
            if (o != null && o.FromId == view.ViewerId && SameItems(o, mine))
                return o;
            // Where this negotiation started.
            if (view.Log[i] is TradeProposed)
                return null;
        }
        return null;
    }

    private static bool SameItems(TradeOffer a, TradeOffer b) =>
        a.ToId == b.ToId
        && a.GiveProperties.ToHashSet().SetEquals(b.GiveProperties) && a.GetProperties.ToHashSet().SetEquals(b.GetProperties)
        && a.GiveCards.ToHashSet().SetEquals(b.GiveCards) && a.GetCards.ToHashSet().SetEquals(b.GetCards);

    /// <summary>
    /// A counter-offer that meets the other player partway (decision #145). The bot works out its
    /// limit, then offers halfway between its last offer and what they asked, never past its limit.
    /// With one counter left it goes straight to its limit, its final offer.
    /// If the trade can't work as asked (for example the bot can't afford it), it tries again
    /// without the properties the other player asked it for.
    /// Returns the counter and a sentence to go with it, or null and the reason why not.
    /// </summary>
    private (TradeOffer? Counter, string Why) CounterOffer(PlayerView view, TradeOffer offer, int countersLeft)
    {
        var (counter, why) = Haggle(view, offer.Reversed(), countersLeft);
        if (counter == null && offer.GetProperties.Count > 0 && offer.GiveProperties.Count > 0)
        {
            // Keep what it was offered, but don't hand over the properties it was asked for.
            var (smaller, smallerWhy) = Haggle(view, offer.Reversed() with { GiveProperties = [] }, countersLeft);
            if (smaller != null)
                return (smaller, smallerWhy);
        }
        return (counter, why);
    }

    /// <param name="asked">The other player's terms, written from the bot's side.</param>
    private (TradeOffer? Counter, string Why) Haggle(PlayerView view, TradeOffer asked, int countersLeft)
    {
        int them = asked.ToId;
        // Cash for cash is pointless: there must be a property or a card in it.
        if (asked.GiveProperties.Count + asked.GetProperties.Count + asked.GiveCards.Count + asked.GetCards.Count == 0)
            return (null, "I only want to trade if a property or a card changes hands.");

        TradeOffer limitOffer = Limit(view, asked);
        int limit = Net(limitOffer);
        // Never pay so much it can't keep half its reserve.
        int affordable = view.Me.Cash - MortgageFees(view, asked.GetProperties) - Reserve(view) / 2;
        limit = Math.Min(limit, affordable) / 10 * 10;
        int ask = Net(asked);

        int net;
        bool final = false;
        if (ask <= limit)
        {
            // Their terms are already fine (it dropped what they asked for): share what it would gain.
            net = ask + (limit - ask) / 2 / 10 * 10;
        }
        else
        {
            int start = MyLastPosition(view, asked) is TradeOffer last ? Net(last)
                : limit - Math.Max(20, (int)(Math.Abs(limit) * (1 - OpeningShare)) / 10 * 10);
            net = (int)Math.Floor((start + ask) / 2.0 / 10) * 10;
            if (net <= start)
                net = start + 10;
            if (countersLeft <= 1 || net >= limit)
            {
                net = limit;
                final = true;
            }
        }

        TradeOffer counter = WithNet(asked, net);
        // Both sides must be able to afford it, including the bank's fee on mortgaged properties.
        if (view.Player(them).Cash < counter.GetCash + MortgageFees(view, counter.GiveProperties))
            return (null, $"I'd need about ${counter.GetCash} from you, and you don't have that much cash.");
        if (view.Me.Cash - counter.GiveCash + counter.GetCash - MortgageFees(view, counter.GetProperties) < Reserve(view) / 2
            || !IsGoodForMe(view, counter.Reversed()))
        {
            return (null, "I can't find a price that works for both of us right now.");
        }
        return (counter, final ? "This is my best and final offer." : "Let's meet partway.");
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
    /// A rough guess at what a whole color group earns its owner from now until the game ends
    /// (decision #145): rent, plus the group's perk (see <see cref="PerkWorth"/>).
    /// For rent it looks at how many houses the owner could build with the cash they'd have left
    /// (plus a GO salary if they're about to pass GO), the most the zoning allows, a Building
    /// Moratorium, Rent Control, and who is left to land there. After the first
    /// <see cref="NearRounds"/> rounds it guesses they build more with their GO salaries,
    /// and counts that later money at <see cref="FarDiscount"/>.
    /// Each player lands on a given space about once every lap, so roughly
    /// (spaces in the group / spaces on the board) times per turn.
    /// </summary>
    protected double GroupEarnings(PlayerView view, int ownerId, string group, int cashAfter)
    {
        var members = content.GroupMembers(group).ToList();
        PropertyDefinition first = content.Property(members[0]);
        string? district = view.Spaces.First(s => s.Id == members[0]).DistrictId;

        double rounds = RoundsLeft(view);
        if (rounds <= 0)
            return 0;
        double near = Math.Min(rounds, NearRounds);
        double far = rounds - near;
        double lapsPerRound = SpacesPerTurn / view.Spaces.Count;

        // Cash to build with: keep $150 back for rent and taxes.
        PlayerInfo owner = view.Player(ownerId);
        int spacesToGo = (view.Spaces.Count - owner.Position) % view.Spaces.Count;
        double budget = Math.Max(0, cashAfter - 150) + (spacesToGo is > 0 and <= 14 ? content.Rules.GoSalary : 0);
        int maxLevel = district != null && view.Zoning.TryGetValue(district, out ZoningLevel zoning)
            ? Math.Min(3, content.Rules.MaxBuildingLevel(zoning))
            : 3;
        // Three houses each is where rent pays best for what they cost, so the guess stops there.
        int Levels(double cash) => first.HouseCost > 0 ? Math.Min(members.Count * maxLevel, (int)(cash / first.HouseCost)) : 0;
        int levels = Levels(budget);
        // Later on, about half of each GO salary goes into houses.
        int farLevels = Levels(budget + content.Rules.GoSalary * near * lapsPerRound * 0.5);

        // Nobody can build during a Building Moratorium, so building waits and earns for fewer rounds.
        int moratoriumRounds = view.ActiveOrdinances
            .Where(o => content.Ordinance(o.OrdinanceId).EffectType == "BuildingMoratorium")
            .Select(o => o.RoundsLeft ?? NearRounds).DefaultIfEmpty(0).Max();

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

        // A player with little cash and little to sell can't pay a big rent, so they count for less.
        double payers = view.Players.Where(p => p.Id != ownerId && !p.IsBankrupt)
            .Sum(p => Math.Clamp((p.Cash + 0.5 * Math.Max(0, p.NetWorth - p.Cash)) / 500.0, 0.4, 1.0));
        double landingsPerRound = (double)members.Count / view.Spaces.Count * payers;
        double waiting = Math.Min(near, moratoriumRounds);
        double rent = landingsPerRound * (AverageRent(0) * waiting + AverageRent(levels) * (near - waiting)
            + AverageRent(farLevels) * far * FarDiscount);
        // Houses cost money; selling them back to the bank returns half, so half their cost is spent for good.
        double housesSpent = (far > 0 ? farLevels : levels) * first.HouseCost * 0.5;
        return Math.Max(0, rent - housesSpent) + PerkWorth(view, ownerId, group, near, far);
    }

    /// <summary>
    /// A rough dollar value for a color group's perk over the rest of the game: for example
    /// Brown's extra $50 at GO is worth $50 for every lap the owner should still make.
    /// Perks that don't pay cash get a small guess. These read the perk's rule changes from
    /// groups.json, so changing a number there changes what the bots think too.
    /// </summary>
    private double PerkWorth(PlayerView view, int ownerId, string group, double near, double far)
    {
        double lapsPerRound = SpacesPerTurn / view.Spaces.Count;
        double laps = (near + far * FarDiscount) * lapsPerRound;
        double rounds = near + far * FarDiscount;
        int opponents = view.Players.Count(p => p.Id != ownerId && !p.IsBankrupt);
        int towers = view.Properties.Values.Count(p => p.OwnerId == ownerId && p.Level >= 5);
        return (content.Groups[group].Perk.Modifiers ?? []).Sum(m => m.Type switch
        {
            "GoCash" => m.Value * laps,
            "GoMomentum" => TokenValue * m.Value * laps,
            "MomentumCap" => 10,
            // Each opponent spends about one turn in eight in Jail or the Hospital.
            "JailToll" => m.Value * opponents * rounds / 8,
            "HostileTakeover" => 30 * laps,
            "DrawTwoKeepOne" => 10 * laps,
            "DividendPerTower" => m.Value * towers * rounds + 30,
            _ => 20,
        });
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
