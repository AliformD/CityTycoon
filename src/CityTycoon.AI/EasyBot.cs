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
public sealed class EasyBot : IPlayerController
{
    private readonly Pcg32 rng;
    private readonly GameContent content;

    /// <summary>The round it last offered a trade, so it offers at most one per turn.</summary>
    private int lastTradeRound = -1;

    /// <summary>Cash the bot keeps back so it can pay rent and taxes.</summary>
    public int SafetyReserve { get; }

    /// <summary>The most it will pay, as a share of assessed value (for example 1.1 = 110%).</summary>
    public double MaxPriceRatio { get; }

    public EasyBot(ulong seed, GameContent? content = null)
    {
        rng = new Pcg32(seed, stream: 101);
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
            DecisionKind.RespondToTrade => IsGoodForMe(view, decision.Trade!) ? new AcceptTrade() : new DeclineTrade(),
            _ => ManageMoney(view, decision, legalActions) ?? MainChoice(view, decision, legalActions),
        };
        return Task.FromResult(choice);
    }

    private GameAction MainChoice(PlayerView view, PendingDecision decision, IReadOnlyList<GameAction> legalActions) => decision.Kind switch
    {
        // Pay bail when comfortably rich; otherwise hope for doubles (it's free).
        DecisionKind.JailChoice => legalActions.Contains(new PayBail()) && view.Me.Cash > SafetyReserve * 3
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
        int spare = view.Me.Cash - SafetyReserve;

        if (decision.Management.HasFlag(ManagementOptions.Trade) && lastTradeRound != view.Round
            && SetCompletingTrade(view) is TradeOffer offer)
        {
            lastTradeRound = view.Round;
            return new ProposeTrade(offer);
        }

        // Build on the cheapest spot it can afford.
        var build = legalActions.OfType<Build>()
            .Select(b => (Action: b, Cost: NextBuildCost(view, b.PropertyId)))
            .Where(b => b.Cost <= spare)
            .OrderBy(b => b.Cost)
            .FirstOrDefault();
        if (build.Action != null)
            return build.Action;

        // Only when really comfortable: get mortgaged properties back, then clear loans.
        if (legalActions.OfType<Unmortgage>().FirstOrDefault() is Unmortgage unmortgage && spare > SafetyReserve * 2)
            return unmortgage;
        if (legalActions.Contains(new RepayLoan()) && spare > SafetyReserve * 2 + content.Rules.LoanRepay)
            return new RepayLoan();

        return null;
    }

    private int NextBuildCost(PlayerView view, string propertyId) =>
        BuildingRules.LevelCost(content, content.Property(propertyId), view.Properties[propertyId].Level + 1);

    /// <summary>Owes money: mortgage first (no loss), then loans, then sell buildings (it loses half).</summary>
    private static GameAction RaiseMoney(IReadOnlyList<GameAction> legalActions) =>
        legalActions.OfType<Mortgage>().FirstOrDefault()
        ?? legalActions.OfType<TakeLoan>().FirstOrDefault()
        ?? legalActions.OfType<SellBuilding>().FirstOrDefault()
        ?? legalActions[0] as GameAction;

    private GameAction ChooseBid(PlayerView view, PendingDecision decision)
    {
        int inc = decision.BidIncrement;
        int assessed = content.Property(decision.PropertyId!).AssessedValue;
        int worth = (int)(assessed * MaxPriceRatio);
        int affordable = view.Me.Cash - SafetyReserve;
        int limit = Math.Min(affordable, worth) / inc * inc;
        if (limit >= decision.MinBid)
            return new PlaceBid(PickBetween(decision.MinBid, limit, inc));

        // Short on cash: a down payment, if the deposit still leaves half the reserve.
        int dpLimit = Math.Min(decision.MaxDownPaymentBid, worth) / inc * inc;
        if (dpLimit >= decision.MinBid
            && view.Me.Cash - AuctionStep.Deposit(content.Rules, decision.MinBid) >= SafetyReserve / 2)
        {
            return new PlaceBid(decision.MinBid, DownPayment: true);
        }

        return new PassBid();
    }

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
            if (view.Me.Cash - price < SafetyReserve)
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
    private bool IsGoodForMe(PlayerView view, TradeOffer offer)
    {
        // The offer is written from the proposer's side; this bot is the receiver.
        int me = view.ViewerId;
        int them = offer.FromId;
        if (view.Me.Cash - offer.GetCash < SafetyReserve / 2)
            return false;

        double gain = offer.GiveCash - offer.GetCash
            + offer.GiveProperties.Sum(id => Value(view, id))
            - offer.GetProperties.Sum(id => Value(view, id));

        gain += GroupBonus(view, me, add: offer.GiveProperties, remove: offer.GetProperties);
        gain -= GroupBonus(view, them, add: offer.GetProperties, remove: offer.GiveProperties);
        return gain > 0;
    }

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
}
