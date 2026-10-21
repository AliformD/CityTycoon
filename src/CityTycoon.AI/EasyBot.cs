using CityTycoon.Engine.Actions;
using CityTycoon.Engine.Content;
using CityTycoon.Engine.Hosting;
using CityTycoon.Engine.Randomness;
using CityTycoon.Engine.Rules;
using CityTycoon.Engine.Steps;
using CityTycoon.Engine.Views;

namespace CityTycoon.AI;

/// <summary>
/// The Easy bot (Section 23.2): mostly simple choices with basic sense. It
/// never bids more than its cash minus a safety reserve, and won't pay much
/// over a property's assessed value.
///
/// Each Easy bot gets a small random "personality" (how much it's willing to
/// overpay), so a table of Easy bots doesn't all play the same.
/// </summary>
public class EasyBot : IPlayerController
{
    protected readonly Pcg32 rng;
    protected readonly GameContent content;

    /// <summary>The round it last offered a trade, so it offers at most one per turn.</summary>
    private int lastTradeRound = -1;

    /// <summary>Cash the bot keeps back so it can pay rent and taxes.</summary>
    public int SafetyReserve { get; }

    /// <summary>Cash to keep back right now. Smarter bots change this with what is ahead on the board.</summary>
    protected virtual int Reserve(PlayerView view) => SafetyReserve;

    /// <summary>The most it will pay, as a share of assessed value (for example 1.1 = 110%).</summary>
    public double MaxPriceRatio { get; }

    public EasyBot(ulong seed, GameContent? content = null, ulong stream = 101)
    {
        rng = new Pcg32(seed, stream);
        this.content = content ?? GameContent.Default;
        SafetyReserve = rng.NextInt(100, 250);
        MaxPriceRatio = 0.9 + rng.NextDouble() * 0.4; // 90% to 130%
    }

    public Task<GameAction> DecideAsync(PlayerView view, PendingDecision decision, IReadOnlyList<GameAction> legalActions)
    {
        GameAction choice = decision.Kind switch
        {
            DecisionKind.SubmitBid => ChooseBid(view, decision),
            DecisionKind.RaiseMoney => RaiseMoney(legalActions),
            DecisionKind.RespondToTrade => RespondToTrade(view, decision),
            DecisionKind.ChoosePrecisionDice => ChooseBestDice(view, decision),
            DecisionKind.ChooseNudge => ChooseNudge(view, decision, legalActions),
            // Insurance only when there's plenty of spare cash.
            DecisionKind.HospitalVisit => view.Me.Cash - content.Rules.InsuredMarkerCost >= Reserve(view) * 2 ? new BuyInsurance() : new NoThanks(),
            // The Casino loses money on average, so the Easy bot walks past.
            DecisionKind.CasinoBet => new NoThanks(),
            // A duel is a coin flip: fine for pocket change only.
            DecisionKind.RespondToDuel => decision.Duel!.Stake <= view.Me.Cash / 20 ? new AcceptDuel() : new DeclineDuel(),
            DecisionKind.ChooseStadiumGroup => new PickStadiumGroup(BestStadiumGroup(view)),
            // A card is usually worth more than one token, while there's room in the hand.
            DecisionKind.CityParkChoice => view.MyHand.Count < view.Me.HandLimit ? new DrawOpportunity() : new TakeMomentum(),
            DecisionKind.MallVisit => MallChoice(view, legalActions),
            DecisionKind.DiscardCards => new DiscardCard(view.MyHand.MinBy(CardValue)!),
            DecisionKind.PlayReaction => ChooseReaction(view, decision, legalActions),
            // Hop onto the Transit Hub when the rent costs more than the hop.
            DecisionKind.TransitHop => decision.AmountOwed > content.GovTile("transit-hub").Neighbor!.TransitHopCost ? new HopToTransitHub() : new NoThanks(),
            // The Easy bot doesn't take the train.
            DecisionKind.TransitRide => new NoThanks(),
            // "AI players pick randomly from what's left" (Section 5).
            DecisionKind.ChooseCharacter => legalActions[rng.NextInt(legalActions.Count)],
            // A reroll is only offered when the roll is bad, so always take it.
            DecisionKind.ChooseReroll => new RerollDice(),
            DecisionKind.KeepCard => new KeepCard(decision.Cards!.MaxBy(CardValue)!),
            DecisionKind.PeekTile => legalActions[rng.NextInt(legalActions.Count)],
            DecisionKind.ChooseOrdinance => ChooseOrdinance(view, decision),
            DecisionKind.ChooseDistrict => ChooseDistrict(view, decision),
            DecisionKind.CouncilVote => Vote(view, decision),
            _ => PlayACard(view, decision, legalActions) ?? ManageMoney(view, decision, legalActions) ?? MainChoice(view, decision, legalActions),
        };
        return Task.FromResult(choice);
    }

    // ---------------------------------------------------------- City Council

    /// <summary>The agenda setter picks the ordinance that looks best for them. Ties are broken at random.</summary>
    private GameAction ChooseOrdinance(PlayerView view, PendingDecision decision)
    {
        var best = decision.Ordinances!
            .GroupBy(id => BestBenefit(view, id))
            .OrderByDescending(g => g.Key).First().ToList();
        return new ChooseOrdinance(best[rng.NextInt(best.Count)]);
    }

    private GameAction ChooseDistrict(PlayerView view, PendingDecision decision)
    {
        string ordinanceId = decision.OrdinanceId!;
        var best = decision.Districts!
            .GroupBy(d => Benefit(view, ordinanceId, d))
            .OrderByDescending(g => g.Key).First().ToList();
        return new ChooseDistrict(best[rng.NextInt(best.Count)]);
    }

    /// <summary>
    /// Votes for what helps it and against what hurts it, and lobbies a little when it feels strongly and
    /// has cash to spare. It plays a Lobbyist card whenever it takes a side.
    /// </summary>
    protected virtual GameAction Vote(PlayerView view, PendingDecision decision)
    {
        int benefit = Benefit(view, decision.OrdinanceId!, decision.DistrictId);
        if (benefit == 0)
            return new CastVote(VoteChoice.Abstain);
        int spare = Math.Max(0, view.Me.Cash - Reserve(view) * 2);
        int lobby = Math.Min(decision.MaxLobby, Math.Min(spare / 6, 40 * Math.Abs(benefit)));
        return new CastVote(benefit > 0 ? VoteChoice.Yes : VoteChoice.No, lobby, PlayLobbyist: decision.HasLobbyist);
    }

    protected int MyRailroads(PlayerView view) =>
        view.Properties.Values.Count(p => p.OwnerId == view.ViewerId && content.Property(p.Id).Type == PropertyType.Railroad);

    private int BestBenefit(PlayerView view, string ordinanceId)
    {
        OrdinanceDefinition def = content.Ordinance(ordinanceId);
        if (def.Target != OrdinanceTarget.District)
            return Benefit(view, ordinanceId, null);
        return content.Board.Districts.Max(d => Benefit(view, ordinanceId, d.Id));
    }

    /// <summary>How much an ordinance helps this bot: above 0 helps, below 0 hurts, 0 doesn't matter much.</summary>
    protected int Benefit(PlayerView view, string ordinanceId, string? districtId)
    {
        OrdinanceDefinition def = content.Ordinance(ordinanceId);
        int mineHere = 0, theirsHere = 0;
        if (districtId != null)
        {
            foreach (SpaceInfo space in view.Spaces.Where(s => s.DistrictId == districtId && s.Kind == SpaceKind.Property))
            {
                PropertyInfo property = view.Properties[space.Id];
                int weight = 1 + property.Level;
                if (property.OwnerId == view.ViewerId)
                    mineHere += weight;
                else if (property.OwnerId != null)
                    theirsHere += weight;
            }
        }
        int mineLevels = view.Properties.Values.Where(p => p.OwnerId == view.ViewerId).Sum(p => p.Level);
        int othersLevels = view.Properties.Values.Where(p => p.OwnerId != null && p.OwnerId != view.ViewerId).Sum(p => p.Level);
        int others = Math.Max(1, view.Players.Count(p => !p.IsBankrupt) - 1);
        int Sign(int mine, int theirs) => mine > theirs ? 1 : mine < theirs ? -1 : 0;

        return def.EffectType switch
        {
            "CollectFromBank" => 1,
            "RevealAllTiles" => 1,
            "AddPermits" => 1,
            "Rezone" => def.Int("direction") * Sign(mineHere, theirsHere),
            "DistrictRent" => Math.Sign(def.Int("percent")) * Sign(mineHere, theirsHere),
            "Preserve" => Sign(mineHere, theirsHere),
            "RailroadRent" => Sign(MyRailroads(view) * others, view.Properties.Values.Count(p => p.OwnerId != null && p.OwnerId != view.ViewerId && content.Property(p.Id).Type == PropertyType.Railroad)),
            // A tax on buildings hurts whoever has built the most.
            "TaxPerLevel" => Sign(othersLevels, mineLevels * others),
            "InfrastructureBond" => -1,
            // A building ban helps whoever hasn't got a full set to build on yet, and hurts builders.
            "BuildingMoratorium" => mineLevels > 0 || view.Properties.Values.Count(p => p.OwnerId == view.ViewerId) >= 3 ? -1 : 1,
            _ => 0,
        };
    }

    private GameAction MainChoice(PlayerView view, PendingDecision decision, IReadOnlyList<GameAction> legalActions) => decision.Kind switch
    {
        // A Precision Roll costs tokens, so only use one when the tokens would
        // otherwise go to waste (it's at its token limit and GO would add more).
        DecisionKind.ChooseMoveMethod when legalActions.Contains(new PrecisionRoll())
            && view.Me.Momentum >= view.Me.MomentumCap => new PrecisionRoll(),
        // Pay bail when comfortably rich (or when it's free); otherwise hope for doubles (it's free).
        DecisionKind.JailChoice => legalActions.Contains(new PayBail()) && (view.Me.Bail == 0 || view.Me.Cash > Reserve(view) * 3)
            ? new PayBail()
            : new RollForDoubles(),
        _ => legalActions.First(a => a is not ManagementAction),
    };

    /// <summary>
    /// Things it does on its own turn before answering: offer a trade, build,
    /// unmortgage, pay back loans. Returns null when there's nothing to do.
    /// Each of these costs money or is limited per turn, so it can't loop forever.
    /// </summary>
    private GameAction? ManageMoney(PlayerView view, PendingDecision decision, IReadOnlyList<GameAction> legalActions)
    {
        int spare = view.Me.Cash - Reserve(view);

        if (decision.Management.HasFlag(ManagementOptions.Trade) && lastTradeRound != view.Round
            && SetCompletingTrade(view) is TradeOffer offer)
        {
            lastTradeRound = view.Round;
            return new ProposeTrade(offer);
        }

        // Build on the cheapest spot it can afford.
        if (PickBuild(view, legalActions, spare) is Build build)
            return build;

        // Only when really comfortable: get mortgaged properties back, then clear loans.
        if (legalActions.OfType<Unmortgage>().FirstOrDefault() is Unmortgage unmortgage && spare > Reserve(view) * 2)
            return unmortgage;
        if (legalActions.Contains(new RepayLoan()) && spare > Reserve(view) * 2 + view.Me.LoanRepayCost)
            return new RepayLoan();

        // The Red perk: force an auction on the one missing piece of a color group it nearly owns.
        if (legalActions.OfType<HostileTakeover>().FirstOrDefault(t => CompletesAGroup(view, t.PropertyId)) is HostileTakeover takeover
            && spare > content.Property(takeover.PropertyId).AssessedValue)
        {
            return takeover;
        }

        return null;
    }

    /// <summary>Which building to build, if any. The Easy bot takes the cheapest one it can afford.</summary>
    protected virtual Build? PickBuild(PlayerView view, IReadOnlyList<GameAction> legalActions, int spare) =>
        legalActions.OfType<Build>()
            .Where(b => NextBuildCost(view, b.PropertyId) <= spare)
            .OrderBy(b => NextBuildCost(view, b.PropertyId))
            .FirstOrDefault();

    protected static int NextBuildCost(PlayerView view, string propertyId) => view.Properties[propertyId].NextLevelCost;

    /// <summary>True if owning this property would give the bot a whole color group.</summary>
    private bool CompletesAGroup(PlayerView view, string propertyId)
    {
        string? group = content.Property(propertyId).Group;
        return group != null && content.GroupMembers(group).Where(id => id != propertyId)
            .All(id => view.Properties[id].OwnerId == view.ViewerId && !view.Properties[id].IsPending);
    }

    /// <summary>Owes money: mortgage first (no loss), then loans, then sell buildings (it loses half).</summary>
    private static GameAction RaiseMoney(IReadOnlyList<GameAction> legalActions) =>
        legalActions.OfType<Mortgage>().FirstOrDefault()
        ?? legalActions.OfType<TakeLoan>().FirstOrDefault()
        ?? legalActions.OfType<SellBuilding>().FirstOrDefault()
        ?? legalActions[0] as GameAction;

    private GameAction ChooseBid(PlayerView view, PendingDecision decision)
    {
        int inc = decision.BidIncrement;
        int worth = (int)PropertyWorth(view, decision);
        int affordable = view.Me.Cash - Reserve(view);
        int limit = Math.Min(affordable, worth) / inc * inc;
        if (limit >= decision.MinBid)
        {
            // In an open auction the bot raises one step at a time, so it climbs to its limit and stops.
            return new PlaceBid(decision.OpenAuction ? decision.MinBid : SealedAmount(view, decision, limit, inc));
        }

        // Short on cash: a down payment, if the deposit still leaves half the reserve.
        int dpLimit = Math.Min(decision.MaxDownPaymentBid, worth) / inc * inc;
        if (dpLimit >= decision.MinBid
            && view.Me.Cash - AuctionStep.Deposit(content.Rules, decision.MinBid) >= Reserve(view) / 2)
        {
            return new PlaceBid(decision.MinBid, DownPayment: true);
        }

        return new PassBid();
    }

    /// <summary>The most this bot thinks the property is worth to it.</summary>
    protected virtual double PropertyWorth(PlayerView view, PendingDecision decision) =>
        content.Property(decision.PropertyId!).AssessedValue * MaxPriceRatio;

    /// <summary>What to put in a sealed bid, given the most it is willing to pay.</summary>
    protected virtual int SealedAmount(PlayerView view, PendingDecision decision, int limit, int inc) =>
        PickBetween(decision.MinBid, limit, inc);

    /// <summary>Somewhere between the minimum and the limit, leaning towards the limit.</summary>
    private int PickBetween(int min, int max, int inc)
    {
        int steps = (max - min) / inc;
        int chosen = Math.Max(rng.NextInt(steps + 1), rng.NextInt(steps + 1));
        return min + chosen * inc;
    }

    /// <summary>
    /// Finds a color group where it owns every property but one, and offers
    /// cash (150% of assessed value) for the missing one.
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

            int price = (int)(content.Property(target.Id).AssessedValue * 1.5) / 10 * 10;
            if (view.Me.Cash - price < Reserve(view))
                continue;

            return new TradeOffer(myId, ownerId, price, 0, [], [target.Id]);
        }
        return null;
    }

    /// <summary>
    /// Accepts when what it gets is worth more than what it gives. Completing
    /// its own color group counts extra; handing someone else a full group
    /// counts against.
    /// </summary>
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
            - offer.GetProperties.Sum(id => Value(view, id));

        gain += GroupBonus(view, me, add: offer.GiveProperties, remove: offer.GetProperties);
        gain -= GroupBonus(view, them, add: offer.GetProperties, remove: offer.GiveProperties);
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
    /// A counter-offer the bot would accept itself: the same properties, with the cash
    /// moved so it comes out ahead by a small margin. It uses the same sums as
    /// accepting, so countering never makes a bad trade easier to get. It never
    /// counters a trade that would hand the other player a whole color group:
    /// those it just declines. Returns null when no counter makes sense.
    /// </summary>
    private TradeOffer? CounterOffer(PlayerView view, TradeOffer offer)
    {
        int me = view.ViewerId;
        int them = offer.FromId;
        if (GroupBonus(view, them, add: offer.GetProperties, remove: offer.GiveProperties) > 0)
            return null;

        // The margin: 5% of everything changing hands, at least $10, so the bot doesn't counter for a break-even trade.
        double traded = offer.GiveCash + offer.GetCash
            + offer.GiveProperties.Concat(offer.GetProperties).Sum(id => Value(view, id));
        double margin = Math.Max(10, traded * 0.05);
        int needed = (int)Math.Ceiling((margin - Gain(view, offer)) / 10) * 10;
        if (needed <= 0)
            return null;

        // From the bot's side: it gives what it was asked for and gets what it was offered.
        // First it pays less cash, then it asks for more.
        int pay = offer.GetCash;
        int ask = offer.GiveCash;
        int cut = Math.Min(pay, needed);
        pay -= cut;
        ask += needed - cut;

        var counter = new TradeOffer(me, them, pay, ask, offer.GetProperties, offer.GiveProperties);
        // Cash for cash is pointless: there must be a property in it.
        if (counter.GiveProperties.Count == 0 && counter.GetProperties.Count == 0)
            return null;
        var asOffer = new TradeOffer(them, me, ask, pay, offer.GiveProperties, offer.GetProperties);
        // Both sides must be able to afford it, including the bank's fee on mortgaged properties.
        if (view.Player(them).Cash < ask + MortgageFees(view, counter.GiveProperties)
            || view.Me.Cash - pay + ask - MortgageFees(view, counter.GetProperties) < Reserve(view) / 2
            || !IsGoodForMe(view, asOffer))
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

    /// <summary>Half the group's value, for each color group this trade would complete for the player.</summary>
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

    // ----- Movement: Precision Roll and Nudge (Section 7.3) -----

    /// <summary>Roughly what a token is worth to the bot, in dollars. A Nudge must save more than this.</summary>
    protected const int TokenValue = 25;

    /// <summary>How much better a Nudge must make the landing spot before the bot spends a token.</summary>
    protected virtual double NudgeThreshold => TokenValue;

    private GameAction ChooseBestDice(PlayerView view, PendingDecision decision)
    {
        var dice = decision.Dice!;
        var pairs = new[] { (0, 1), (0, 2), (1, 2) };
        var best = pairs.MaxBy(p =>
        {
            int a = dice[p.Item1], b = dice[p.Item2];
            // A small bonus for doubles, which give another turn.
            return LandingScore(view, a + b) + (a == b ? 20 : 0);
        });
        return new ChooseDice(best.Item1, best.Item2);
    }

    private GameAction ChooseNudge(PlayerView view, PendingDecision decision, IReadOnlyList<GameAction> legalActions)
    {
        int rolled = decision.Dice!.Sum();
        double stay = LandingScore(view, rolled);
        var best = legalActions.OfType<Nudge>()
            .Where(n => n.Spaces != 0)
            .Select(n => (Nudge: n, Score: LandingScore(view, rolled + n.Spaces)))
            .OrderByDescending(x => x.Score)
            .FirstOrDefault();
        return best.Nudge != null && best.Score - stay > NudgeThreshold ? best.Nudge : new Nudge(0);
    }

    /// <summary>
    /// How good it would be to land this many spaces ahead, in rough dollars:
    /// paying rent or tax is bad, a property to bid on is good.
    /// </summary>
    private double LandingScore(PlayerView view, int spaces)
    {
        SpaceInfo space = view.Spaces[(view.Me.Position + spaces) % view.Spaces.Count];
        return space.Kind switch
        {
            SpaceKind.Property => PropertyScore(view, space.Id, spaces),
            SpaceKind.Tax => -(space.Id == "income-tax" ? content.Rules.IncomeTax : content.Rules.LuxuryTax),
            SpaceKind.GoToJail => -100,
            SpaceKind.CityPark => TokenValue,
            _ => 0,
        };
    }

    private double PropertyScore(PlayerView view, string id, int diceTotal)
    {
        PropertyInfo owned = view.Properties[id];
        if (owned.OwnerId == null)
            return view.Me.Cash - content.Property(id).AssessedValue / 2 > Reserve(view) ? 30 : 0;
        if (owned.OwnerId == view.ViewerId)
            return 5;
        return -EstimateRent(view, id, diceTotal);
    }

    /// <summary>A rough copy of the rent rules, from what the bot can see.</summary>
    protected double EstimateRent(PlayerView view, string id, int diceTotal)
    {
        PropertyInfo owned = view.Properties[id];
        if (owned.IsMortgaged || owned.IsPending || owned.OwnerId is not int ownerId)
            return 0;
        PropertyDefinition def = content.Property(id);
        bool Counts(string otherId) => view.Properties[otherId].OwnerId == ownerId && !view.Properties[otherId].IsPending;
        double rent = def.Type switch
        {
            PropertyType.Color => def.Rents![owned.Level]
                * (owned.Level == 0 && content.GroupMembers(def.Group!).All(m => Counts(m) && !view.Properties[m].IsMortgaged) ? 2 : 1),
            PropertyType.Railroad => content.Rules.RailroadRents[Math.Max(0, CountOfType(view, ownerId, PropertyType.Railroad) - 1)],
            _ => content.Rules.UtilityRentMultipliers[Math.Max(0, CountOfType(view, ownerId, PropertyType.Utility) - 1)] * diceTotal,
        };
        if (def.Group != null && view.StadiumBoosts.Any(b => b.GroupId == def.Group))
            rent *= content.Rules.StadiumRentMultiplier;
        rent = rent * (100 + owned.RentPercent) / 100; // revealed tiles next door
        if (view.Player(ownerId).InJail)
            rent *= content.Rules.JailRentMultiplier;
        return rent;
    }

    protected int CountOfType(PlayerView view, int ownerId, PropertyType type) =>
        view.Properties.Values.Count(p => p.OwnerId == ownerId && !p.IsPending && content.Property(p.Id).Type == type);

    /// <summary>
    /// The Stadium doubles a group's rent: pick the group where the bot earns
    /// the most, and where opponents earn the least.
    /// </summary>
    private string BestStadiumGroup(PlayerView view) =>
        content.Groups.Keys.MaxBy(group => content.GroupMembers(group).Sum(id =>
        {
            int? owner = view.Properties[id].OwnerId;
            double rent = EstimateRent(view, id, 7);
            return owner == view.ViewerId ? rent : owner == null ? 0 : -rent;
        }))!;

    // ----- Cards (Section 15.4) -----

    /// <summary>
    /// How much the bot likes holding a card, by what its effect does. Used to
    /// pick what to discard or sell. Cards the Easy bot never plays are worth least.
    /// </summary>
    private int CardValue(string cardId) => content.Card(cardId).EffectType switch
    {
        "CancelCard" => 9,
        "RentMultiplier" => 8,
        "GetOutOfJailFree" => 6,
        "PropertyRentBoost" => 6,
        "GainMomentum" => 5,
        "PayPercentOfCash" => 5,
        "Detour" => 4,
        "ConstructionPermit" => 4,
        "PeekTile" => 2,
        _ => 1,
    };

    /// <summary>
    /// Plays an Opportunity card on its own turn when the moment is obviously
    /// right. Returns null to do something else. Playing removes the card from
    /// the hand, so this can't loop forever.
    /// </summary>
    private GameAction? PlayACard(PlayerView view, PendingDecision decision, IReadOnlyList<GameAction> legalActions)
    {
        var plays = legalActions.OfType<PlayCard>().ToList();
        if (plays.Count == 0)
            return null;
        PlayCard? Find(string effect) => plays.FirstOrDefault(p => content.Card(p.CardId).EffectType == effect);

        if (decision.Kind == DecisionKind.JailChoice)
            return Find("GetOutOfJailFree");

        if (Find("GainMomentum") is PlayCard fastTrack && view.Me.Momentum + content.Card(fastTrack.CardId).Int("amount") <= content.Rules.MomentumCap - 2)
            return fastTrack;

        // Audit the richest opponent, if it's worth it.
        var audits = plays.Where(p => content.Card(p.CardId).EffectType == "PayPercentOfCash" && p.TargetPlayerId != null).ToList();
        if (audits.MaxBy(p => view.Player(p.TargetPlayerId!.Value).Cash) is PlayCard audit && view.Player(audit.TargetPlayerId!.Value).Cash >= 300)
            return audit;

        // Grand Opening on its best earner.
        var openings = plays.Where(p => content.Card(p.CardId).EffectType == "PropertyRentBoost" && p.TargetSpaceId != null).ToList();
        if (openings.MaxBy(p => EstimateRent(view, p.TargetSpaceId!, 7)) is PlayCard opening && EstimateRent(view, opening.TargetSpaceId!, 7) >= 20)
            return opening;

        // A Construction Permit only after moving, when there's money to build with.
        if (decision.Kind == DecisionKind.PostMoveActions && Find("ConstructionPermit") is PlayCard permit
            && view.Me.BuildDiscountThisTurn == 0
            && legalActions.OfType<Build>().Any(b => NextBuildCost(view, b.PropertyId) * 3 <= view.Me.Cash - Reserve(view)))
        {
            return permit;
        }

        return Find("PeekTile");
    }

    /// <summary>Reaction cards: always use Rent Strike and Insurance; use Detour when it helps.</summary>
    private GameAction ChooseReaction(PlayerView view, PendingDecision decision, IReadOnlyList<GameAction> legalActions)
    {
        var plays = legalActions.OfType<PlayCard>().ToList();
        var detours = plays.Where(p => content.Card(p.CardId).EffectType == "Detour").ToList();
        if (detours.Count == 0)
            return plays.FirstOrDefault() ?? (GameAction)new NoThanks();

        // Detour: steer the mover onto the bot's own property, or away from nothing.
        if (decision.OtherPlayerId is not int moverId)
            return new NoThanks();
        int from = view.Player(moverId).Position;
        double Gain(int spaces)
        {
            SpaceInfo space = view.Spaces[(from + spaces) % view.Spaces.Count];
            if (space.Kind == SpaceKind.GoToJail)
                return 50;
            return space.Kind == SpaceKind.Property && view.Properties[space.Id].OwnerId == view.ViewerId
                ? EstimateRent(view, space.Id, decision.Spaces)
                : 0;
        }
        double stay = Gain(decision.Spaces);
        var best = detours.MaxBy(d => Gain(decision.Spaces + d.Number))!;
        return Gain(decision.Spaces + best.Number) > stay + TokenValue ? best : new NoThanks();
    }

    /// <summary>Buys the cheapest shop card if the hand has room and cash is plentiful; sells junk when the hand is full.</summary>
    private GameAction MallChoice(PlayerView view, IReadOnlyList<GameAction> legalActions)
    {
        int spare = view.Me.Cash - Reserve(view) * 2;
        if (view.MyHand.Count < view.Me.HandLimit && legalActions.Contains(new BuyCard(0))
            && view.MallShop.Count > 0 && CardValue(view.MallShop[0]) >= 4 && spare >= content.Rules.MallPrices[0])
        {
            return new BuyCard(0);
        }
        if (view.MyHand.Count >= view.Me.HandLimit && view.MyHand.MinBy(CardValue) is string junk && CardValue(junk) <= 2)
            return new SellCard(junk);
        return new NoThanks();
    }
}
