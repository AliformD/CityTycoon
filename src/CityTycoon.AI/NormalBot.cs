using CityTycoon.Engine.Actions;
using CityTycoon.Engine.Content;
using CityTycoon.Engine.Events;
using CityTycoon.Engine.Hosting;
using CityTycoon.Engine.State;
using CityTycoon.Engine.Views;

namespace CityTycoon.AI;

/// <summary>
/// The Normal bot (Section 23.2). It plays like the Easy bot, but it uses simple estimates:
/// what a property is worth to it (a big bonus when it completes a color group), which building
/// pays back fastest, and how much an ordinance is worth in dollars, so it bids for the Politician's
/// vote (or lobbies, as the Politician) when a vote clearly helps or hurts it.
///
/// Everything here only uses the bot's own <see cref="PlayerView"/>, so it can't see hidden tiles,
/// other players' cards or sealed bids (Section 23.1).
/// </summary>
public class NormalBot(ulong seed, GameContent? content = null, ulong stream = 102) : EasyBot(seed, content, stream)
{
    protected virtual double WorthScale => 1.0;

    protected override PokerSkill CasinoSkill => PokerSkill.ChaseStraights;

    /// <summary>Normal and Hard bots are less cautious than Easy about handing over a color group in a trade.</summary>
    protected override double SetCaution => 1.0;

    /// <summary>1 early in a Timed game, fading to 0.5 in the last few rounds, when a set no longer has time to pay off.</summary>
    protected static double TimeLeftFactor(PlayerView view) =>
        view.Mode == GameMode.Timed ? Math.Clamp((view.MaxRounds - view.Round) / 10.0, 0.5, 1.0) : 1.0;

    // ------------------------------------------------------------ Bidding

    protected override double PropertyWorth(PlayerView view, PendingDecision decision)
    {
        string id = decision.PropertyId!;
        PropertyDefinition def = content.Property(id);
        double worth = def.AssessedValue * MaxPriceRatio + (SetBonus(view, id) + CharacterFit(view, id)) * TimeLeftFactor(view)
            + PerkBonus(view, id);
        return WithBlocking(view, decision, worth * WorthScale);
    }

    /// <summary>
    /// The share of a color group's perk (what trades count, see <c>PerkWorth</c>) a bot adds to its bid for the
    /// last piece of the group; a smaller share for each piece it already has when the group is still open (decision #156).
    /// </summary>
    protected virtual double BidPerkShare => 0.5;

    /// <summary>How much of <see cref="CharacterFit"/> the bot adds to a bid (decision #156).</summary>
    protected virtual double CharacterFitShare => 0.3;

    /// <summary>Part of the group's perk, for winning the last piece, or for a piece of a group it could still finish.</summary>
    protected double PerkBonus(PlayerView view, string id)
    {
        string? group = content.Property(id).Group;
        if (group == null || BidPerkShare <= 0)
            return 0;
        var others = content.GroupMembers(group).Where(m => m != id).ToList();
        bool Mine(string m) => view.Properties[m].OwnerId == view.ViewerId && !view.Properties[m].IsPending;
        double perk = PerkWorthFor(view, view.ViewerId, group);
        if (others.All(Mine))
            return perk * BidPerkShare;
        if (others.Any(Mine) && others.All(m => Mine(m) || view.Properties[m].OwnerId == null))
            return perk * BidPerkShare * 0.2 * others.Count(Mine);
        return 0;
    }

    /// <summary>
    /// Extra a property is worth because it suits the bot's character, while its group can still be finished:
    /// the Contractor saves on every house, so groups with dear houses suit it; the Commuter
    /// (longer Nudges) likes the extra token room Light Blue's Commuter Belt gives.
    /// </summary>
    protected double CharacterFit(PlayerView view, string id)
    {
        PropertyDefinition def = content.Property(id);
        if (def.Group == null || CharacterFitShare <= 0
            || content.GroupMembers(def.Group).Any(m => view.Properties[m].OwnerId is int o && o != view.ViewerId))
        {
            return 0;
        }
        double fit = 0;
        // About 3 levels on this property, at the discount, and half of that because the set isn't sure yet.
        fit += CharacterModifier(view, view.ViewerId, "BuildDiscount") / 100.0 * def.HouseCost * 3 * 0.5;
        if (CharacterModifier(view, view.ViewerId, "NudgeRange") > 0 && PerkModifier(def.Group, "MomentumCap") > 0)
            fit += def.AssessedValue * 0.25;
        return fit * CharacterFitShare;
    }

    /// <summary>Extra dollars a property is worth because of the color group (or railroads and utilities) it belongs to.</summary>
    protected double SetBonus(PlayerView view, string id)
    {
        PropertyDefinition def = content.Property(id);
        if (def.Group == null)
        {
            // Railroads and utilities pay more for each one owned.
            int owned = CountOfType(view, view.ViewerId, def.Type);
            return def.AssessedValue * 0.2 * owned;
        }

        var others = content.GroupMembers(def.Group).Where(m => m != id).ToList();
        double groupValue = GroupValue(id);
        bool Mine(string m) => view.Properties[m].OwnerId == view.ViewerId && !view.Properties[m].IsPending;

        double bonus = 0;
        if (others.All(Mine))
            bonus += groupValue * 0.75;
        else if (others.Any(Mine) && others.All(m => Mine(m) || view.Properties[m].OwnerId == null))
            bonus += groupValue * 0.15 * others.Count(Mine);

        return bonus;
    }

    /// <summary>A sealed bid close to its limit, and all the way up to it when the set matters.</summary>
    protected override int SealedAmount(PlayerView view, PendingDecision decision, int limit, int inc)
    {
        bool matters = SetBonus(view, decision.PropertyId!) > 0 || RivalCompleting(view, decision.PropertyId!) != null;
        double share = matters ? 1.0 : 0.8 + rng.NextDouble() * 0.2;
        int amount = (int)(limit * share) / inc * inc;
        return Math.Clamp(amount, decision.MinBid, limit);
    }

    // ------------------------------------------------------------ Building

    /// <summary>
    /// Builds where each dollar buys the most extra rent. With Green's Dividends, a new hotel also
    /// pays its dividend every turn, counted here as the same money in rent.
    /// </summary>
    protected override Build? PickBuild(PlayerView view, IReadOnlyList<GameAction> legalActions, int spare)
    {
        int dividend = GroupPerkModifier(view, view.ViewerId, "DividendPerTower");
        int opponents = Math.Max(1, view.Players.Count(p => !p.IsBankrupt && p.Id != view.ViewerId));
        double Payback(Build b)
        {
            PropertyDefinition def = content.Property(b.PropertyId);
            int level = view.Properties[b.PropertyId].Level;
            double cost = Math.Max(1, NextBuildCost(view, b.PropertyId));
            if (def.Rents == null || level + 1 >= def.Rents.Count)
                return 0;
            double gain = def.Rents[level + 1] - def.Rents[level];
            // Rent is paid about (opponents / spaces) times a round; the dividend once a round.
            if (level + 1 == content.Rules.HotelLevel)
                gain += dividend * (double)view.Spaces.Count / opponents;
            return gain / cost;
        }
        return legalActions.OfType<Build>()
            .Where(b => NextBuildCost(view, b.PropertyId) <= spare)
            .OrderByDescending(Payback)
            .FirstOrDefault();
    }

    // ------------------------------------------------------------ City Council

    /// <summary>Normal and Hard bots rank the agenda by what each ordinance is worth to them in dollars (decision #159).</summary>
    protected override double OrdinanceScore(PlayerView view, string ordinanceId, string? districtId) =>
        OrdinanceWorth(view, view.ViewerId, ordinanceId, districtId);

    /// <summary>
    /// Votes for what is worth money to it. Only the Politician may lobby (decision #162), and it lobbies only as far
    /// as the extra chance of winning the vote pays for itself.
    /// </summary>
    protected override GameAction Vote(PlayerView view, PendingDecision decision)
    {
        double worth = OrdinanceWorth(view, view.ViewerId, decision.OrdinanceId!, decision.DistrictId);
        if (decision.Deal is VoteDeal bound)
            worth = bound.Choice == VoteChoice.Yes ? Math.Max(0, worth) : Math.Min(0, worth);
        if (Math.Abs(worth) < MinVoteWorth)
            return decision.Deal is VoteDeal held ? new CastVote(held.Choice) : new CastVote(VoteChoice.Abstain);

        VoteChoice side = worth > 0 ? VoteChoice.Yes : VoteChoice.No;
        bool useLobbyist = decision.HasLobbyist && Math.Abs(worth) >= 100;
        double e = view.Me.ContractPercent / 100.0;
        if (e <= 0 || decision.MaxLobby <= 0)
            return new CastVote(side, 0, useLobbyist);

        var (mine, theirs) = Influence(view, decision, view.ViewerId, side);
        // The lobby L that makes |worth| x chance(L) - L biggest: chance = (mine + eL) / (mine + theirs + eL).
        double best = (Math.Sqrt(Math.Abs(worth) * e * theirs) - (mine + theirs)) / e;
        int spare = Math.Max(0, view.Me.Cash - Reserve(view) * LobbyReserveFactor);
        int lobby = (int)Math.Clamp(best, 0, Math.Min(decision.MaxLobby, spare * LobbyShare)) / 10 * 10;
        return new CastVote(side, lobby, useLobbyist);
    }

    /// <summary>Below this many dollars either way, the bot doesn't care and abstains.</summary>
    protected const double MinVoteWorth = 10;

    /// <summary>The most of its spare cash it will lobby with on one vote.</summary>
    protected virtual double LobbyShare => 0.5;
    protected virtual int LobbyReserveFactor => 2;

    /// <summary>
    /// Expected influence on the bot's side and against it, if <paramref name="voterId"/> votes <paramref name="side"/>:
    /// public opinion on both, each other player voting the way their holdings suggest, and the Politician held to a
    /// contract (one already taken this session, or the <paramref name="contract"/> being weighed), with the
    /// contract money adding to their vote (decision #162).
    /// </summary>
    private (double Mine, double Theirs) Influence(PlayerView view, PendingDecision decision, int voterId, VoteChoice side,
        VoteDeal? contract = null)
    {
        double mine = content.Rules.PublicOpinionInfluence, theirs = content.Rules.PublicOpinionInfluence;
        var deals = DealsThisSession(view);
        if (contract != null)
            deals[contract.ToId] = contract;
        foreach (PlayerInfo p in view.Players.Where(p => !p.IsBankrupt))
        {
            VoteChoice vote;
            deals.TryGetValue(p.Id, out VoteDeal? held);
            if (p.Id == voterId)
                vote = side;
            else if (held != null)
                vote = held.Choice;
            else
                vote = Preference(view, decision, p.Id);
            if (vote == VoteChoice.Abstain)
                continue;
            double weight = VoteOf(view, p.Id) + (held?.Choice == vote ? held.Amount * p.ContractPercent / 100.0 : 0);
            if (vote == side) mine += weight;
            else theirs += weight;
        }
        return (mine, theirs);
    }

    /// <summary>How a player would vote on their own, from what the ordinance is worth to them.</summary>
    private VoteChoice Preference(PlayerView view, PendingDecision decision, int playerId)
    {
        double w = OrdinanceWorth(view, playerId, decision.OrdinanceId!, decision.DistrictId);
        return Math.Abs(w) < MinVoteWorth ? VoteChoice.Abstain : w > 0 ? VoteChoice.Yes : VoteChoice.No;
    }

    /// <summary>A player's vote influence (100 for everyone since decision #162).</summary>
    private int VoteOf(PlayerView view, int playerId) =>
        Math.Max(content.Rules.VoteInfluence, CharacterModifier(view, playerId, "VoteInfluence"));

    /// <summary>Contracts taken in this session so far, read from the log: who must vote which way, and for how much.</summary>
    private static Dictionary<int, VoteDeal> DealsThisSession(PlayerView view)
    {
        var deals = new Dictionary<int, VoteDeal>();
        for (int i = view.Log.Count - 1; i >= 0 && view.Log[i] is not OrdinanceProposed; i--)
            if (view.Log[i] is VoteDealMade made)
                deals[made.Deal.ToId] = made.Deal;
        return deals;
    }

    /// <summary>The chance its side wins with these influences.</summary>
    private static double Chance((double Mine, double Theirs) x) => x.Mine / Math.Max(1, x.Mine + x.Theirs);

    /// <summary>
    /// Bids for the Politician's vote (decision #162). If the top offer is already for its side, it waits. Otherwise it
    /// works out, for each offer it could make, how much more likely its side wins compared with letting the top offer
    /// stand (or the Politician voting freely), times what the vote is worth to it, minus the offer. It makes the offer
    /// that leaves it furthest ahead, but never less than the Politician would need to take it.
    /// </summary>
    protected override GameAction OfferDeal(PlayerView view, PendingDecision decision)
    {
        double worth = OrdinanceWorth(view, view.ViewerId, decision.OrdinanceId!, decision.DistrictId);
        if (Math.Abs(worth) < MinVoteWorth || decision.OtherPlayerId is not int politician)
            return new NoThanks();
        VoteChoice side = worth > 0 ? VoteChoice.Yes : VoteChoice.No;
        VoteDeal? top = decision.Deal;
        if (top?.Choice == side)
            return new NoThanks();

        double now = Chance(Influence(view, decision, view.ViewerId, side, top));
        int spare = Math.Max(0, Math.Min(decision.MaxBid, view.Me.Cash - Reserve(view)));
        int floor = Math.Max(decision.MinBid, PoliticianPrice(view, decision, politician, side));
        int step = Engine.Steps.CouncilStep.MinDeal;

        int bestAmount = 0;
        double bestProfit = DealProfitNeeded, last = double.MinValue;
        for (int amount = (floor + step - 1) / step * step; amount <= spare; amount += step)
        {
            double chance = Chance(Influence(view, decision, view.ViewerId, side, new VoteDeal(view.ViewerId, politician, amount, side)));
            double profit = Math.Abs(worth) * (chance - now) - amount;
            if (profit > bestProfit)
            {
                bestProfit = profit;
                bestAmount = amount;
            }
            // Each extra dollar buys a little less chance, so once the profit falls it only keeps falling.
            if (profit < last)
                break;
            last = profit;
        }
        return bestAmount > 0 ? new OfferVoteDeal(new VoteDeal(view.ViewerId, politician, bestAmount, side)) : new NoThanks();
    }

    /// <summary>
    /// A contract must leave the bot this many dollars ahead before it offers one. Low on purpose: a $10 offer
    /// already buys the Politician's whole vote, and the Sim showed bots hardly bid with a higher bar (decision #162).
    /// </summary>
    protected const double DealProfitNeeded = 5;

    /// <summary>
    /// About the least the Politician would take to vote <paramref name="side"/>: nothing extra if they would vote that
    /// way anyway, otherwise what giving up their own side costs them (see <see cref="AnswerDeal"/>).
    /// </summary>
    private int PoliticianPrice(PlayerView view, PendingDecision decision, int politician, VoteChoice side)
    {
        double theirWorth = OrdinanceWorth(view, politician, decision.OrdinanceId!, decision.DistrictId);
        if (Math.Abs(theirWorth) < MinVoteWorth || (theirWorth > 0) == (side == VoteChoice.Yes))
            return 0;
        VoteChoice theirSide = theirWorth > 0 ? VoteChoice.Yes : VoteChoice.No;
        double free = Chance(Influence(view, decision, politician, theirSide));
        // A contract at the price adds to their vote too, but the price isn't known yet, so this counts the vote alone.
        double sold = 1 - Chance(Influence(view, decision, politician, side));
        return (int)Math.Ceiling(Math.Abs(theirWorth) * (free - sold) + 10);
    }

    /// <summary>
    /// The Politician takes a contract that asks it to vote the way it would anyway (free money), or one that pays more
    /// than its own side is worth to it: the drop in its chance of getting its way, times what the vote means to it.
    /// </summary>
    protected override GameAction AnswerDeal(PlayerView view, PendingDecision decision)
    {
        VoteDeal deal = decision.Deal!;
        double worth = OrdinanceWorth(view, view.ViewerId, decision.OrdinanceId!, decision.DistrictId);
        bool agrees = Math.Abs(worth) < MinVoteWorth || (worth > 0) == (deal.Choice == VoteChoice.Yes);
        if (agrees)
            return new AcceptVoteDeal();
        VoteChoice mySide = worth > 0 ? VoteChoice.Yes : VoteChoice.No;
        double withMe = Chance(Influence(view, decision, view.ViewerId, mySide));
        // Voting their way, with their money behind it: its own side's chance is what's left.
        double sold = 1 - Chance(Influence(view, decision, view.ViewerId, deal.Choice, deal));
        double cost = Math.Abs(worth) * (withMe - sold);
        return deal.Amount >= cost + 10 ? new AcceptVoteDeal() : new DeclineVoteDeal();
    }

    /// <summary>
    /// What one more building level on each of its groups in this district would earn before the game ends
    /// (plus Green's dividend for each new hotel), for a group already built to the zoning limit.
    /// </summary>
    private double RezoneWorth(PlayerView view, string districtId)
    {
        int opponents = Math.Max(1, view.Players.Count(p => !p.IsBankrupt && p.Id != view.ViewerId));
        int dividend = GroupPerkModifier(view, view.ViewerId, "DividendPerTower");
        double rounds = Math.Min(RoundsLeft(view), NearRounds);
        double worth = 0;
        foreach (SpaceInfo space in view.Spaces.Where(s => s.DistrictId == districtId && s.Kind == SpaceKind.Property))
        {
            PropertyInfo property = view.Properties[space.Id];
            PropertyDefinition def = content.Property(space.Id);
            if (property.OwnerId != view.ViewerId || def.Rents == null || property.Level + 1 >= def.Rents.Count)
                continue;
            double gain = (def.Rents[property.Level + 1] - def.Rents[property.Level]) * opponents / (double)view.Spaces.Count;
            if (property.Level + 1 == content.Rules.HotelLevel)
                gain += dividend;
            worth += gain * rounds - property.NextLevelCost * 0.5;
        }
        return Math.Max(80, worth);
    }

    /// <summary>A rough dollar size of how much an ordinance matters to this bot.</summary>
    protected double Impact(PlayerView view, string ordinanceId, string? districtId)
    {
        OrdinanceDefinition def = content.Ordinance(ordinanceId);
        if (def.EffectType == "DistrictRent" && districtId != null)
        {
            int others = Math.Max(1, view.Players.Count(p => !p.IsBankrupt) - 1);
            double mine = 0, theirs = 0;
            foreach (SpaceInfo space in view.Spaces.Where(s => s.DistrictId == districtId && s.Kind == SpaceKind.Property))
            {
                double rent = EstimateRent(view, space.Id, 7);
                if (view.Properties[space.Id].OwnerId == view.ViewerId) mine += rent;
                else theirs += rent;
            }
            double swing = Math.Abs(def.Int("percent")) / 100.0 * Math.Abs(mine - theirs / others) * 3;
            return Math.Max(60, swing);
        }
        if (def.EffectType == "Rezone" && districtId != null && Benefit(view, ordinanceId, districtId) == 2)
            return RezoneWorth(view, districtId);
        return 80;
    }
}
